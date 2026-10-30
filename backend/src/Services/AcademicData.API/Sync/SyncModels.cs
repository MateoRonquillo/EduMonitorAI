using System.Text.Json;

namespace EduMonitorAI.AcademicData.Sync;

public sealed record SyncCourseRequest(
    SyncCourse Course,
    IReadOnlyList<SyncStudent>? Students,
    IReadOnlyList<SyncActivity>? Activities,
    IReadOnlyList<SyncGrade>? Grades);

public sealed record SyncCourse(int MoodleId, string? ShortName, string? Name);

public sealed record SyncStudent(int MoodleId, string? FullName);

public sealed record SyncActivity(
    int MoodleId,
    string? Name,
    string? Category,
    decimal? WeightPercent,
    decimal MaxScale);

/// <summary>
/// El valor llega sin tipar para poder rechazar entradas como "abc" sin romper la deserialización de la carga completa.
/// Un valor ausente (null o cadena vacía) representa una calificación "sin calificar", nunca cero.
/// </summary>
public sealed record SyncGrade(int ActivityMoodleId, int StudentMoodleId, JsonElement? Value);

public sealed record NormalizedCourse(int MoodleId, string ShortName, string Name);

public sealed record NormalizedStudent(int MoodleId, string FullName);

public sealed record NormalizedActivity(
    int MoodleId,
    string Name,
    string? Category,
    decimal? WeightPercent,
    decimal MaxScale);

/// <summary>Valor nulo significa "sin calificar".</summary>
public sealed record NormalizedGrade(int ActivityMoodleId, int StudentMoodleId, decimal? Value);

public sealed record NormalizedCourseData(
    NormalizedCourse Course,
    IReadOnlyList<NormalizedStudent> Students,
    IReadOnlyList<NormalizedActivity> Activities,
    IReadOnlyList<NormalizedGrade> Grades);

public sealed record RejectedRecord(string Kind, string Reference, string Reason);

public sealed record NormalizationResult(
    NormalizedCourseData? Data,
    IReadOnlyList<RejectedRecord> Rejected,
    int DuplicatesInPayload);

public sealed record StoreResult(int Inserted, int Updated, int Unchanged);

public sealed record SyncSummary(
    int CourseMoodleId,
    int Inserted,
    int Updated,
    int DuplicatesSkipped,
    int Rejected,
    int WithoutGrade,
    IReadOnlyList<RejectedRecord> RejectedRecords);
