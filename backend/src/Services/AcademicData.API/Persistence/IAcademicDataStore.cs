using EduMonitorAI.AcademicData.Sync;

namespace EduMonitorAI.AcademicData.Persistence;

public interface IAcademicDataStore
{
    /// <summary>
    /// Guarda el curso normalizado actualizando por (curso, actividad, estudiante) en lugar de duplicar.
    /// Es atómico: si falla, no se conserva ningún cambio parcial.
    /// </summary>
    Task<StoreResult> SaveAsync(NormalizedCourseData data, CancellationToken cancellationToken);
}

public sealed class AcademicStoreUnavailableException(Exception inner)
    : Exception("La base de datos auxiliar no está disponible.", inner);
