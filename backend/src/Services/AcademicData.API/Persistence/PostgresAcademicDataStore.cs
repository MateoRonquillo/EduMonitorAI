using System.Net.Sockets;
using EduMonitorAI.AcademicData.Sync;
using Npgsql;

namespace EduMonitorAI.AcademicData.Persistence;

public sealed class PostgresAcademicDataStore(NpgsqlDataSource dataSource) : IAcademicDataStore
{
    private volatile bool _schemaReady;

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (_schemaReady)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await using var command = dataSource.CreateCommand(DatabaseSchema.CreateScript);
            await command.ExecuteNonQueryAsync(cancellationToken);
            _schemaReady = true;
            return 0;
        });
    }

    public async Task<StoreResult> SaveAsync(NormalizedCourseData data, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        return await RunAsync(async () =>
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            var courseId = await UpsertCourseAsync(connection, transaction, data.Course, cancellationToken);

            var studentIds = new Dictionary<int, int>();
            foreach (var student in data.Students)
            {
                var studentId = await UpsertStudentAsync(connection, transaction, student, cancellationToken);
                studentIds[student.MoodleId] = studentId;
                await EnrollAsync(connection, transaction, courseId, studentId, cancellationToken);
            }

            var activityIds = new Dictionary<int, int>();
            foreach (var activity in data.Activities)
            {
                activityIds[activity.MoodleId] = await UpsertActivityAsync(connection, transaction, courseId, activity, cancellationToken);
            }

            int inserted = 0, updated = 0, unchanged = 0;
            foreach (var grade in data.Grades)
            {
                var outcome = await UpsertGradeAsync(
                    connection,
                    transaction,
                    activityIds[grade.ActivityMoodleId],
                    studentIds[grade.StudentMoodleId],
                    grade.Value,
                    cancellationToken);

                switch (outcome)
                {
                    case true: inserted++; break;
                    case false: updated++; break;
                    default: unchanged++; break;
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return new StoreResult(inserted, updated, unchanged);
        });
    }

    private static async Task<int> UpsertCourseAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, NormalizedCourse course, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO curso (moodle_id, nombre_corto, nombre)
            VALUES (@moodle_id, @short, @name)
            ON CONFLICT (moodle_id) DO UPDATE
                SET nombre_corto = EXCLUDED.nombre_corto, nombre = EXCLUDED.nombre, sincronizado_en = now()
            RETURNING id
            """, connection, transaction);
        command.Parameters.AddWithValue("moodle_id", course.MoodleId);
        command.Parameters.AddWithValue("short", course.ShortName);
        command.Parameters.AddWithValue("name", course.Name);
        return (int)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task<int> UpsertStudentAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, NormalizedStudent student, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO estudiante (moodle_id, nombre_completo)
            VALUES (@moodle_id, @name)
            ON CONFLICT (moodle_id) DO UPDATE SET nombre_completo = EXCLUDED.nombre_completo
            RETURNING id
            """, connection, transaction);
        command.Parameters.AddWithValue("moodle_id", student.MoodleId);
        command.Parameters.AddWithValue("name", student.FullName);
        return (int)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task EnrollAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, int courseId, int studentId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "INSERT INTO matricula (curso_id, estudiante_id) VALUES (@course, @student) ON CONFLICT DO NOTHING",
            connection,
            transaction);
        command.Parameters.AddWithValue("course", courseId);
        command.Parameters.AddWithValue("student", studentId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> UpsertActivityAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, int courseId, NormalizedActivity activity, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO actividad (curso_id, moodle_id, nombre, categoria, ponderacion, escala_maxima)
            VALUES (@course, @moodle_id, @name, @category, @weight, @scale)
            ON CONFLICT (curso_id, moodle_id) DO UPDATE
                SET nombre = EXCLUDED.nombre,
                    categoria = EXCLUDED.categoria,
                    ponderacion = EXCLUDED.ponderacion,
                    escala_maxima = EXCLUDED.escala_maxima
            RETURNING id
            """, connection, transaction);
        command.Parameters.AddWithValue("course", courseId);
        command.Parameters.AddWithValue("moodle_id", activity.MoodleId);
        command.Parameters.AddWithValue("name", activity.Name);
        command.Parameters.AddWithValue("category", (object?)activity.Category ?? DBNull.Value);
        command.Parameters.AddWithValue("weight", (object?)activity.WeightPercent ?? DBNull.Value);
        command.Parameters.AddWithValue("scale", activity.MaxScale);
        return (int)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    /// <summary>true = insertada, false = actualizada, null = sin cambios.</summary>
    private static async Task<bool?> UpsertGradeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int activityId,
        int studentId,
        decimal? value,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO calificacion (actividad_id, estudiante_id, valor)
            VALUES (@activity, @student, @value)
            ON CONFLICT (actividad_id, estudiante_id) DO UPDATE
                SET valor = EXCLUDED.valor, sincronizado_en = now()
                WHERE calificacion.valor IS DISTINCT FROM EXCLUDED.valor
            RETURNING (xmax = 0)
            """, connection, transaction);
        command.Parameters.AddWithValue("activity", activityId);
        command.Parameters.AddWithValue("student", studentId);
        command.Parameters.AddWithValue("value", NpgsqlTypes.NpgsqlDbType.Numeric, (object?)value ?? DBNull.Value);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is bool wasInserted ? wasInserted : null;
    }

    private static async Task<T> RunAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (PostgresException exception) when (exception.SqlState.StartsWith("23", StringComparison.Ordinal))
        {
            throw;
        }
        catch (Exception exception) when (exception is NpgsqlException or SocketException or TimeoutException or IOException)
        {
            throw new AcademicStoreUnavailableException(exception);
        }
    }
}
