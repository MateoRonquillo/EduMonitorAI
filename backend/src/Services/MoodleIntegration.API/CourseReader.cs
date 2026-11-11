using EduMonitorAI.Integration;
namespace MoodleIntegration.API.Application;

public sealed class CourseReader(MoodleClient moodle)
{
    public async Task<Snapshot> Read(string token, long courseId, CancellationToken ct)
    {
        try { return await ReadCore(token, courseId, ct); }
        catch (Exception error) when (error is System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentOutOfRangeException)
        { throw new ApiFailure(502, "moodle_invalid_academic_response"); }
    }
    private async Task<Snapshot> ReadCore(string token, long courseId, CancellationToken ct)
    {
        if (courseId <= 0) throw new ApiFailure(400, "invalid_course");
        var access = await moodle.Authorize(token, courseId, ct);
        var id = courseId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var participants = await moodle.Call(token, "core_enrol_get_enrolled_users", ct, ("courseid", id), ("options[0][name]", "onlyactive"), ("options[0][value]", "1"));
        var activities = await moodle.Call(token, "core_course_get_contents", ct, ("courseid", id));
        var data = await moodle.Call(token, "gradereport_user_get_grade_items", ct, ("courseid", id), ("userid", "0"));
        if (data.TryGetProperty("warnings", out var warnings) && warnings.GetArrayLength() > 0)
            throw new ApiFailure(502, "incomplete_moodle_response");
        var students = participants.EnumerateArray().Where(p => p.GetProperty("roles").EnumerateArray()
         .Any(r => r.GetProperty("shortname").GetString() == "student"))
         .Select(p => new Student(p.GetProperty("id").GetInt64(), p.GetProperty("fullname").GetString()!)).ToArray();
        var grades = new List<Grade>();
        var states = await moodle.Call(token, "local_edumonitor_get_states", ct, ("courseid", id));
        if (states.ValueKind != System.Text.Json.JsonValueKind.Array) throw new ApiFailure(502, "invalid_submission_evidence");
        foreach (var user in data.GetProperty("usergrades").EnumerateArray())
        {
            var studentId = user.GetProperty("userid").GetInt64();
            if (!students.Any(s => s.Id == studentId)) continue;
            foreach (var item in user.GetProperty("gradeitems").EnumerateArray())
            {
                decimal? value = item.TryGetProperty("graderaw", out var raw) && raw.ValueKind == System.Text.Json.JsonValueKind.Number ? raw.GetDecimal() : null;
                string? module = item.TryGetProperty("itemmodule", out var mod) ? mod.GetString() : null;
                long? instance = item.TryGetProperty("iteminstance", out var ins) && ins.ValueKind == System.Text.Json.JsonValueKind.Number ? ins.GetInt64() : null;
                long? scale = item.TryGetProperty("scaleid", out var sc) && sc.ValueKind == System.Text.Json.JsonValueKind.Number ? sc.GetInt64() : null;
                var evidence = states.EnumerateArray().FirstOrDefault(s => module == "assign" &&
                    s.GetProperty("studentId").GetInt64() == studentId && s.GetProperty("instance").GetInt64() == instance);
                var state = value.HasValue ? "graded" : evidence.ValueKind == System.Text.Json.JsonValueKind.Object
                    ? evidence.GetProperty("state").GetString()! : "unknown";
                if (state is not ("graded" or "pending" or "not_submitted" or "unknown"))
                    throw new ApiFailure(502, "invalid_submission_evidence");
                DateTimeOffset? Timestamp(string key) => evidence.ValueKind == System.Text.Json.JsonValueKind.Object &&
                    evidence.TryGetProperty(key, out var t) && t.GetInt64() > 0 ? DateTimeOffset.FromUnixTimeSeconds(t.GetInt64()) : null;
                grades.Add(new Grade(studentId, item.GetProperty("id").GetInt64(),
                 item.GetProperty("itemname").GetString() ?? "Total", module, instance, value,
                 item.GetProperty("grademin").GetDecimal(), item.GetProperty("grademax").GetDecimal(), scale,
                 state, Timestamp("dueAt"), Timestamp("submittedAt")));
            }
        }
        return new Snapshot(courseId, access.Courses.Single(c => c.Id == courseId).Name,
         DateTimeOffset.UtcNow, "Moodle", students, grades.ToArray(), activities);
    }
}
