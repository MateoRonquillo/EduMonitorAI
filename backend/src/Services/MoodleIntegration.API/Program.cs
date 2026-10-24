using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
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
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(new
                {
                    type = "https://httpstatuses.com/403",
                    title = "Acceso denegado",
                    status = StatusCodes.Status403Forbidden,
                    code = "course_forbidden",
                    detail = "La identidad autenticada no tiene permisos efectivos sobre el curso solicitado."
                });
            }
        };
    });

builder.Services.Configure<MoodleOptions>(builder.Configuration.GetSection("Moodle"));
builder.Services
    .AddHttpClient<MoodleWebServiceClient>(client => client.Timeout = TimeSpan.FromSeconds(30))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IAuthorizationHandler, CourseAccessHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Teacher", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("role", "docente");
    });
    options.AddPolicy("CourseAccess", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("role", "docente");
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

app.MapGet("/courses", async (
        ClaimsPrincipal user,
        MoodleWebServiceClient moodle,
        ILogger<Program> logger,
        CancellationToken cancellationToken) =>
    {
        var allowedCourseIds = user.FindAll("course_access")
            .Select(claim => int.TryParse(claim.Value, out var courseId) ? courseId : (int?)null)
            .Where(courseId => courseId.HasValue)
            .Select(courseId => courseId!.Value)
            .ToHashSet();

        try
        {
            var courses = await moodle.GetCoursesAsync(cancellationToken);
            var response = courses
                .Where(course => allowedCourseIds.Contains(course.Id))
                .Select(MoodleResponseMapper.Map)
                .ToArray();
            return Results.Ok(response);
        }
        catch (MoodleServiceException exception)
        {
            logger.LogWarning("Moodle course query failed with error code {ErrorCode}", exception.ErrorCode);
            return MoodleError(exception);
        }
        catch (HttpRequestException)
        {
            logger.LogWarning("Moodle course query could not reach the configured service");
            return MoodleUnavailable();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Moodle course query timed out");
            return MoodleUnavailable();
        }
    })
    .RequireAuthorization("Teacher")
    .WithName("GetPermittedCourses")
    .WithSummary("Consultar los cursos permitidos del docente")
    .WithDescription("Consulta Moodle y filtra la respuesta por los grants course_access del token docente validado.")
    .Produces<IReadOnlyList<CourseResponse>>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status502BadGateway)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

app.MapGet("/courses/{courseId:int}/participants", async (
        int courseId,
        MoodleWebServiceClient moodle,
        ILogger<Program> logger,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var participants = await moodle.GetEnrolledUsersAsync(courseId, cancellationToken);
            return Results.Ok(participants.Select(MoodleResponseMapper.Map).ToArray());
        }
        catch (MoodleServiceException exception)
        {
            logger.LogWarning("Moodle participant query for course {CourseId} failed with error code {ErrorCode}", courseId, exception.ErrorCode);
            return MoodleError(exception);
        }
        catch (HttpRequestException)
        {
            logger.LogWarning("Moodle participant query for course {CourseId} could not reach the configured service", courseId);
            return MoodleUnavailable();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Moodle participant query for course {CourseId} timed out", courseId);
            return MoodleUnavailable();
        }
    })
    .RequireAuthorization("CourseAccess")
    .WithName("GetPermittedCourseParticipants")
    .WithSummary("Consultar participantes activos de un curso permitido")
    .WithDescription("Consulta participantes activos en Moodle. La policy exige un grant course_access para el ID solicitado.")
    .Produces<IReadOnlyList<CourseParticipantResponse>>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status403Forbidden)
    .ProducesProblem(StatusCodes.Status502BadGateway)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

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

static IResult MoodleError(MoodleServiceException exception) => Results.Problem(
    statusCode: (int)exception.UpstreamStatus == StatusCodes.StatusServiceUnavailable
        ? StatusCodes.Status503ServiceUnavailable
        : StatusCodes.Status502BadGateway,
    type: (int)exception.UpstreamStatus == StatusCodes.StatusServiceUnavailable
        ? "https://httpstatuses.com/503"
        : "https://httpstatuses.com/502",
    title: (int)exception.UpstreamStatus == StatusCodes.StatusServiceUnavailable
        ? "Moodle no está disponible"
        : "Error al consultar Moodle",
    extensions: new Dictionary<string, object?> { ["code"] = exception.ErrorCode });

static IResult MoodleUnavailable() => Results.Problem(
    statusCode: StatusCodes.Status503ServiceUnavailable,
    type: "https://httpstatuses.com/503",
    title: "Moodle no está disponible",
    extensions: new Dictionary<string, object?> { ["code"] = "moodle_unavailable" });

public partial class Program { }
