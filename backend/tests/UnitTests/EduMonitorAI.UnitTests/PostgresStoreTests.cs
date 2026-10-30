using System.Text.Json;
using EduMonitorAI.AcademicData.Persistence;
using EduMonitorAI.AcademicData.Sync;
using Npgsql;
using Xunit;

namespace EduMonitorAI.UnitTests;

/// <summary>Se omite si no se define EDUMONITOR_TEST_DB (cadena de conexión a una base PostgreSQL de pruebas).</summary>
public sealed class RequiresPostgresFactAttribute : FactAttribute
{
    public const string Variable = "EDUMONITOR_TEST_DB";

    public RequiresPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable)))
        {
            Skip = $"Defina {Variable} para ejecutar las pruebas contra PostgreSQL.";
        }
    }
}

/// <summary>Crea un esquema temporal por ejecución, de modo que nunca toca tablas existentes.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly string? _adminConnection = Environment.GetEnvironmentVariable(RequiresPostgresFactAttribute.Variable);
    private readonly string _schema = "test_" + Guid.NewGuid().ToString("N");

    public NpgsqlDataSource? DataSource { get; private set; }

    public PostgresAcademicDataStore? Store { get; private set; }

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(_adminConnection))
        {
            return;
        }

        await using (var admin = new NpgsqlDataSourceBuilder(_adminConnection).Build())
        {
            await admin.CreateCommand($"CREATE SCHEMA {_schema}").ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(_adminConnection) { SearchPath = _schema };
        DataSource = NpgsqlDataSource.Create(builder.ConnectionString);
        Store = new PostgresAcademicDataStore(DataSource);
    }

    public async Task DisposeAsync()
    {
        if (DataSource is null)
        {
            return;
        }

        await DataSource.DisposeAsync();
        await using var admin = new NpgsqlDataSourceBuilder(_adminConnection!).Build();
        await admin.CreateCommand($"DROP SCHEMA {_schema} CASCADE").ExecuteNonQueryAsync();
    }
}

public class PostgresStoreTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();

    private static NormalizedCourseData Course(int courseId, params (int Activity, int Student, string? Value)[] grades) =>
        GradeNormalizer.Normalize(new SyncCourseRequest(
            new SyncCourse(courseId, "CURSO", "Curso " + courseId),
            [new SyncStudent(201, "Estudiante Uno"), new SyncStudent(202, "Estudiante Dos")],
            [new SyncActivity(10, "Examen parcial 1", "Examen", 40, 10m), new SyncActivity(11, "APE", "APE", 30, 10m)],
            grades.Select(g => new SyncGrade(g.Activity, g.Student, g.Value is null ? null : Json(g.Value))).ToArray())).Data!;

    private async Task<long> CountAsync(string table) =>
        (long)(await fixture.DataSource!.CreateCommand($"SELECT count(*) FROM {table}").ExecuteScalarAsync())!;

    [RequiresPostgresFact]
    public async Task Cp51_EnsureSchema_CreatesAllTablesWithUniqueConstraints()
    {
        await fixture.Store!.EnsureSchemaAsync(CancellationToken.None);

        foreach (var table in DatabaseSchema.Tables)
        {
            Assert.Equal(0L, await CountAsync(table));
        }

        await using var command = fixture.DataSource!.CreateCommand(
            "SELECT count(*) FROM pg_constraint WHERE contype IN ('u', 'p') AND conrelid::regclass::text = ANY(@tables)");
        command.Parameters.AddWithValue("tables", DatabaseSchema.Tables);
        Assert.True((long)(await command.ExecuteScalarAsync())! >= 6);
    }

    [RequiresPostgresFact]
    public async Task Cp56_Cp57_Cp58_FirstSyncUpdateAndRepeat_DoNotDuplicate()
    {
        var store = fixture.Store!;

        var first = await store.SaveAsync(Course(77, (10, 201, "8"), (10, 202, null), (11, 201, "9.5")), CancellationToken.None);
        Assert.Equal(new StoreResult(3, 0, 0), first);
        Assert.Equal(3L, await CountAsync("calificacion"));

        var repeated = await store.SaveAsync(Course(77, (10, 201, "8"), (10, 202, null), (11, 201, "9.5")), CancellationToken.None);
        Assert.Equal(new StoreResult(0, 0, 3), repeated);
        Assert.Equal(3L, await CountAsync("calificacion"));

        var changed = await store.SaveAsync(Course(77, (10, 201, "6.5"), (10, 202, null), (11, 201, "9.5")), CancellationToken.None);
        Assert.Equal(new StoreResult(0, 1, 2), changed);
        Assert.Equal(3L, await CountAsync("calificacion"));

        var value = await fixture.DataSource!.CreateCommand(
            "SELECT valor FROM calificacion c JOIN estudiante e ON e.id = c.estudiante_id JOIN actividad a ON a.id = c.actividad_id WHERE e.moodle_id = 201 AND a.moodle_id = 10").ExecuteScalarAsync();
        Assert.Equal(6.5m, value);
    }

    [RequiresPostgresFact]
    public async Task Cp54_Cp59_MissingGrade_IsStoredAsNullNotZero_AndStructureMatchesPayload()
    {
        await fixture.Store!.SaveAsync(Course(88, (10, 201, "8"), (10, 202, null)), CancellationToken.None);

        var withoutGrade = await fixture.DataSource!.CreateCommand(
            "SELECT count(*) FROM calificacion c JOIN actividad a ON a.id = c.actividad_id JOIN curso k ON k.id = a.curso_id WHERE k.moodle_id = 88 AND c.valor IS NULL").ExecuteScalarAsync();
        var zeros = await fixture.DataSource.CreateCommand(
            "SELECT count(*) FROM calificacion WHERE valor = 0").ExecuteScalarAsync();
        var scale = await fixture.DataSource.CreateCommand(
            "SELECT escala_maxima FROM actividad a JOIN curso k ON k.id = a.curso_id WHERE k.moodle_id = 88 AND a.moodle_id = 10").ExecuteScalarAsync();

        Assert.Equal(1L, withoutGrade);
        Assert.Equal(0L, zeros);
        Assert.Equal(10m, scale);
    }

    [RequiresPostgresFact]
    public async Task SaveAsync_RollsBackEverything_WhenTheDatabaseRejectsARecord()
    {
        var bad = new NormalizedCourseData(
            new NormalizedCourse(99, "X", "Curso X"),
            [new NormalizedStudent(201, "Estudiante Uno")],
            [new NormalizedActivity(10, "Examen", null, null, 10m)],
            [new NormalizedGrade(10, 201, -5m)]);

        await Assert.ThrowsAnyAsync<PostgresException>(() => fixture.Store!.SaveAsync(bad, CancellationToken.None));

        var courses = await fixture.DataSource!.CreateCommand("SELECT count(*) FROM curso WHERE moodle_id = 99").ExecuteScalarAsync();
        Assert.Equal(0L, courses);
    }
}
