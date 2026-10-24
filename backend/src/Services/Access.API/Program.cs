using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var identityOptions = builder.Configuration.GetSection("Identity");
var authority = identityOptions["Authority"];
var audience = identityOptions["Audience"];

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.Authority = authority;
        options.Audience = audience;
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authority,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var principal = context.Principal;
                var hasSubject = !string.IsNullOrWhiteSpace(principal?.FindFirst("sub")?.Value);
                var hasTeacherId = !string.IsNullOrWhiteSpace(principal?.FindFirst("teacher_id")?.Value);
                var isTeacher = principal?.FindAll("role").Any(claim => claim.Value == "docente") == true;

                if (!hasSubject || !hasTeacherId || !isTeacher)
                {
                    context.Fail("El token no contiene una identidad docente válida.");
                }

                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/problem+json";

                await context.Response.WriteAsJsonAsync(new
                {
                    type = "https://httpstatuses.com/401",
                    title = "No autenticado",
                    status = StatusCodes.Status401Unauthorized,
                    code = "unauthorized",
                    detail = "Se requiere un token válido de una identidad docente confiable."
                });
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/auth/session", (ClaimsPrincipal user) =>
    {
        var identity = new TeacherSessionIdentity(
            user.FindFirst("sub")!.Value,
            user.FindFirst("teacher_id")!.Value,
            user.FindFirst("name")?.Value,
            user.FindFirst("email")?.Value);

        return Results.Ok(identity);
    })
    .RequireAuthorization()
    .WithName("GetTeacherSession")
    .WithSummary("Validar la sesión de un docente")
    .WithDescription("Valida un token firmado por el emisor configurado y devuelve la identidad docente verificada.")
    .Produces<TeacherSessionIdentity>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status401Unauthorized);

app.MapGet("/health", async (HealthCheckService healthChecks, CancellationToken cancellationToken) =>
    {
        var report = await healthChecks.CheckHealthAsync(cancellationToken);
        var statusCode = report.Status == HealthStatus.Unhealthy
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status200OK;

        return Results.Text(report.Status.ToString(), "text/plain", statusCode: statusCode);
    })
    .WithName("GetHealth")
    .WithSummary("Comprobar la disponibilidad del proceso")
    .WithDescription("Comprobación de vida; no valida Moodle, persistencia ni permisos.")
    .Produces<string>(StatusCodes.Status200OK, "text/plain")
    .Produces<string>(StatusCodes.Status503ServiceUnavailable, "text/plain");

app.Run();

public sealed record TeacherSessionIdentity(string Subject, string TeacherId, string? Name, string? Email);

public partial class Program { }
