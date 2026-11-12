using System.Text.Json;
using EduMonitorAI.Integration;
using MySqlConnector;
namespace AcademicData.API.Infrastructure;

public interface IAcademicStore
{
    Task RecordFailure(long course, long actor, string code, CancellationToken ct);
    Task Save(Snapshot snapshot, long actor, CancellationToken ct);
    Task<Snapshot?> Read(long course, CancellationToken ct);
}
public sealed class AcademicStore(IConfiguration config) : IAcademicStore
{
    private async Task<MySqlConnection> Open(CancellationToken ct)
    {
        var connectionString = config.GetConnectionString("Academic");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            if (string.IsNullOrWhiteSpace(config["Database:Host"]) || string.IsNullOrWhiteSpace(config["Database:Password"]))
                throw new ApiFailure(503, "database_configuration_required");
            connectionString = new MySqlConnectionStringBuilder
            {
                Server = config["Database:Host"], Database = "academic", UserID = "edumonitor",
                Password = config["Database:Password"]
            }.ConnectionString;
        }
        var connection = new MySqlConnection(connectionString);
        try { await connection.OpenAsync(ct); return connection; }
        catch (MySqlException) { await connection.DisposeAsync(); throw new ApiFailure(503, "database_unavailable"); }
    }
    public async Task RecordFailure(long course, long actor, string code, CancellationToken ct)
    {
        await using var db = await Open(ct);
        await using var cmd = new MySqlCommand("INSERT INTO sync_runs(course_id,actor_id,finished_at,status,error_code) VALUES(@course,@actor,UTC_TIMESTAMP(6),'failed',@code)", db);
        cmd.Parameters.AddWithValue("@course", course); cmd.Parameters.AddWithValue("@actor", actor);
        cmd.Parameters.AddWithValue("@code", code); await cmd.ExecuteNonQueryAsync(ct);
    }
    public async Task Save(Snapshot snapshot, long actor, CancellationToken ct)
    {
        if (snapshot.CourseId <= 0 || snapshot.Students.Any(s => s.Id <= 0) ||
            snapshot.Students.Select(s => s.Id).Distinct().Count() != snapshot.Students.Length ||
            snapshot.Grades.Select(g => (g.StudentId, g.ItemId)).Distinct().Count() != snapshot.Grades.Length ||
            snapshot.Grades.Any(g => !snapshot.Students.Any(s => s.Id == g.StudentId) || g.Minimum > g.Maximum ||
             g.Value.HasValue && (g.Value < g.Minimum || g.Value > g.Maximum) ||
             g.State is not ("graded" or "pending" or "not_submitted" or "unknown") ||
             (g.State == "graded") != g.Value.HasValue))
            throw new ApiFailure(502, "invalid_academic_data");
        await using var db = await Open(ct);
        await using var transaction = await db.BeginTransactionAsync(ct);
        async Task Execute(string sql, params (string, object?)[] values)
        {
            await using var command = new MySqlCommand(sql, db, transaction);
            foreach (var (key, value) in values) command.Parameters.AddWithValue(key, value ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(ct);
        }
        // Course row serializes concurrent replacements. The whole snapshot commits atomically.
        await Execute("INSERT INTO courses(id,name,synced_at,source,activities) VALUES(@id,@name,@at,@source,@activities) ON DUPLICATE KEY UPDATE name=@name,synced_at=@at,source=@source,activities=@activities",
         ("@id", snapshot.CourseId), ("@name", snapshot.CourseName), ("@at", snapshot.SyncedAt.UtcDateTime), ("@source", snapshot.Source), ("@activities", snapshot.Activities.GetRawText()));
        await Execute("DELETE FROM grades WHERE course_id=@id", ("@id", snapshot.CourseId));
        await Execute("DELETE FROM students WHERE course_id=@id", ("@id", snapshot.CourseId));
        foreach (var s in snapshot.Students)
            await Execute("INSERT INTO students(course_id,id,name) VALUES(@course,@id,@name)", ("@course", snapshot.CourseId), ("@id", s.Id), ("@name", s.Name));
        foreach (var g in snapshot.Grades)
            await Execute("INSERT INTO grades(course_id,student_id,item_id,payload) VALUES(@course,@student,@item,@payload)",
             ("@course", snapshot.CourseId), ("@student", g.StudentId), ("@item", g.ItemId), ("@payload", JsonSerializer.Serialize(g)));
        await Execute("INSERT INTO sync_runs(course_id,actor_id,finished_at,status) VALUES(@course,@actor,UTC_TIMESTAMP(6),'success')", ("@course", snapshot.CourseId), ("@actor", actor));
        await transaction.CommitAsync(ct);
    }
    public async Task<Snapshot?> Read(long course, CancellationToken ct)
    {
        await using var db = await Open(ct);
        // Repeatable-read transaction keeps metadata, students and grades from the same version.
        await using var tx = await db.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        await using var cmd = new MySqlCommand("SELECT name,synced_at,source,activities FROM courses WHERE id=@id", db, tx);
        cmd.Parameters.AddWithValue("@id", course);
        string name, source; DateTimeOffset at; JsonElement activities;
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            if (!await r.ReadAsync(ct)) return null;
            name = r.GetString(0); at = new DateTimeOffset(DateTime.SpecifyKind(r.GetDateTime(1), DateTimeKind.Utc));
            source = r.GetString(2); activities = JsonDocument.Parse(r.GetString(3)).RootElement.Clone();
        }
        cmd.CommandText = "SELECT id,name FROM students WHERE course_id=@id ORDER BY id";
        var students = new List<Student>();
        await using (var r = await cmd.ExecuteReaderAsync(ct)) while (await r.ReadAsync(ct)) students.Add(new Student(r.GetInt64(0), r.GetString(1)));
        cmd.CommandText = "SELECT payload FROM grades WHERE course_id=@id ORDER BY student_id,item_id";
        var grades = new List<Grade>();
        await using (var r = await cmd.ExecuteReaderAsync(ct)) while (await r.ReadAsync(ct)) grades.Add(JsonSerializer.Deserialize<Grade>(r.GetString(0))!);
        await tx.CommitAsync(ct);
        return new Snapshot(course, name, at, source, students.ToArray(), grades.ToArray(), activities);
    }
}
