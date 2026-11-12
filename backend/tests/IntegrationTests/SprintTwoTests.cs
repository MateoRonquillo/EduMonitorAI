extern alias AcademicApi;
extern alias MoodleApi;
extern alias AgentApi;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EduMonitorAI.Integration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Analysis = AcademicApi::AcademicData.API.Application.AcademicAnalysis;
using Rules = AcademicApi::AcademicData.API.Application.AnalysisRules;

namespace EduMonitorAI.IntegrationTests;

public class SprintTwoTests
{
    static readonly DateTimeOffset At = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
    static Grade Grade(long student, decimal? value, string state = "graded", decimal max = 10, long item = 1) =>
        new(student, item, "Actividad", "assign", item, value, 0, max, null, state);
    static Snapshot Snapshot(params Grade[] grades) => new(10, "Curso", At, "Moodle",
        [new(20, "Uno"), new(21, "Dos"), new(22, "Tres")], grades,
        JsonDocument.Parse("""[{"id":1,"name":"Álgebra","modules":[{"id":101,"modname":"assign","instance":1}]}]""").RootElement.Clone());

    [Fact]
    public void NormalizationPreservesZeroAndExcludesNullAndTotals()
    {
        var stats = Analysis.Calculate([Grade(20, 0), Grade(21, 10), Grade(22, null, "pending"),
            Grade(20, 50, max:100, item:2), Grade(20, 100) with { Module = null }]);
        Assert.Equal(50, stats.MeanPercent);
        Assert.Equal(3, stats.Observations); Assert.Equal(1, stats.Excluded);
        Assert.Equal(2, stats.Distribution.Below7); Assert.Equal(1, stats.Distribution.From9To10);
    }
    [Fact]
    public void InvalidRangesQualitativeScalesAndOutOfRangeAreExcluded()
    {
        var stats = Analysis.Calculate([Grade(20, 11), Grade(20, 1) with { ScaleId = 3 }, Grade(20, 0, max:0)]);
        Assert.Null(stats.MeanPercent); Assert.Equal(0, stats.Observations); Assert.Equal(3, stats.Excluded);
    }
    [Fact]
    public void GradeDistributionUsesZeroToTenAndSevenAsMinimum()
    {
        var stats = Analysis.Calculate([Grade(20, 0), Grade(20, 6.99m), Grade(20, 7), Grade(20, 8.99m), Grade(20, 9), Grade(20, 10)]);
        Assert.Equal(2, stats.Distribution.Below7);
        Assert.Equal(2, stats.Distribution.From7To89);
        Assert.Equal(2, stats.Distribution.From9To10);
        Assert.Equal(0, stats.MinimumGrade); Assert.Equal(10, stats.MaximumGrade);
        Assert.Equal(70, new Rules().LowGradePercent);
        Assert.Equal("0–10", stats.Scale);
    }
    [Fact]
    public void EmptyCourseHasNoInventedValuesOrFindings()
    {
        var report = new Analysis(new Rules()).Build(Snapshot() with { Students = [] });
        Assert.Null(report.Statistics.MeanPercent); Assert.Empty(report.Alerts); Assert.Empty(report.Reinforcement);
    }
    [Fact]
    public void AlertsRespectStrictBoundaryAndVerifiedDeadline()
    {
        var report = new Analysis(new Rules()).Build(Snapshot(Grade(20, 7), Grade(21, 6.99m),
            Grade(22, null, "pending") with { DueAt = At.AddDays(-1) },
            Grade(20, null, "unknown", item:2) with { DueAt = At.AddDays(-1) },
            Grade(21, null, "not_submitted", item:2),
            Grade(22, null, "not_submitted", item:2) with { DueAt = At.AddSeconds(-1) },
            Grade(20, null, "not_submitted", item:3) with { DueAt = At }));
        Assert.Equal(2, report.Alerts.Length);
        Assert.Contains(report.Alerts, a => a.Rule == "low_grade" && a.StudentId == 21);
        Assert.Contains(report.Alerts, a => a.Rule == "overdue_submission" && a.StudentId == 22);
    }
    [Fact]
    public void ContentRequiresVerifiedMappingAndEnoughDistinctStudents()
    {
        var analysis = new Analysis(new Rules());
        var snapshot = Snapshot(Grade(20, 1), Grade(21, 2), Grade(22, 9));
        var finding = Assert.Single(analysis.Build(snapshot).Reinforcement);
        Assert.Equal(3, finding.Students); Assert.Equal(2, finding.BelowThreshold);
        Assert.Equal(66.67m, finding.BelowThresholdPercent); Assert.Equal("Álgebra", finding.Content);
        var unmapped = analysis.Build(snapshot with { Activities = JsonDocument.Parse("[]").RootElement.Clone() });
        Assert.Empty(unmapped.Reinforcement); Assert.Equal(1, unmapped.UnmappedActivities);
        Assert.Empty(analysis.Build(Snapshot(Grade(20, 1), Grade(21, 2))).Reinforcement);
    }
    [Fact]
    public void StudentProfileNeverFallsBackToAnotherStudent()
    {
        var analysis = new Analysis(new Rules());
        var snapshot = Snapshot(Grade(20, 0), Grade(21, 8));
        var profile = analysis.Profile(snapshot, 20);
        Assert.All(profile.Grades, g => Assert.Equal(20, g.StudentId));
        Assert.Equal(0, profile.Statistics.MeanPercent);
        Assert.Equal(404, Assert.Throws<ApiFailure>(() => analysis.Profile(snapshot, 99)).Status);
    }
    [Theory]
    [InlineData("/api/courses/10/analysis")]
    [InlineData("/api/courses/10/students/20")]
    public async Task NewAcademicRoutesDenyRevokedPermissionsBeforeDatabase(string path)
    {
        using var factory = MoodleFactory<AcademicApi::Program>(_ => """{"userId":7,"name":"Docente","courses":[]}""");
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", "revoked");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
    }
    [Fact]
    public async Task MoodleSubmissionEvidenceKeepsFourStates()
    {
        using var factory = MoodleFactory<MoodleApi::Program>(body =>
            body.Contains("get_access") ? """{"userId":7,"name":"D","courses":[{"id":10,"name":"C"}]}""" :
            body.Contains("get_enrolled") ? """[{"id":20,"fullname":"A","roles":[{"shortname":"student"}]}]""" :
            body.Contains("get_contents") ? "[]" :
            body.Contains("get_states") ? """[{"studentId":20,"instance":2,"state":"not_submitted","dueAt":0,"submittedAt":0},{"studentId":20,"instance":3,"state":"pending","dueAt":0,"submittedAt":123}]""" :
            """{"usergrades":[{"userid":20,"gradeitems":[{"id":1,"itemname":"Cero","itemmodule":"assign","iteminstance":1,"graderaw":0,"grademin":0,"grademax":10},{"id":2,"itemname":"Sin entrega","itemmodule":"assign","iteminstance":2,"graderaw":null,"grademin":0,"grademax":10},{"id":3,"itemname":"Pendiente","itemmodule":"assign","iteminstance":3,"graderaw":null,"grademin":0,"grademax":10},{"id":4,"itemname":"Desconocido","itemmodule":"quiz","iteminstance":4,"graderaw":null,"grademin":0,"grademax":10}]}],"warnings":[]}""");
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", "teacher");
        var snapshot = await client.GetFromJsonAsync<Snapshot>("/api/courses/10/snapshot");
        Assert.Equal(new[] { "graded", "not_submitted", "pending", "unknown" }, snapshot!.Grades.Select(g => g.State));
        Assert.Equal(0, snapshot.Grades[0].Value); Assert.Null(snapshot.Grades[1].Value);
        Assert.NotNull(snapshot.Grades[2].SubmittedAt);
    }
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"actions\":[\"invented_grade_100\"]}")]
    [InlineData("{\"actions\":[\"teacher_review\"],\"grade\":100}")]
    [InlineData("{\"actions\":[]}")]
    public void ModelCannotPublishInventedFacts(string json)
    {
        Assert.Throws<ApiFailure>(() => AgentApi::ModelOutput.Validate(JsonDocument.Parse(json).RootElement,
            new Dictionary<string, string> { ["teacher_review"] = "Revisar evidencia" }));
    }
    [Fact]
    public async Task AgentRejectsUnauthorizedWithoutContactingModel()
    {
        using var factory = MoodleFactory<AgentApi::Program>(_ => """{"userId":7,"name":"D","courses":[]}""");
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", "teacher");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/courses/10/interpret", new { focus = "summary" })).StatusCode);
    }
    [Theory]
    [InlineData("false", "https://agent.test", false)]
    [InlineData("true", "https://agent.test", true)]
    [InlineData("true", "https://agent.test?secret=value", false)]
    public async Task AgentStatusProtectsConfigurationAndCourseAccess(string enabled, string endpoint, bool available)
    {
        using var factory = MoodleFactory<AgentApi::Program>(_ => """{"userId":7,"name":"D","courses":[{"id":10,"name":"C"}]}""")
            .WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
                ["Agent:Enabled"] = enabled, ["Agent:BaseUrl"] = endpoint, ["Agent:ApiKey"] = "private-key"
            })));
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/courses/10/agent-status")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "teacher");
        using var response = await client.GetAsync("/api/courses/10/agent-status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, json.EnumerateObject().Count());
        Assert.Equal(available, json.GetProperty("available").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/courses/11/agent-status")).StatusCode);
    }
    [Theory]
    [InlineData("Development", HttpStatusCode.OK)]
    [InlineData("Production", HttpStatusCode.NotFound)]
    public async Task AgentHealthAndOpenApiRespectEnvironment(string environment, HttpStatusCode expected)
    {
        using var factory = new WebApplicationFactory<AgentApi::Program>().WithWebHostBuilder(b => b.UseEnvironment(environment));
        using var client = factory.CreateClient();
        Assert.Equal("Healthy", await client.GetStringAsync("/health"));
        Assert.Equal(expected, (await client.GetAsync("/openapi/v1.json")).StatusCode);
    }

    sealed class MemoryStore : AcademicApi::AcademicData.API.Infrastructure.IAcademicStore
    {
        public Task<Snapshot?> Read(long course, CancellationToken ct) => Task.FromResult<Snapshot?>(Snapshot(Grade(20, 0), Grade(21, 10)));
        public Task Save(Snapshot s, long actor, CancellationToken ct) => Task.CompletedTask;
        public Task RecordFailure(long course, long actor, string code, CancellationToken ct) => Task.CompletedTask;
    }
    [Fact]
    public async Task AuthorizedAnalysisAndProfileUseOnlySelectedCourseAndStudent()
    {
        using var factory = MoodleFactory<AcademicApi::Program>(_ => """{"userId":7,"name":"D","courses":[{"id":10,"name":"C"}]}""")
            .WithWebHostBuilder(b => b.ConfigureServices(s => {
                s.RemoveAll<AcademicApi::AcademicData.API.Infrastructure.IAcademicStore>();
                s.AddSingleton<AcademicApi::AcademicData.API.Infrastructure.IAcademicStore, MemoryStore>();
            }));
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", "teacher");
        using var report = await client.GetAsync("/api/courses/10/analysis");
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        Assert.Equal("no-store", report.Headers.CacheControl!.ToString());
        var body = await report.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(50, body.GetProperty("statistics").GetProperty("meanPercent").GetDecimal());
        var profile = await client.GetFromJsonAsync<JsonElement>("/api/courses/10/students/20");
        Assert.Equal(20, profile.GetProperty("student").GetProperty("id").GetInt64());
        Assert.Single(profile.GetProperty("grades").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/courses/10/students/99")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/courses/11/students/20")).StatusCode);
    }

    sealed class ServiceStub(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => handle(request, ct);
    }
    [Theory]
    [InlineData("success", HttpStatusCode.OK)]
    [InlineData("offline", HttpStatusCode.ServiceUnavailable)]
    [InlineData("timeout", HttpStatusCode.GatewayTimeout)]
    [InlineData("invalid", HttpStatusCode.BadGateway)]
    [InlineData("service-key", HttpStatusCode.OK)]
    public async Task AgentUsesMinimizedFactsAndControlsModelFailures(string scenario, HttpStatusCode expected)
    {
        var contacted = false;
        using var factory = MoodleFactory<AgentApi::Program>(_ => """{"userId":7,"name":"D","courses":[{"id":10,"name":"PRIVATE_COURSE"}]}""")
            .WithWebHostBuilder(b => { b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string,string?> {
                ["Agent:Enabled"] = "true", ["Agent:BaseUrl"] = "https://agent.test",
                ["Agent:ApiKey"] = scenario == "service-key" ? "provider-secret" : null
            })); b.ConfigureServices(s => {
                s.AddHttpClient("academic").ConfigurePrimaryHttpMessageHandler(() => new ServiceStub((request, ct) => {
                    Assert.Equal("teacher-secret", request.Headers.Authorization!.Parameter);
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new {
                        source = "Moodle", syncedAt = At, students = 3,
                        courseName = "PRIVATE_COURSE", studentNames = new[] { "PRIVATE_STUDENT" },
                        statistics = new { observations = 3, meanPercent = 50 },
                        states = new { graded = 3 }, alerts = Array.Empty<object>(), reinforcement = Array.Empty<object>()
                    }) });
                }));
                s.AddHttpClient("agent-provider").ConfigurePrimaryHttpMessageHandler(() => new ServiceStub(async (request, ct) => {
                    contacted = true;
                    if (scenario == "service-key") Assert.Equal("provider-secret", request.Headers.Authorization!.Parameter);
                    else Assert.Null(request.Headers.Authorization);
                    var content = await request.Content!.ReadAsStringAsync(ct);
                    Assert.DoesNotContain("teacher-secret", content); Assert.DoesNotContain("PRIVATE", content);
                    if (scenario == "offline") throw new HttpRequestException();
                    if (scenario == "timeout") throw new TaskCanceledException();
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new {
                        actions = new[] { scenario == "invalid" ? "invented" : "teacher_review" }
                    }) };
                }));
            }); });
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", "teacher-secret");
        using var response = await client.PostAsJsonAsync("/api/courses/10/interpret", new { focus = "summary" });
        Assert.True(contacted); Assert.Equal(expected, response.StatusCode);
        var result = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("PRIVATE", result); Assert.DoesNotContain("teacher-secret", result); Assert.DoesNotContain("provider-secret", result);
        if (scenario == "success") Assert.Contains("confirmedData", result);
    }

    [Theory]
    [InlineData("false", "https://agent.test")]
    [InlineData("true", "http://remote.test/interpret")]
    [InlineData("true", "https://agent.test?secret=value")]
    public async Task AgentRequiresExplicitSecureConfiguration(string enabled, string endpoint)
    {
        using var factory = MoodleFactory<AgentApi::Program>(_ => """{"userId":7,"name":"D","courses":[{"id":10,"name":"C"}]}""")
            .WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string,string?> {
                ["Agent:Enabled"] = enabled, ["Agent:BaseUrl"] = endpoint
            })));
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", "teacher");
        var response = await client.PostAsJsonAsync("/api/courses/10/interpret", new { focus = "summary" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("agent_configuration_required", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("success", HttpStatusCode.OK)]
    [InlineData("offline", HttpStatusCode.ServiceUnavailable)]
    [InlineData("timeout", HttpStatusCode.GatewayTimeout)]
    [InlineData("invalid", HttpStatusCode.BadGateway)]
    [InlineData("oversized", HttpStatusCode.BadGateway)]
    public async Task ChatUsesAuthorizedAggregatesAndBoundedProviderOutput(string scenario, HttpStatusCode expected)
    {
        using var factory = MoodleFactory<AgentApi::Program>(_ => """{"userId":7,"name":"PRIVATE_TEACHER","courses":[{"id":10,"name":"PRIVATE_COURSE"}]}""")
            .WithWebHostBuilder(b => {
                b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string,string?> {
                    ["Agent:Enabled"] = "true", ["Agent:BaseUrl"] = "https://agent.test", ["Agent:ApiKey"] = "provider-key"
                }));
                b.ConfigureServices(s => {
                    s.AddHttpClient("academic").ConfigurePrimaryHttpMessageHandler(() => new ServiceStub((request, ct) => {
                        Assert.Equal("teacher-secret", request.Headers.Authorization!.Parameter);
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new {
                            students = 12, statistics = new { observations = 12, meanGrade = 8.5m }, states = new { graded = 12 },
                            alerts = Array.Empty<object>(), reinforcement = Array.Empty<object>(), rules = new { lowGradePercent = 70 },
                            source = "Moodle", syncedAt = At, courseName = "PRIVATE_COURSE"
                        }) });
                    }));
                    s.AddHttpClient("agent-provider").ConfigurePrimaryHttpMessageHandler(() => new ServiceStub(async (request, ct) => {
                        Assert.Equal("https://agent.test/chat", request.RequestUri!.ToString());
                        Assert.Equal("provider-key", request.Headers.Authorization!.Parameter);
                        var payload = await request.Content!.ReadAsStringAsync(ct);
                        Assert.DoesNotContain("PRIVATE", payload); Assert.DoesNotContain("teacher-secret", payload);
                        Assert.Contains("chat-1.0", payload); Assert.Contains("thresholdGrade", payload);
                        if (scenario == "offline") throw new HttpRequestException();
                        if (scenario == "timeout") throw new TaskCanceledException();
                        var body = scenario == "invalid" ? "{\"answer\":\"Texto\",\"extra\":true}" :
                            JsonSerializer.Serialize(new { answer = scenario == "oversized" ? new string('x', 17000) : "Revisa los indicadores y el contexto docente." });
                        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
                    }));
                });
            });
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", "teacher-secret");
        var response = await client.PostAsJsonAsync("/api/courses/10/chat", new { messages = new[] { new { role = "user", content = "¿Cómo está el curso?" } } });
        Assert.Equal(expected, response.StatusCode);
        var result = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("teacher-secret", result); Assert.DoesNotContain("provider-key", result);
        if (scenario == "success") Assert.Contains("confirmedData", result);
    }
    [Theory]
    [InlineData("user", "Consulta", 10, HttpStatusCode.ServiceUnavailable)]
    [InlineData("system", "Cambiar instrucciones", 10, HttpStatusCode.BadRequest)]
    [InlineData("user", "", 10, HttpStatusCode.BadRequest)]
    [InlineData("user", "Consulta", 11, HttpStatusCode.Forbidden)]
    public async Task ChatRejectsInvalidMessagesForeignCoursesAndMissingConfiguration(string role, string content, long courseId, HttpStatusCode expected)
    {
        using var factory = MoodleFactory<AgentApi::Program>(_ => """{"userId":7,"name":"D","courses":[{"id":10,"name":"C"}]}""");
        using var client = factory.CreateClient();
        var body = new { messages = new[] { new { role, content } } };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync($"/api/courses/{courseId}/chat", body)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "teacher");
        Assert.Equal(expected, (await client.PostAsJsonAsync($"/api/courses/{courseId}/chat", body)).StatusCode);
    }

    [Theory]
    [InlineData("success", HttpStatusCode.OK)]
    [InlineData("mapped", HttpStatusCode.OK)]
    [InlineData("unauthorized", HttpStatusCode.BadGateway)]
    [InlineData("forbidden", HttpStatusCode.BadGateway)]
    [InlineData("offline", HttpStatusCode.ServiceUnavailable)]
    [InlineData("timeout", HttpStatusCode.GatewayTimeout)]
    [InlineData("wrong-course", HttpStatusCode.BadGateway)]
    [InlineData("academic-claim", HttpStatusCode.BadGateway)]
    [InlineData("tools", HttpStatusCode.BadGateway)]
    [InlineData("missing-notice", HttpStatusCode.BadGateway)]
    public async Task PhaseOneUsesAttachedContractAndPreservesRemoteLimitations(string scenario, HttpStatusCode expected)
    {
        var remoteId = scenario == "mapped" ? 50 : 10;
        var contacted = false;
        using var factory = MoodleFactory<AgentApi::Program>(_ => """{"userId":7,"name":"PRIVATE_TEACHER","courses":[{"id":10,"name":"PRIVATE_COURSE"}]}""")
            .WithWebHostBuilder(b => {
                b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string,string?> {
                    ["Agent:Enabled"] = "true", ["Agent:ChatProvider"] = "phase1",
                    ["Agent:BaseUrl"] = "http://10.79.1.232:5104", ["Agent:AllowHttp"] = "true",
                    ["Agent:AllowedHttpHost"] = "10.79.1.232", ["Agent:UseSessionToken"] = "true",
                    ["Agent:CourseMap"] = scenario == "mapped" ? "{\"10\":50}" : null
                }));
                b.ConfigureServices(s => {
                    s.AddHttpClient("academic").ConfigurePrimaryHttpMessageHandler(() => new ServiceStub((r, ct) => throw new Exception("Phase 1 must not request or transmit academic data")));
                    s.AddHttpClient("agent-provider").ConfigurePrimaryHttpMessageHandler(() => new ServiceStub(async (request, ct) => {
                        contacted = true;
                        Assert.Equal($"http://10.79.1.232:5104/courses/{remoteId}/agent/messages", request.RequestUri!.ToString());
                        Assert.Equal("teacher-secret", request.Headers.Authorization!.Parameter);
                        var payload = await request.Content!.ReadAsStringAsync(ct);
                        using var body = JsonDocument.Parse(payload);
                        Assert.Single(body.RootElement.EnumerateObject());
                        Assert.Equal("Consulta actual", body.RootElement.GetProperty("message").GetString());
                        Assert.DoesNotContain("PRIVATE", payload); Assert.DoesNotContain("teacher-secret", payload);
                        if (scenario == "offline") throw new HttpRequestException();
                        if (scenario == "timeout") throw new TaskCanceledException();
                        if (scenario is "unauthorized" or "forbidden") return new HttpResponseMessage(scenario == "unauthorized" ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden) {
                            Content = JsonContent.Create(new { code = "unauthorized", detail = "private remote diagnostics" })
                        };
                        var response = new Dictionary<string, object> {
                            ["courseId"] = scenario == "wrong-course" ? 99 : remoteId,
                            ["answer"] = "No tengo herramientas académicas conectadas.",
                            ["academicDataAvailable"] = scenario == "academic-claim",
                            ["notice"] = "FASE 1: esta respuesta no verifica calificaciones.",
                            ["toolsUsed"] = scenario == "tools" ? new[] { "invented-tool" } : Array.Empty<string>(), ["traceId"] = "remote-trace"
                        };
                        if (scenario == "missing-notice") response.Remove("notice");
                        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(response) };
                    }));
                });
            });
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", "teacher-secret");
        using var response = await client.PostAsJsonAsync("/api/courses/10/chat", new { messages = new[] {
            new { role = "user", content = "PRIVATE_HISTORY" }, new { role = "assistant", content = "PRIVATE_REPLY" }, new { role = "user", content = "Consulta actual" }
        } });
        Assert.True(contacted); Assert.Equal(expected, response.StatusCode);
        Assert.Null(response.Headers.WwwAuthenticate.FirstOrDefault());
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("teacher-secret", raw); Assert.DoesNotContain("private remote diagnostics", raw);
        if (expected == HttpStatusCode.OK) {
            using var body = JsonDocument.Parse(raw);
            Assert.Equal(10, body.RootElement.GetProperty("courseId").GetInt64());
            Assert.False(body.RootElement.GetProperty("academicDataAvailable").GetBoolean());
            Assert.False(body.RootElement.GetProperty("supportsHistory").GetBoolean());
            Assert.Equal("FASE 1: esta respuesta no verifica calificaciones.", body.RootElement.GetProperty("notice").GetString());
            Assert.False(body.RootElement.TryGetProperty("confirmedData", out _));
        }
    }

    [Theory]
    [InlineData("false", "10.79.1.232", false)]
    [InlineData("true", "another-host", false)]
    [InlineData("true", "10.79.1.232", true)]
    public async Task PhaseOneHttpRequiresExplicitHostAndAuthorization(string allowHttp, string allowedHost, bool available)
    {
        using var factory = MoodleFactory<AgentApi::Program>(_ => """{"userId":7,"name":"D","courses":[{"id":10,"name":"C"}]}""")
            .WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string,string?> {
                ["Agent:Enabled"] = "true", ["Agent:ChatProvider"] = "phase1", ["Agent:BaseUrl"] = "http://10.79.1.232:5104",
                ["Agent:UseSessionToken"] = "true", ["Agent:AllowHttp"] = allowHttp, ["Agent:AllowedHttpHost"] = allowedHost
            })));
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/courses/10/agent-status")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "teacher");
        var status = await client.GetFromJsonAsync<JsonElement>("/api/courses/10/agent-status");
        Assert.Equal(available, status.GetProperty("chatAvailable").GetBoolean());
        Assert.False(status.GetProperty("academicDataAvailable").GetBoolean());
        Assert.Equal(4000, status.GetProperty("maximumMessageLength").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/courses/11/agent-status")).StatusCode);
    }

    [Theory]
    [InlineData("chat")]
    [InlineData("interpret")]
    public async Task AllPhaseOneOperationsUseTheSingleConfiguredApi(string operation)
    {
        var contacted = false;
        using var factory = MoodleFactory<AgentApi::Program>(_ => """{"userId":7,"name":"D","courses":[{"id":10,"name":"C"}]}""")
            .WithWebHostBuilder(b => {
                b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string,string?> {
                    ["Agent:Enabled"] = "true", ["Agent:ChatProvider"] = "phase1", ["Agent:BaseUrl"] = "https://agent.test/gateway",
                    ["Agent:UseSessionToken"] = "true", ["Agent:Endpoint"] = "https://obsolete.test/interpret",
                    ["Agent:ChatEndpoint"] = "https://obsolete.test/chat", ["Agent:Phase1:BaseUrl"] = "https://obsolete.test"
                }));
                b.ConfigureServices(s => s.AddHttpClient("agent-provider").ConfigurePrimaryHttpMessageHandler(() => new ServiceStub((request, ct) => {
                    contacted = true;
                    Assert.Equal("https://agent.test/gateway/courses/10/agent/messages", request.RequestUri!.ToString());
                    Assert.Equal("teacher", request.Headers.Authorization!.Parameter);
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new {
                        courseId = 10, answer = "Sin herramientas académicas.", academicDataAvailable = false,
                        notice = "FASE 1", toolsUsed = Array.Empty<string>()
                    }) });
                })));
            });
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", "teacher");
        object body = operation == "chat" ? new { messages = new[] { new { role = "user", content = "Consulta" } } } : new { focus = "summary" };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/courses/10/{operation}", body)).StatusCode);
        Assert.True(contacted);
    }

    [Fact]
    public void ObsoleteEndpointsCannotEnableAnAgentWithoutItsSingleBaseUrl()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["Agent:Enabled"] = "true", ["Agent:Endpoint"] = "https://obsolete.test/interpret",
            ["Agent:ChatEndpoint"] = "https://obsolete.test/chat", ["Agent:Phase1:BaseUrl"] = "https://obsolete.test"
        }).Build();
        Assert.False(AgentApi::ProviderConfiguration.IsReady(config, false));
    }

    sealed class Stub(Func<string, string> reply) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            new(HttpStatusCode.OK) { Content = new StringContent(reply(await request.Content!.ReadAsStringAsync(ct)), Encoding.UTF8, "application/json") };
    }
    static WebApplicationFactory<T> MoodleFactory<T>(Func<string,string> reply) where T : class =>
        new WebApplicationFactory<T>().WithWebHostBuilder(b => b.ConfigureServices(s => {
            s.RemoveAll<MoodleClient>();
            s.AddSingleton(new MoodleClient(new HttpClient(new Stub(reply)), new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string,string?> { ["Moodle:BaseUrl"] = "https://moodle.test" }).Build()));
        }));
}
