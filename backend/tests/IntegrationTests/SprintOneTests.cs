extern alias MoodleApi;
extern alias AcademicApi;
extern alias AccessApi;
using System.Net;
using System.Text;
using System.Text.Json;
using EduMonitorAI.Integration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
namespace EduMonitorAI.IntegrationTests;

public class SprintOneTests
{
    sealed class MoodleStub(Func<string, string> response) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.DoesNotContain("wstoken", request.RequestUri!.Query);
            var body = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response(body), Encoding.UTF8, "application/json") };
        }
    }
    static WebApplicationFactory<T> Factory<T>(Func<string, string> response) where T : class =>
     new WebApplicationFactory<T>().WithWebHostBuilder(b => b.ConfigureServices(services =>
     {
         services.RemoveAll<MoodleClient>();
         services.AddSingleton(new MoodleClient(new HttpClient(new MoodleStub(response)),
       new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Moodle:BaseUrl"] = "https://moodle.test" }).Build()));
     }));
    const string Teacher = """{"userId":7,"name":"Docente","courses":[{"id":10,"name":"Curso"}]}""";
    [Fact]
    public async Task TeacherCanReadOwnCourses()
    {
        using var factory = Factory<AccessApi::Program>(_ => Teacher);
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", "teacher");
        var response = await client.GetAsync("/api/access/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Curso", await response.Content.ReadAsStringAsync());
    }
    [Theory]
    [InlineData("/api/access/courses/11")]
    [InlineData("/api/access/courses/0")]
    public async Task OtherCourseDenied(string route)
    {
        using var factory = Factory<AccessApi::Program>(_ => Teacher);
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", "teacher");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(route)).StatusCode);
    }
    [Fact]
    public async Task StudentDenied()
    {
        using var factory = Factory<AccessApi::Program>(_ => """{"userId":8,"name":"Alumno","courses":[]}""");
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", "student");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/access/me")).StatusCode);
    }
    [Fact]
    public async Task MissingTokenDeniedBeforeMoodle()
    {
        using var factory = Factory<MoodleApi::Program>(_ => throw new Exception("Should not call Moodle"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/courses")).StatusCode);
    }
    [Fact]
    public async Task ExpiredTokenDenied()
    {
        using var factory = Factory<AccessApi::Program>(_ => """{"exception":"moodle_exception","errorcode":"invalidtoken"}""");
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", "expired");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/access/me")).StatusCode);
    }
    [Fact]
    public async Task CachedDataStillChecksCurrentPermissions()
    {
        using var factory = Factory<AcademicApi::Program>(_ => """{"userId":8,"name":"Alumno","courses":[]}""");
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", "student");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/courses/10/data")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/courses/10/sync", null)).StatusCode);
    }
    [Fact]
    public async Task SnapshotPreservesZeroNullAndScale()
    {
        using var factory = Factory<MoodleApi::Program>(body =>
         body.Contains("local_edumonitor_get_access") ? Teacher :
         body.Contains("core_enrol_get_enrolled_users") ? """[{"id":20,"fullname":"Alumno","roles":[{"shortname":"student"}]}]""" :
         body.Contains("core_course_get_contents") || body.Contains("local_edumonitor_get_states") ? "[]" :
         """{"usergrades":[{"userid":20,"gradeitems":[{"id":1,"itemname":"Cero","graderaw":0,"grademin":0,"grademax":10},{"id":2,"itemname":"Pendiente","graderaw":null,"grademin":0,"grademax":100}]}],"warnings":[]}""");
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", "teacher");
        var response = await client.GetAsync("/api/courses/10/snapshot");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var grades = json.RootElement.GetProperty("grades");
        Assert.Equal(0, grades[0].GetProperty("value").GetDecimal());
        Assert.Equal("graded", grades[0].GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, grades[1].GetProperty("value").ValueKind);
        Assert.Equal("unknown", grades[1].GetProperty("state").GetString());
        Assert.Equal(100, grades[1].GetProperty("maximum").GetDecimal());
    }
    [Fact]
    public async Task MalformedMoodleResponseIsControlled()
    {
        using var factory = Factory<AccessApi::Program>(_ => "<html>unavailable</html>");
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", "teacher");
        Assert.Equal(HttpStatusCode.BadGateway, (await client.GetAsync("/api/access/me")).StatusCode);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("range")]
    [InlineData("orphan")]
    public async Task InvalidSnapshotCannotReachPersistence(string scenario)
    {
        var store = new AcademicApi::AcademicData.API.Infrastructure.AcademicStore(new ConfigurationBuilder().Build());
        var grade = new Grade(scenario == "orphan" ? 99 : 20, 1, "Actividad", "assign", 1,
            scenario == "range" ? 11 : 0, 0, 10, null, "graded");
        var snapshot = new Snapshot(10, "Curso", DateTimeOffset.UtcNow, "Moodle",
            [new Student(20, "Alumno")], scenario == "duplicate" ? [grade, grade] : [grade],
            JsonDocument.Parse("[]").RootElement.Clone());
        var error = await Assert.ThrowsAsync<ApiFailure>(() => store.Save(snapshot, 7, CancellationToken.None));
        Assert.Equal("invalid_academic_data", error.Message);
        Assert.Equal(502, error.Status);
    }
}
