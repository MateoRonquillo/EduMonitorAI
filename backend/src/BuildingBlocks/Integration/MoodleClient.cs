using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EduMonitorAI.Integration;

public sealed class ApiFailure(int status, string code) : Exception(code)
{
    public int Status { get; } = status;
}
public sealed record Course(long Id, string Name);
public sealed record Access(long UserId, string Name, Course[] Courses);
public sealed record Student(long Id, string Name);
public sealed record Grade(long StudentId, long ItemId, string Name, string? Module, long? Instance,
    decimal? Value, decimal Minimum, decimal Maximum, long? ScaleId, string State,
    DateTimeOffset? DueAt = null, DateTimeOffset? SubmittedAt = null);
public sealed record Snapshot(long CourseId, string CourseName, DateTimeOffset SyncedAt, string Source,
    Student[] Students, Grade[] Grades, JsonElement Activities);

public sealed class MoodleClient(HttpClient http, IConfiguration config)
{
    public async Task<JsonElement> Call(string token, string function, CancellationToken ct,
        params (string Key, string Value)[] args)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new ApiFailure(401, "missing_token");
        var url = config["Moodle:BaseUrl"];
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && !(uri.Scheme == "http" && config.GetValue<bool>("Moodle:AllowHttp"))))
            throw new ApiFailure(503, "moodle_configuration_required");
        var fields = new Dictionary<string, string>
        {
            ["wstoken"] = token,
            ["wsfunction"] = function,
            ["moodlewsrestformat"] = "json"
        };
        foreach (var (key, value) in args) fields[key] = value;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url!.TrimEnd('/') + "/webservice/rest/server.php")
            { Content = new FormUrlEncodedContent(fields) };
            // Docker DNS differs from Moodle's canonical browser hostname.
            if (!string.IsNullOrWhiteSpace(config["Moodle:HostHeader"]))
                request.Headers.Host = config["Moodle:HostHeader"];
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) throw new ApiFailure(502, "moodle_http_error");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = json.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("exception", out _))
            {
                var code = root.TryGetProperty("errorcode", out var error) ? error.GetString() : "";
                throw new ApiFailure(code == "invalidtoken" ? 401 : 403, "moodle_access_denied");
            }
            return root.Clone();
        }
        catch (HttpRequestException) { throw new ApiFailure(502, "moodle_unavailable"); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ApiFailure(504, "moodle_timeout"); }
        catch (JsonException) { throw new ApiFailure(502, "moodle_invalid_response"); }
    }
    public async Task<Access> Authorize(string token, long? courseId, CancellationToken ct)
    {
        var json = await Call(token, "local_edumonitor_get_access", ct);
        Access access;
        try
        {
            access = JsonSerializer.Deserialize<Access>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new ApiFailure(502, "moodle_invalid_response");
        }
        catch (JsonException) { throw new ApiFailure(502, "moodle_invalid_response"); }
        if (access.Courses is null || access.UserId <= 0) throw new ApiFailure(502, "moodle_invalid_response");
        if (access.Courses.Length == 0 || courseId.HasValue && !access.Courses.Any(c => c.Id == courseId))
            throw new ApiFailure(403, "course_access_denied");
        return access;
    }
}
public static class ApiSetup
{
    public static void AddIntegration(this IServiceCollection services)
    {
        services.AddHttpClient<MoodleClient>(client => client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
    }
    public static string Token(this HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) || header.Length > 512)
            throw new ApiFailure(401, "missing_token");
        return header[7..].Trim();
    }
    public static void UseApiErrors(this WebApplication app) => app.Use(async (context, next) =>
    {
        context.Response.Headers.CacheControl = "no-store";
        try { await next(context); }
        catch (ApiFailure ex)
        {
            app.Logger.LogWarning("Operación rechazada: {Code}; estado {Status}; traza {TraceId}", ex.Message, ex.Status, context.TraceIdentifier);
            context.Response.StatusCode = ex.Status;
            if (ex.Status == 401) context.Response.Headers.WWWAuthenticate = "Bearer";
            await context.Response.WriteAsJsonAsync(new { error = ex.Message, traceId = context.TraceIdentifier });
        }
    });
}
