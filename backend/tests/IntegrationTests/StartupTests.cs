extern alias MoodleApi;
extern alias AcademicApi;
extern alias AccessApi;

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace EduMonitorAI.IntegrationTests;

public class MoodleIntegrationStartupTests : StartupTests<MoodleApi::Program> { }
public class AcademicDataStartupTests : StartupTests<AcademicApi::Program> { }
public class AccessStartupTests : StartupTests<AccessApi::Program> { }

public abstract class StartupTests<TProgram> where TProgram : class
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Health_ReturnsHealthy_WhenHostStarts(string environment)
    {
        using var factory = CreateFactory(environment);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OpenApi_DescribesHealth_InDevelopment()
    {
        using var factory = CreateFactory("Development");
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.StartsWith("3.", root.GetProperty("openapi").GetString());
        var operation = root.GetProperty("paths").GetProperty("/health").GetProperty("get");
        var responses = operation.GetProperty("responses");
        Assert.True(responses.GetProperty("200").GetProperty("content").TryGetProperty("text/plain", out _));
        Assert.True(responses.GetProperty("503").GetProperty("content").TryGetProperty("text/plain", out _));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task OpenApi_ReturnsNotFound_OutsideDevelopment(string environment)
    {
        using var factory = CreateFactory(environment);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static WebApplicationFactory<TProgram> CreateFactory(string environment) =>
        new WebApplicationFactory<TProgram>()
            .WithWebHostBuilder(builder => builder.UseEnvironment(environment));

    private static HttpClient CreateClient(WebApplicationFactory<TProgram> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
}
