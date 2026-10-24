extern alias AccessApi;

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace EduMonitorAI.IntegrationTests;

public sealed class CourseAuthorizationTests
{
    private const string Issuer = "https://issuer.test.local";
    private const string Audience = "edumonitor-api-tests";
    private const string CourseId = "course-42";
    private const string SigningKey = "integration-test-key-with-at-least-32-bytes";

    [Fact]
    public async Task CourseAccess_ReturnsNoContent_WhenTeacherHasCourseGrant()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var request = CreateCourseRequest(CreateToken("docente", CourseId));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task CourseAccess_ReturnsForbidden_WhenTeacherDoesNotHaveCourseGrant()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var request = CreateCourseRequest(CreateToken("docente", "course-99"));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("course_forbidden", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task CourseAccess_ReturnsUnauthorized_WhenIdentityIsStudent()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var request = CreateCourseRequest(CreateToken("estudiante", CourseId));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("unauthorized", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task CourseAccess_ReturnsUnauthorized_WhenThereIsNoSession()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/courses/{CourseId}/access");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("unauthorized", body.RootElement.GetProperty("code").GetString());
    }

    private static WebApplicationFactory<AccessApi::Program> CreateFactory() =>
        new WebApplicationFactory<AccessApi::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Identity:Authority"] = Issuer,
                        ["Identity:Audience"] = Audience
                    }));

                builder.ConfigureTestServices(services =>
                    services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                    {
                        options.Authority = null;
                        options.MetadataAddress = null;
                        options.ConfigurationManager = null;
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidIssuer = Issuer,
                            ValidateAudience = true,
                            ValidAudience = Audience,
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                            RequireSignedTokens = true,
                            RequireExpirationTime = true,
                            ValidateLifetime = true,
                            ClockSkew = TimeSpan.Zero,
                            NameClaimType = "name",
                            RoleClaimType = "role"
                        };
                    }));
            });

    private static HttpRequestMessage CreateCourseRequest(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/courses/{CourseId}/access");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static string CreateToken(string role, params string[] courseGrants)
    {
        var now = DateTimeOffset.UtcNow;
        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            alg = "HS256",
            typ = "JWT"
        }));
        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = Issuer,
            aud = Audience,
            sub = "subject-123",
            teacher_id = "teacher-123",
            role,
            course_access = courseGrants,
            nbf = now.AddMinutes(-1).ToUnixTimeSeconds(),
            exp = now.AddMinutes(5).ToUnixTimeSeconds()
        }));
        var signingInput = $"{header}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SigningKey));
        var signature = Base64UrlEncode(hmac.ComputeHash(Encoding.ASCII.GetBytes(signingInput)));

        return $"{signingInput}.{signature}";
    }

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
