# Endpoints técnicos

Los tres hosts (`MoodleIntegration.API`, `AcademicData.API` y `Access.API`) exponen el mismo contrato técnico. Todavía no hay rutas de negocio ni autenticación.

| Método y ruta | Entorno | Respuesta |
| --- | --- | --- |
| `GET /health` | Todos | HTTP 200, texto plano `Healthy` o `Degraded`; HTTP 503, texto plano `Unhealthy`. |
| `GET /openapi/v1.json` | Solo `Development` | HTTP 200 con el documento OpenAPI JSON del host, que incluye `/health`. |
| `GET /openapi/v1.json` | Cualquier otro entorno | HTTP 404; la ruta no se registra. |

La sonda consulta `HealthCheckService` y solo expresa vida del proceso. No hay verificaciones de Moodle, base de datos u otras dependencias, ni se incluyen detalles internos o secretos en su respuesta. No constituye una comprobación de disponibilidad funcional del MVP.

OpenAPI se genera con el paquete oficial de ASP.NET Core. La publicación del documento se limita mediante `IsDevelopment()`; el entorno debe configurarse correctamente. La documentación local no incorpora Swagger UI.

Consulta los [comandos de ejecución y validación](../../backend/README.md). Los perfiles locales utilizan los puertos 5101 (MoodleIntegration), 5102 (AcademicData) y 5103 (Access).
