using System.Text.Json;
using EduMonitorAI.AcademicData.Sync;
using Xunit;

namespace EduMonitorAI.UnitTests;

public class GradeNormalizerTests
{
    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();

    private static SyncCourseRequest Request(params SyncGrade[] grades) => new(
        new SyncCourse(50, "CURSO-A", "Curso A"),
        [new SyncStudent(201, "Estudiante Uno"), new SyncStudent(202, "Estudiante Dos")],
        [new SyncActivity(10, "Examen parcial 1", "Examen", 40, 10m)],
        grades);

    [Fact]
    public void Cp53_ValidGrade_KeepsValueScaleAndIdentifiers()
    {
        var result = GradeNormalizer.Normalize(Request(new SyncGrade(10, 201, Json("8"))));

        Assert.Empty(result.Rejected);
        var grade = Assert.Single(result.Data!.Grades);
        Assert.Equal(8m, grade.Value);
        Assert.Equal(10, grade.ActivityMoodleId);
        Assert.Equal(201, grade.StudentMoodleId);
        Assert.Equal(10m, Assert.Single(result.Data.Activities).MaxScale);
        Assert.Equal(50, result.Data.Course.MoodleId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    public void Cp54_MissingGrade_IsNullNotZero(string? raw)
    {
        JsonElement? value = raw is null ? null : Json(raw);

        var result = GradeNormalizer.Normalize(Request(new SyncGrade(10, 202, value)));

        Assert.Empty(result.Rejected);
        var grade = Assert.Single(result.Data!.Grades);
        Assert.Null(grade.Value);
    }

    [Fact]
    public void Cp54_ExplicitZero_StaysZero()
    {
        var result = GradeNormalizer.Normalize(Request(new SyncGrade(10, 201, Json("0"))));

        Assert.Equal(0m, Assert.Single(result.Data!.Grades).Value);
    }

    [Fact]
    public void Cp55_OutOfScaleAndNonNumeric_AreRejectedWithoutAffectingValidRecords()
    {
        var result = GradeNormalizer.Normalize(Request(
            new SyncGrade(10, 201, Json("12")),
            new SyncGrade(10, 202, Json("\"abc\"")),
            new SyncGrade(10, 201, Json("7.5"))));

        Assert.Equal(2, result.Rejected.Count);
        Assert.Contains(result.Rejected, r => r.Reason == "out_of_scale");
        Assert.Contains(result.Rejected, r => r.Reason == "non_numeric_value");
        var valid = Assert.Single(result.Data!.Grades);
        Assert.Equal(7.5m, valid.Value);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("10.01")]
    [InlineData("true")]
    [InlineData("[]")]
    public void InvalidValues_AreRejected(string raw)
    {
        var result = GradeNormalizer.Normalize(Request(new SyncGrade(10, 201, Json(raw))));

        Assert.Single(result.Rejected);
        Assert.Empty(result.Data!.Grades);
    }

    [Fact]
    public void ValueEqualToMaxScale_IsAccepted()
    {
        var result = GradeNormalizer.Normalize(Request(new SyncGrade(10, 201, Json("\"10\""))));

        Assert.Equal(10m, Assert.Single(result.Data!.Grades).Value);
    }

    [Fact]
    public void Cp58_DuplicateGradeInSamePayload_IsKeptOnceAndCounted()
    {
        var result = GradeNormalizer.Normalize(Request(
            new SyncGrade(10, 201, Json("8")),
            new SyncGrade(10, 201, Json("8"))));

        Assert.Single(result.Data!.Grades);
        Assert.Equal(1, result.DuplicatesInPayload);
    }

    [Fact]
    public void GradeForUnknownActivityOrStudent_IsRejected()
    {
        var result = GradeNormalizer.Normalize(Request(
            new SyncGrade(99, 201, Json("5")),
            new SyncGrade(10, 999, Json("5"))));

        Assert.Equal(["unknown_activity", "unknown_student"], result.Rejected.Select(r => r.Reason).ToArray());
        Assert.Empty(result.Data!.Grades);
    }

    [Fact]
    public void InvalidActivities_AreRejected_AndTheirGradesAlongWithThem()
    {
        var request = new SyncCourseRequest(
            new SyncCourse(50, null, "Curso A"),
            [new SyncStudent(201, "Estudiante Uno")],
            [
                new SyncActivity(10, "Sin escala", null, null, 0m),
                new SyncActivity(11, "Peso inválido", null, 130m, 10m),
                new SyncActivity(12, " ", null, null, 10m),
                new SyncActivity(13, "Aprendizaje autónomo", "  ", 30m, 20m)
            ],
            [new SyncGrade(10, 201, Json("5")), new SyncGrade(13, 201, Json("18"))]);

        var result = GradeNormalizer.Normalize(request);

        Assert.Equal(["invalid_scale", "invalid_weight", "invalid_activity", "unknown_activity"], result.Rejected.Select(r => r.Reason).ToArray());
        var activity = Assert.Single(result.Data!.Activities);
        Assert.Null(activity.Category);
        Assert.Equal("Curso A", result.Data.Course.ShortName);
        Assert.Equal(18m, Assert.Single(result.Data.Grades).Value);
    }

    [Fact]
    public void InvalidCourse_ProducesNoData()
    {
        var request = new SyncCourseRequest(new SyncCourse(0, null, ""), null, null, null);

        var result = GradeNormalizer.Normalize(request);

        Assert.Null(result.Data);
        Assert.Equal("invalid_course", Assert.Single(result.Rejected).Reason);
    }
}
