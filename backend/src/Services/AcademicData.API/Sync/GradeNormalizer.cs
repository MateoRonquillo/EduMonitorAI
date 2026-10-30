using System.Globalization;
using System.Text.Json;

namespace EduMonitorAI.AcademicData.Sync;

public static class GradeNormalizer
{
    public static NormalizationResult Normalize(SyncCourseRequest request)
    {
        var rejected = new List<RejectedRecord>();
        var duplicates = 0;

        var course = NormalizeCourse(request.Course, rejected);
        if (course is null)
        {
            return new NormalizationResult(null, rejected, duplicates);
        }

        var students = new Dictionary<int, NormalizedStudent>();
        foreach (var student in request.Students ?? [])
        {
            if (student.MoodleId <= 0 || string.IsNullOrWhiteSpace(student.FullName))
            {
                rejected.Add(new RejectedRecord("student", student.MoodleId.ToString(CultureInfo.InvariantCulture), "invalid_student"));
            }
            else if (!students.TryAdd(student.MoodleId, new NormalizedStudent(student.MoodleId, student.FullName.Trim())))
            {
                duplicates++;
            }
        }

        var activities = new Dictionary<int, NormalizedActivity>();
        foreach (var activity in request.Activities ?? [])
        {
            var reference = activity.MoodleId.ToString(CultureInfo.InvariantCulture);
            if (activity.MoodleId <= 0 || string.IsNullOrWhiteSpace(activity.Name))
            {
                rejected.Add(new RejectedRecord("activity", reference, "invalid_activity"));
            }
            else if (activity.MaxScale <= 0)
            {
                rejected.Add(new RejectedRecord("activity", reference, "invalid_scale"));
            }
            else if (activity.WeightPercent is < 0 or > 100)
            {
                rejected.Add(new RejectedRecord("activity", reference, "invalid_weight"));
            }
            else if (!activities.TryAdd(activity.MoodleId, new NormalizedActivity(
                activity.MoodleId,
                activity.Name.Trim(),
                string.IsNullOrWhiteSpace(activity.Category) ? null : activity.Category.Trim(),
                activity.WeightPercent,
                activity.MaxScale)))
            {
                duplicates++;
            }
        }

        var grades = new Dictionary<(int Activity, int Student), NormalizedGrade>();
        foreach (var grade in request.Grades ?? [])
        {
            var reference = $"{grade.ActivityMoodleId}/{grade.StudentMoodleId}";
            if (!activities.TryGetValue(grade.ActivityMoodleId, out var activity))
            {
                rejected.Add(new RejectedRecord("grade", reference, "unknown_activity"));
                continue;
            }

            if (!students.ContainsKey(grade.StudentMoodleId))
            {
                rejected.Add(new RejectedRecord("grade", reference, "unknown_student"));
                continue;
            }

            if (!TryReadValue(grade.Value, out var value, out var failure))
            {
                rejected.Add(new RejectedRecord("grade", reference, failure));
                continue;
            }

            if (value.HasValue && (value.Value < 0 || value.Value > activity.MaxScale))
            {
                rejected.Add(new RejectedRecord("grade", reference, "out_of_scale"));
                continue;
            }

            if (!grades.TryAdd((grade.ActivityMoodleId, grade.StudentMoodleId),
                    new NormalizedGrade(grade.ActivityMoodleId, grade.StudentMoodleId, value)))
            {
                duplicates++;
            }
        }

        var data = new NormalizedCourseData(
            course,
            students.Values.ToArray(),
            activities.Values.ToArray(),
            grades.Values.ToArray());
        return new NormalizationResult(data, rejected, duplicates);
    }

    private static NormalizedCourse? NormalizeCourse(SyncCourse? course, List<RejectedRecord> rejected)
    {
        if (course is null || course.MoodleId <= 0 || string.IsNullOrWhiteSpace(course.Name))
        {
            rejected.Add(new RejectedRecord("course", course?.MoodleId.ToString(CultureInfo.InvariantCulture) ?? "-", "invalid_course"));
            return null;
        }

        var name = course.Name.Trim();
        return new NormalizedCourse(course.MoodleId, string.IsNullOrWhiteSpace(course.ShortName) ? name : course.ShortName.Trim(), name);
    }

    /// <summary>Un valor ausente produce <c>null</c> (sin calificar), nunca 0.</summary>
    private static bool TryReadValue(JsonElement? element, out decimal? value, out string failure)
    {
        value = null;
        failure = string.Empty;

        if (element is null)
        {
            return true;
        }

        switch (element.Value.ValueKind)
        {
            case JsonValueKind.Undefined:
            case JsonValueKind.Null:
                return true;
            case JsonValueKind.Number:
                if (element.Value.TryGetDecimal(out var number))
                {
                    value = number;
                    return true;
                }

                break;
            case JsonValueKind.String:
                var text = element.Value.GetString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    return true;
                }

                if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    value = parsed;
                    return true;
                }

                break;
        }

        failure = "non_numeric_value";
        return false;
    }
}
