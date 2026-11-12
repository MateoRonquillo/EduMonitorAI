using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using EduMonitorAI.Integration;

// Adapter for the other team's Phase 1 contract. No academic facts or history are sent.
public static class PhaseOneAgent
{
    public const string Notice = "FASE 1: el agente todavía no tiene herramientas académicas conectadas. Sus respuestas no verifican notas, progreso ni alertas. Cada consulta se envía por separado, sin historial.";
    public static bool IsSelected(IConfiguration config) => config["Agent:ChatProvider"] == "phase1";

    public static bool IsReady(IConfiguration config, long courseId) =>
        ProviderConfiguration.IsReady(config, false) &&
        config.GetValue<bool>("Agent:UseSessionToken") &&
        config.GetValue<int?>("Agent:TimeoutSeconds") is null or (>= 1 and <= 600) &&
        MappedCourse(config, courseId) is > 0;

    public static long? MappedCourse(IConfiguration config, long courseId)
    {
        if (courseId <= 0) return null;
        var mapping = config["Agent:CourseMap"];
        if (string.IsNullOrWhiteSpace(mapping)) return courseId;
        try {
            var courses = JsonSerializer.Deserialize<Dictionary<string, long>>(mapping);
            return courses is not null && courses.TryGetValue(courseId.ToString(CultureInfo.InvariantCulture), out var remoteId) && remoteId > 0
                ? remoteId : null;
        }
        catch (JsonException) { return null; }
    }

    public static async Task<IResult> Send(long courseId, string message, string sessionToken,
        HttpContext context, IHttpClientFactory clients, IConfiguration config, ILogger logger, CancellationToken ct)
    {
        if (!IsReady(config, courseId)) throw new ApiFailure(503, "agent_configuration_required");
        var remoteId = MappedCourse(config, courseId)!.Value;
        var endpoint = ProviderConfiguration.Endpoint(config, $"courses/{remoteId}/agent/messages");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = JsonContent.Create(new { message }) };
            // Explicitly enabled for the named destination only. Redirects are disabled on this client.
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(config.GetValue<int?>("Agent:TimeoutSeconds") ?? 120));
            using var response = await clients.CreateClient("agent-provider").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            // Remote identity errors must not invalidate the local Moodle session (local 401 logs out the UI).
            if (response.StatusCode == HttpStatusCode.Unauthorized) throw new ApiFailure(502, "agent_provider_authentication_failed");
            if (response.StatusCode == HttpStatusCode.Forbidden) throw new ApiFailure(502, "agent_provider_course_forbidden");
            if (response.StatusCode == HttpStatusCode.GatewayTimeout) throw new ApiFailure(504, "agent_timeout");
            if (!response.IsSuccessStatusCode) throw new ApiFailure(response.StatusCode == HttpStatusCode.ServiceUnavailable ? 503 : 502, "agent_provider_error");
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            var buffer = new byte[32769];
            var length = 0;
            while (length < buffer.Length) {
                var read = await stream.ReadAsync(buffer.AsMemory(length), timeout.Token);
                if (read == 0) break;
                length += read;
            }
            if (length > 32768) throw new ApiFailure(502, "invalid_model_output");
            using var json = JsonDocument.Parse(buffer.AsMemory(0, length));
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("courseId", out var id) || !id.TryGetInt64(out var returnedId) || returnedId != remoteId ||
                !root.TryGetProperty("answer", out var answer) || answer.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(answer.GetString()) || answer.GetString()!.Length > 8000 ||
                !root.TryGetProperty("notice", out var notice) || notice.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(notice.GetString()) || notice.GetString()!.Length > 2000 ||
                !root.TryGetProperty("academicDataAvailable", out var academic) || academic.ValueKind != JsonValueKind.False ||
                !root.TryGetProperty("toolsUsed", out var tools) || tools.ValueKind != JsonValueKind.Array || tools.GetArrayLength() != 0)
                throw new ApiFailure(502, "invalid_model_output");
            logger.LogInformation("Chat Fase 1 completado para curso {CourseId}; traza {TraceId}", courseId, context.TraceIdentifier);
            return Results.Ok(new { courseId, answer = answer.GetString(), notice = notice.GetString(),
                academicDataAvailable = false, toolsUsed = Array.Empty<string>(), supportsHistory = false, provider = "phase1",
                traceId = context.TraceIdentifier });
        }
        catch (HttpRequestException) { throw new ApiFailure(503, "agent_unavailable"); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ApiFailure(504, "agent_timeout"); }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException or OverflowException)
            { throw new ApiFailure(502, "invalid_model_output"); }
    }
}
