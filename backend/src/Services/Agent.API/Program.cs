using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using EduMonitorAI.Integration;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIntegration();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();
builder.Services.AddHttpClient("academic", c => c.Timeout = TimeSpan.FromSeconds(30))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddHttpClient("agent-provider", c => c.Timeout = Timeout.InfiniteTimeSpan)
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddRateLimiter(o => {
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddConcurrencyLimiter("model", l =>
        { l.PermitLimit = 2; l.QueueLimit = 0; l.QueueProcessingOrder = QueueProcessingOrder.OldestFirst; });
});
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 16384);
var app = builder.Build();
app.UseApiErrors();
app.UseRateLimiter();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapGet("/health", () => Results.Text("Healthy", "text/plain"));
app.MapGet("/api/courses/{courseId:long}/agent-status", async (long courseId,
    HttpContext context, MoodleClient moodle, IConfiguration config, CancellationToken ct) =>
{
    await moodle.Authorize(context.Token(), courseId, ct);
    if (PhaseOneAgent.IsSelected(config)) return Results.Ok(new {
        available = PhaseOneAgent.IsReady(config, courseId),
        chatAvailable = PhaseOneAgent.IsReady(config, courseId), mode = "phase1", supportsHistory = false,
        academicDataAvailable = false, maximumMessageLength = 4000, notice = PhaseOneAgent.Notice
    });
    return Results.Ok(new { available = ProviderConfiguration.IsReady(config, app.Environment.IsDevelopment()),
        chatAvailable = ProviderConfiguration.IsReady(config, app.Environment.IsDevelopment()) });
});
app.MapPost("/api/courses/{courseId:long}/interpret", async (long courseId, InterpretationRequest input,
    HttpContext context, MoodleClient moodle, IHttpClientFactory clients, IConfiguration config, CancellationToken ct) =>
{
    var token = context.Token();
    await moodle.Authorize(token, courseId, ct);
    if (input.Focus is not ("summary" or "alerts" or "reinforcement")) throw new ApiFailure(400, "invalid_focus");
    if (PhaseOneAgent.IsSelected(config)) return await PhaseOneAgent.Send(courseId,
        input.Focus switch { "alerts" => "¿Qué puedes explicar sobre las alertas de este curso con la información verificada disponible?", "reinforcement" => "¿Qué orientación de refuerzo puedes ofrecer con la evidencia disponible?", _ => "Resume qué información puedes consultar de este curso y sus limitaciones actuales." },
        token, context, clients, config, app.Logger, ct);
    if (!ProviderConfiguration.IsReady(config, app.Environment.IsDevelopment()))
        throw new ApiFailure(503, "agent_configuration_required");
    var uri = ProviderConfiguration.Endpoint(config, "interpret");
    var baseUrl = config["Services:AcademicData"] ?? "http://localhost:5102";
    using var request = new HttpRequestMessage(HttpMethod.Get, baseUrl.TrimEnd('/') + $"/api/courses/{courseId}/analysis");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    JsonElement report;
    try
    {
        using var response = await clients.CreateClient("academic").SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw new ApiFailure((int)response.StatusCode, "academic_data_unavailable");
        report = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
    }
    catch (HttpRequestException) { throw new ApiFailure(502, "academic_service_unavailable"); }
    catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ApiFailure(504, "academic_timeout"); }
    catch (JsonException) { throw new ApiFailure(502, "invalid_academic_response"); }
    // No identifiers, course/section titles, student names, credentials or user prose enter the model context.
    var facts = new {
        students = report.GetProperty("students").GetInt32(), statistics = report.GetProperty("statistics"),
        states = report.GetProperty("states"), alertCount = report.GetProperty("alerts").GetArrayLength(),
        reinforcementCount = report.GetProperty("reinforcement").GetArrayLength()
    };
    if (report.GetProperty("statistics").GetProperty("observations").GetInt32() == 0)
        throw new ApiFailure(422, "insufficient_verified_data");
    var actions = new Dictionary<string, string> {
        ["review_examples"] = "Revisar ejemplos resueltos y contrastarlos con las actividades evaluadas.",
        ["formative_check"] = "Proponer una comprobación formativa breve antes de decidir un refuerzo.",
        ["teacher_review"] = "Revisar las evidencias y el contexto del curso antes de tomar decisiones."
    };
    JsonElement generated;
    try
    {
        using var modelRequest = new HttpRequestMessage(HttpMethod.Post, uri) {
            Content = JsonContent.Create(new { contractVersion = "1.0", focus = input.Focus, facts,
                allowedActions = actions.Keys.ToArray() })
        };
        var apiKey = config["Agent:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
            modelRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));
        using var response = await clients.CreateClient("agent-provider").SendAsync(modelRequest, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (!response.IsSuccessStatusCode) throw new ApiFailure(503, "agent_unavailable");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[16385];
        var length = 0;
        while (length < buffer.Length) {
            var n = await stream.ReadAsync(buffer.AsMemory(length), timeout.Token);
            if (n == 0) break;
            length += n;
        }
        if (length > 16384) throw new ApiFailure(502, "invalid_model_output");
        using var envelope = JsonDocument.Parse(buffer.AsMemory(0, length));
        generated = envelope.RootElement.Clone();
    }
    catch (HttpRequestException) { throw new ApiFailure(503, "agent_unavailable"); }
    catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ApiFailure(504, "agent_timeout"); }
    catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException)
        { throw new ApiFailure(502, "invalid_model_output"); }
    var selected = ModelOutput.Validate(generated, actions);
    app.Logger.LogInformation("Interpretación API completada para curso {CourseId}; foco {Focus}; traza {TraceId}", courseId, input.Focus, context.TraceIdentifier);
    return Results.Ok(new {
        courseId, source = report.GetProperty("source"), syncedAt = report.GetProperty("syncedAt"),
        confirmedData = facts, interpretation = selected, provider = "external-api",
        notice = "Sugerencias seleccionadas por API de IA y acotadas por el backend. Requieren criterio docente; no son diagnósticos ni modifican Moodle."
    });
}).RequireRateLimiting("model");
app.MapAcademicChat();
app.Run();
public sealed record InterpretationRequest(string Focus);
public static class ProviderConfiguration
{
    public static Uri Endpoint(IConfiguration config, string path) => new(config["Agent:BaseUrl"]!.TrimEnd('/') + "/" + path.TrimStart('/'));
    public static bool IsReady(IConfiguration config, bool development) =>
        config.GetValue<bool>("Agent:Enabled") &&
        Uri.TryCreate(config["Agent:BaseUrl"], UriKind.Absolute, out var uri) &&
        (uri.Scheme == "https" || (uri.Scheme == "http" && ((uri.IsLoopback && development) ||
            (config.GetValue<bool>("Agent:AllowHttp") && !string.IsNullOrWhiteSpace(config["Agent:AllowedHttpHost"]) &&
                uri.Host.Equals(config["Agent:AllowedHttpHost"], StringComparison.OrdinalIgnoreCase))))) &&
        uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0;
}
public static class ModelOutput
{
    public static string[] Validate(JsonElement result, IReadOnlyDictionary<string, string> allowed)
    {
        if (result.ValueKind != JsonValueKind.Object || result.EnumerateObject().Count() != 1 ||
            !result.TryGetProperty("actions", out var list) || list.ValueKind != JsonValueKind.Array || list.GetArrayLength() is < 1 or > 3)
            throw new ApiFailure(502, "invalid_model_output");
        var values = list.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : null).ToArray();
        if (values.Any(x => x is null || !allowed.ContainsKey(x))) throw new ApiFailure(502, "invalid_model_output");
        return values.Select(x => allowed[x!]).Distinct().ToArray();
    }
}
public partial class Program { }
