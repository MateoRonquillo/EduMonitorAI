using System.Text.Json;
using EduMonitorAI.Integration;

namespace AcademicData.API.Application;

public sealed class AnalysisRules
{
    public decimal LowGradePercent { get; set; } = 70;
    public decimal ReinforcementPercent { get; set; } = 50;
    public int MinimumObservations { get; set; } = 3;
    public void Validate()
    {
        if (LowGradePercent is < 0 or > 100 || ReinforcementPercent is < 0 or > 100 || MinimumObservations < 1)
            throw new InvalidOperationException("Invalid Analysis configuration");
    }
}
public sealed record Distribution(int Below7, int From7To89, int From9To10);
public sealed record Statistics(decimal? MeanPercent, decimal? MinimumPercent, decimal? MaximumPercent,
    int Observations, int Excluded, Distribution Distribution, string Scale = "0–10")
{
    public decimal? MeanGrade => MeanPercent / 10;
    public decimal? MinimumGrade => MinimumPercent / 10;
    public decimal? MaximumGrade => MaximumPercent / 10;
}
public sealed record AcademicAlert(string Rule, long StudentId, long ItemId, string Activity, decimal? Value,
    decimal Minimum, decimal Maximum, decimal ThresholdPercent, DateTimeOffset? DueAt, DateTimeOffset EvidenceAt, string Evidence);
public sealed record ActivityResult(long ItemId, string Name, string Module, long? Instance, Statistics Statistics);
public sealed record ContentFinding(long SectionId, string Content, string MappingSource, long[] ActivityIds,
    int Students, int BelowThreshold, decimal BelowThresholdPercent, Statistics Statistics);
public sealed record AnalysisReport(long CourseId, string CourseName, string Source, DateTimeOffset SyncedAt,
    int Students, int Activities, Statistics Statistics, Dictionary<string, int> States,
    ActivityResult[] ActivityResults, AcademicAlert[] Alerts, ContentFinding[] Reinforcement,
    int UnmappedActivities, AnalysisRules Rules, string Method);
public sealed record StudentProfile(long CourseId, Student Student, string Source, DateTimeOffset SyncedAt,
    Grade[] Grades, Statistics Statistics, AcademicAlert[] Alerts);

public sealed class AcademicAnalysis(AnalysisRules rules)
{
    public static bool IsActivity(Grade g) => !string.IsNullOrWhiteSpace(g.Module);
    public static decimal? Normalize(Grade g) => g.Value.HasValue && g.State == "graded" &&
        g.ScaleId is null or 0 && g.Maximum > g.Minimum && g.Value >= g.Minimum && g.Value <= g.Maximum
        ? (g.Value.Value - g.Minimum) * 100 / (g.Maximum - g.Minimum) : null;

    public static Statistics Calculate(IEnumerable<Grade> grades)
    {
        var all = grades.Where(IsActivity).ToArray();
        var values = all.Select(Normalize).Where(n => n.HasValue).Select(n => n!.Value).ToArray();
        decimal? Round(decimal? value) => value.HasValue ? Math.Round(value.Value, 2, MidpointRounding.AwayFromZero) : null;
        return new(Round(values.Length == 0 ? null : values.Average()), Round(values.Length == 0 ? null : values.Min()),
            Round(values.Length == 0 ? null : values.Max()), values.Length, all.Length - values.Length,
            new(values.Count(n => n < 70), values.Count(n => n >= 70 && n < 90), values.Count(n => n >= 90)));
    }

    public AnalysisReport Build(Snapshot snapshot)
    {
        var grades = snapshot.Grades.Where(IsActivity).ToArray();
        var groups = grades.GroupBy(g => g.ItemId).ToArray();
        var activities = groups.Select(g => new ActivityResult(g.Key, g.First().Name, g.First().Module!,
            g.First().Instance, Calculate(g))).ToArray();
        var mapped = new HashSet<long>();
        var findings = new List<ContentFinding>();
        var visibleActivities = new HashSet<long>();
        if (snapshot.Activities.ValueKind == JsonValueKind.Array)
        {
            foreach (var section in snapshot.Activities.EnumerateArray())
            {
                if (!section.TryGetProperty("modules", out var modules) || modules.ValueKind != JsonValueKind.Array) continue;
                var sectionGrades = new List<Grade>();
                foreach (var module in modules.EnumerateArray())
                {
                    if (module.TryGetProperty("id", out var cmid)) visibleActivities.Add(cmid.GetInt64());
                    if (!module.TryGetProperty("instance", out var instance) || !module.TryGetProperty("modname", out var modname)) continue;
                    sectionGrades.AddRange(grades.Where(g => g.Instance == instance.GetInt64() && g.Module == modname.GetString()));
                }
                // An explicit Moodle section label supplies the verified mapping. Never guess from a grade title.
                if (!section.TryGetProperty("name", out var name) || string.IsNullOrWhiteSpace(name.GetString()) ||
                    !section.TryGetProperty("id", out var sectionId)) continue;
                foreach (var grade in sectionGrades) mapped.Add(grade.ItemId);
                var perStudent = sectionGrades.Where(g => Normalize(g).HasValue).GroupBy(g => g.StudentId)
                    .Select(g => g.Average(x => Normalize(x)!.Value)).ToArray();
                var low = perStudent.Count(v => v < rules.LowGradePercent);
                var percent = perStudent.Length == 0 ? 0 : 100m * low / perStudent.Length;
                if (perStudent.Length >= rules.MinimumObservations && percent >= rules.ReinforcementPercent)
                    findings.Add(new(sectionId.GetInt64(), name.GetString()!, "Moodle: sección → módulo → ítem de calificación",
                        sectionGrades.Select(g => g.ItemId).Distinct().ToArray(), perStudent.Length, low,
                        Math.Round(percent, 2), Calculate(sectionGrades)));
            }
        }
        return new(snapshot.CourseId, snapshot.CourseName, snapshot.Source, snapshot.SyncedAt, snapshot.Students.Length,
            visibleActivities.Count, Calculate(grades), new[] { "graded", "pending", "not_submitted", "unknown" }
                .ToDictionary(state => state, state => grades.Count(g => g.State == state)), activities,
            Alerts(snapshot, grades), findings.ToArray(), groups.Count(g => !mapped.Contains(g.Key)), rules,
            "Media aritmética de notas de actividades normalizadas; excluye totales, categorías, escalas cualitativas, rangos inválidos y notas ausentes. No es la nota oficial ponderada de Moodle.");
    }

    public StudentProfile Profile(Snapshot snapshot, long studentId)
    {
        var student = snapshot.Students.SingleOrDefault(s => s.Id == studentId) ?? throw new ApiFailure(404, "student_not_in_course");
        var grades = snapshot.Grades.Where(g => g.StudentId == studentId && IsActivity(g)).ToArray();
        return new(snapshot.CourseId, student, snapshot.Source, snapshot.SyncedAt, grades, Calculate(grades), Alerts(snapshot, grades));
    }

    private AcademicAlert[] Alerts(Snapshot snapshot, IEnumerable<Grade> grades) => grades.SelectMany(g =>
    {
        var normalized = Normalize(g);
        string? rule = normalized < rules.LowGradePercent ? "low_grade" :
            g.State == "not_submitted" && g.DueAt.HasValue && g.DueAt < snapshot.SyncedAt ? "overdue_submission" : null;
        return rule is null ? Array.Empty<AcademicAlert>() : [new AcademicAlert(rule, g.StudentId, g.ItemId, g.Name,
            g.Value, g.Minimum, g.Maximum, rules.LowGradePercent, g.DueAt, snapshot.SyncedAt,
            rule == "low_grade" ? $"Nota en escala 0–10: {Math.Round(normalized!.Value / 10, 2)} < {rules.LowGradePercent / 10}." :
                "Moodle confirma ausencia de entrega y fecha límite anterior a la sincronización; requiere revisión docente.")];
    }).ToArray();
}
