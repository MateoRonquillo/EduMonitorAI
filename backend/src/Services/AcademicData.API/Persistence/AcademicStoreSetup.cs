using EduMonitorAI.AcademicData.Sync;
using Npgsql;

namespace EduMonitorAI.AcademicData.Persistence;

public static class AcademicStoreSetup
{
    public const string ConnectionStringName = "AcademicDb";

    public static IServiceCollection AddAcademicStore(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddSingleton<IAcademicDataStore, UnconfiguredAcademicDataStore>();
            return services;
        }

        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddSingleton<PostgresAcademicDataStore>();
        services.AddSingleton<IAcademicDataStore>(provider => provider.GetRequiredService<PostgresAcademicDataStore>());
        services.AddHostedService<DatabaseInitializer>();
        return services;
    }
}

internal sealed class UnconfiguredAcademicDataStore : IAcademicDataStore
{
    public Task<StoreResult> SaveAsync(NormalizedCourseData data, CancellationToken cancellationToken) =>
        throw new AcademicStoreUnavailableException(new InvalidOperationException("ConnectionStrings:AcademicDb no está configurada."));
}

/// <summary>
/// Intenta crear las tablas al arrancar sin detener el servicio si la base no responde: reintenta en segundo plano
/// y, mientras tanto, las sincronizaciones reciben un error controlado.
/// </summary>
internal sealed class DatabaseInitializer(PostgresAcademicDataStore store, ILogger<DatabaseInitializer> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await store.EnsureSchemaAsync(stoppingToken);
                logger.LogInformation("Estructuras de la base auxiliar verificadas.");
                return;
            }
            catch (AcademicStoreUnavailableException)
            {
                logger.LogWarning("La base auxiliar no está disponible; se reintentará en {Seconds} s.", RetryDelay.TotalSeconds);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "No se pudieron crear las estructuras de la base auxiliar.");
                return;
            }

            try
            {
                await Task.Delay(RetryDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
