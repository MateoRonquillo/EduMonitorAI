using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using EduMonitorAI.Access.Authorization;

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
                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    type = "https://httpstatuses.com/401",
                    title = "No autenticado",
                    status = StatusCodes.Status401Unauthorized,
                    code = "unauthorized",
                    detail = "Se requiere un token válido de una identidad docente confiable."
                }), context.HttpContext.RequestAborted);
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    type = "https://httpstatuses.com/403",
                    title = "Acceso denegado",
                    status = StatusCodes.Status403Forbidden,
                    code = "course_forbidden",
                    detail = "La identidad autenticada no tiene permisos efectivos sobre el curso solicitado."
                }), context.HttpContext.RequestAborted);
            }
        };
    });

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IAuthorizationHandler, CourseAccessHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(CourseAccessRequirement.PolicyName, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new CourseAccessRequirement());
    });
});
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

app.MapGet("/courses/{courseId}/access", () => Results.NoContent())
    .RequireAuthorization(CourseAccessRequirement.PolicyName)
    .WithName("CheckCourseAccess")
    .WithSummary("Comprobar el acceso efectivo a un curso")
    .WithDescription("Responde sin contenido si el token confiable incluye un permiso para el curso indicado.")
    .Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status403Forbidden);

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
