# Endpoints técnicos

Los tres hosts (`MoodleIntegration.API`, `AcademicData.API` y `Access.API`) exponen el contrato técnico de salud y OpenAPI. `MoodleIntegration.API` autentica docentes con JWT y consulta Moodle para listar cursos permitidos, participantes activos, actividades y calificaciones. `AcademicData.API` y `Access.API` aún solo exponen los endpoints técnicos descritos aquí.

| Método y ruta | Entorno | Respuesta |
| --- | --- | --- |
| `GET /health` | Todos | HTTP 200, texto plano `Healthy` o `Degraded`; HTTP 503, texto plano `Unhealthy`. |
| `GET /openapi/v1.json` | Solo `Development` | HTTP 200 con el documento OpenAPI JSON del host, que incluye `/health`. |
| `GET /openapi/v1.json` | Cualquier otro entorno | HTTP 404; la ruta no se registra. |
| `GET /courses` | `MoodleIntegration.API`, docente autenticado | HTTP 200 con cursos de Moodle que coinciden con grants `course_access` del token. |
| `GET /courses/{courseId}/participants` | `MoodleIntegration.API`, docente con grant para el curso | HTTP 200 con participantes activos inscritos, mapeados a campos mínimos. |
| `GET /courses/{courseId}/activities` | `MoodleIntegration.API`, docente con grant para el curso | HTTP 200 con actividades por sección y campos mínimos. |
| `GET /courses/{courseId}/grades` | `MoodleIntegration.API`, docente con grant para el curso | HTTP 200 con calificaciones de participantes activos inscritos. |
| Rutas anteriores | Token ausente o inválido | HTTP 401 Problem Details (`unauthorized`). |
| Rutas por curso | Docente sin grant para el curso | HTTP 403 Problem Details (`course_forbidden`). |
| Rutas Moodle | Moodle no disponible o responde con error | HTTP 502 o 503 Problem Details, sin exponer mensajes ni depuración de Moodle. |

## Cursos y participantes de Moodle

`GET /courses` requiere un JWT válido del proveedor OpenID Connect configurado. Consulta `core_course_get_courses` con el token técnico del servidor y devuelve únicamente cursos cuyo ID aparece en un claim `course_access` del JWT. Los cursos del token que Moodle no devuelve tampoco aparecen en la respuesta.

`GET /courses/{courseId}/participants` exige el mismo JWT y un grant `course_access` exacto para el ID de ruta antes de llamar a `core_enrol_get_enrolled_users`. La llamada solicita solo matrículas activas y los campos Moodle `id`, `firstname` y `lastname`. La respuesta pública incluye `id`, `fullName` y los roles Moodle; no devuelve usuario ni correo.

`GET /courses/{courseId}/activities` exige el grant antes de consultar `core_course_get_contents`; aplana las actividades de las secciones y conserva número y nombre de sección. `GET /courses/{courseId}/grades` exige el grant, obtiene primero los participantes activos y limita el reporte `gradereport_user_get_grade_items` a sus IDs. No incluye usuarios ajenos o inactivos; si el reporte no trae una fila para un participante activo, este aparece con `grades: []`.

Cada elemento de nota contiene `gradeRaw`, `gradeFormatted`, `status`, `submittedAtUtc` y `gradedAtUtc`. `gradeRaw: 0` es una nota evaluada válida (`status: "evaluado"`); un valor `null` no se convierte en cero. Un valor nulo con fecha de entrega se marca `pendiente`; si Moodle no reporta nota ni entrega, se marca `sin_calificar`. El reporte no permite inferir ausencia de entrega con certeza en todos los casos, por lo que la API conserva el dato nulo en lugar de afirmar que hubo una ausencia.

Ejemplo de curso:

```json
{
  "id": 50,
  "shortName": "MET-101",
  "fullName": "Metodologías Ágiles",
  "summary": "Descripción del curso",
  "startAtUtc": "2026-01-05T00:00:00+00:00",
  "endAtUtc": null,
  "visible": true
}
```

Ejemplo de participante:

```json
{
  "id": 201,
  "fullName": "Estudiante Prueba 1",
  "roles": ["student"]
}
```

La integración requiere `Identity:Authority`, `Identity:Audience`, `Moodle:BaseUrl` y `Moodle:Token` mediante configuración externa. El emisor debe firmar tokens con `sub`, `teacher_id`, `role: docente` y uno o más claims `course_access`. Moodle debe tener habilitado REST y permitir `core_course_get_courses`, `core_enrol_get_enrolled_users`, `core_course_get_contents` y `gradereport_user_get_grade_items` para el token técnico. Se envían el token y parámetros por POST en el cuerpo, no en la URL. En producción, configura Moodle por HTTPS; HTTP se admite solo para loopback local.

La sonda consulta `HealthCheckService` y solo expresa vida del proceso. No hay verificaciones de Moodle, base de datos u otras dependencias, ni se incluyen detalles internos o secretos en su respuesta. No constituye una comprobación de disponibilidad funcional del MVP.

OpenAPI se genera con el paquete oficial de ASP.NET Core. La publicación del documento se limita mediante `IsDevelopment()`; el entorno debe configurarse correctamente. La documentación local no incorpora Swagger UI.

Consulta los [comandos de ejecución y validación](../../backend/README.md). Los perfiles locales utilizan los puertos 5101 (MoodleIntegration), 5102 (AcademicData) y 5103 (Access).
