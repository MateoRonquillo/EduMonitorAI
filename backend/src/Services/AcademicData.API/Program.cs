using EduMonitorAI.AcademicData.Persistence;
using EduMonitorAI.AcademicData.Sync;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAcademicStore(builder.Configuration);
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

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

app.MapPost("/courses/{courseId:int}/sync", async (
        int courseId,
        SyncCourseRequest request,
        IAcademicDataStore store,
        ILogger<Program> logger,
        CancellationToken cancellationToken) =>
    {
        if (request.Course is null || request.Course.MoodleId != courseId)
        {
            return Results.Problem(
                title: "Curso inconsistente",
                detail: "El curso de la ruta debe coincidir con el curso del cuerpo.",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["code"] = "course_mismatch" });
        }

        var normalization = GradeNormalizer.Normalize(request);
        if (normalization.Data is null)
        {
            return Results.Problem(
                title: "Curso inválido",
                detail: "El curso no tiene identificador o nombre válidos.",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["code"] = "invalid_course", ["rejected"] = normalization.Rejected });
        }

        try
        {
            var stored = await store.SaveAsync(normalization.Data, cancellationToken);
            var summary = new SyncSummary(
                courseId,
                stored.Inserted,
                stored.Updated,
                stored.Unchanged + normalization.DuplicatesInPayload,
                normalization.Rejected.Count,
                normalization.Data.Grades.Count(grade => grade.Value is null),
                normalization.Rejected);
            logger.LogInformation(
                "Sync of course {CourseId}: {Inserted} inserted, {Updated} updated, {Duplicates} duplicates, {Rejected} rejected",
                courseId, summary.Inserted, summary.Updated, summary.DuplicatesSkipped, summary.Rejected);
            return Results.Ok(summary);
        }
        catch (AcademicStoreUnavailableException exception)
        {
            logger.LogWarning(exception.InnerException, "Academic database unavailable during sync of course {CourseId}", courseId);
            return Results.Problem(
                title: "Base de datos no disponible",
                detail: "No se pudo conectar con la base auxiliar; no se modificó la información almacenada.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                extensions: new Dictionary<string, object?> { ["code"] = "database_unavailable" });
        }
    })
    .WithName("SyncCourseAcademicData")
    .WithSummary("Normalizar y almacenar los datos académicos de un curso")
    .WithDescription("Valida escalas y valores, representa las calificaciones ausentes como nulas, rechaza registros inválidos y actualiza sin duplicar. No escribe en Moodle.")
    .Produces<SyncSummary>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

app.Run();

public partial class Program { }
