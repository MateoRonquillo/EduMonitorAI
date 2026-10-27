using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EduMonitorAI.AcademicData.Persistence;
using EduMonitorAI.AcademicData.Sync;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace EduMonitorAI.UnitTests;

public class SyncEndpointTests
{
    private const string Body = """
        {
          "course": { "moodleId": 50, "shortName": "CURSO-A", "name": "Curso A" },
          "students": [ { "moodleId": 201, "fullName": "Estudiante Uno" }, { "moodleId": 202, "fullName": "Estudiante Dos" } ],
          "activities": [ { "moodleId": 10, "name": "Examen parcial 1", "category": "Examen", "weightPercent": 40, "maxScale": 10 } ],
          "grades": [
            { "activityMoodleId": 10, "studentMoodleId": 201, "value": 8 },
            { "activityMoodleId": 10, "studentMoodleId": 202, "value": null },
            { "activityMoodleId": 10, "studentMoodleId": 203, "value": 5 },
            { "activityMoodleId": 10, "studentMoodleId": 201, "value": 12 },
            { "activityMoodleId": 10, "studentMoodleId": 202, "value": "abc" }
          ]
        }
        """;

    private static WebApplicationFactory<Program> CreateFactory(IAcademicDataStore? store = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            if (store is not null)
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IAcademicDataStore>();
                    services.AddSingleton(store);
                });
            }
        });

    private static StringContent Json(string body) => new(body, System.Text.Encoding.UTF8, "application/json");

    [Fact]
    public async Task Sync_ReportsRejectedRecords_AndStoresTheValidOnes()
    {
        var store = new CapturingStore();
        using var factory = CreateFactory(store);
        using var client = factory.CreateClient();

        using var response = await client.PostAsync("/courses/50/sync", Json(Body));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = (await response.Content.ReadFromJsonAsync<SyncSummary>(new JsonSerializerOptions(JsonSerializerDefaults.Web)))!;
        Assert.Equal(3, summary.Rejected);
        Assert.Equal(["unknown_student", "out_of_scale", "non_numeric_value"], summary.RejectedRecords.Select(r => r.Reason).ToArray());
        Assert.Equal(1, summary.WithoutGrade);
        var saved = Assert.IsType<NormalizedCourseData>(store.Saved);
        Assert.Equal(2, saved.Grades.Count);
        Assert.Contains(saved.Grades, g => g is { StudentMoodleId: 202, Value: null });
    }

    [Fact]
    public async Task Sync_ReportsUnchangedRecordsAsDuplicates()
    {
        var store = new CapturingStore { Result = new StoreResult(0, 0, 2) };
        using var factory = CreateFactory(store);
        using var client = factory.CreateClient();

        using var response = await client.PostAsync("/courses/50/sync", Json(Body));

        var summary = (await response.Content.ReadFromJsonAsync<SyncSummary>(new JsonSerializerOptions(JsonSerializerDefaults.Web)))!;
        Assert.Equal(0, summary.Inserted);
        Assert.Equal(2, summary.DuplicatesSkipped);
    }

    [Fact]
    public async Task Sync_ReturnsBadRequest_WhenRouteAndBodyCoursesDiffer()
    {
        var store = new CapturingStore();
        using var factory = CreateFactory(store);
        using var client = factory.CreateClient();

        using var response = await client.PostAsync("/courses/51/sync", Json(Body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(store.Saved);
    }

    [Fact]
    public async Task Cp52_Sync_ReturnsControlledError_WhenDatabaseIsNotConfigured()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync("/courses/50/sync", Json(Body));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("database_unavailable", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Cp52_Sync_ReturnsControlledError_WhenDatabaseRefusesConnection_AndHostKeepsRunning()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting(
                "ConnectionStrings:AcademicDb",
                "Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=2;Command Timeout=2");
        });
        using var client = factory.CreateClient();

        using var response = await client.PostAsync("/courses/50/sync", Json(Body));
        using var health = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("database_unavailable", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    private sealed class CapturingStore : IAcademicDataStore
    {
        public NormalizedCourseData? Saved { get; private set; }
        public StoreResult Result { get; init; } = new(2, 0, 0);

        public Task<StoreResult> SaveAsync(NormalizedCourseData data, CancellationToken cancellationToken)
        {
            Saved = data;
            return Task.FromResult(Result);
        }
    }
}
