# Endpoints técnicos

Los tres hosts (`MoodleIntegration.API`, `AcademicData.API` y `Access.API`) exponen el contrato técnico de salud y OpenAPI. `Access.API` también valida la sesión docente mediante un token JWT firmado por el proveedor de identidad configurado.

| Método y ruta | Entorno | Respuesta |
| --- | --- | --- |
| `GET /health` | Todos | HTTP 200, texto plano `Healthy` o `Degraded`; HTTP 503, texto plano `Unhealthy`. |
| `GET /openapi/v1.json` | Solo `Development` | HTTP 200 con el documento OpenAPI JSON del host, que incluye `/health` y las rutas de negocio disponibles. |
| `GET /openapi/v1.json` | Cualquier otro entorno | HTTP 404; la ruta no se registra. |
| `GET /auth/session` | `Access.API`, token válido | HTTP 200 con la identidad docente verificada. |
| `GET /auth/session` | `Access.API`, token ausente, inválido, vencido o sin identidad docente | HTTP 401 con `application/problem+json` y `code: "unauthorized"`. |
| `GET /courses/{courseId}/access` | `Access.API`, token docente con grant para el curso | HTTP 204 sin contenido. |
| `GET /courses/{courseId}/access` | `Access.API`, identidad válida sin grant para el curso | HTTP 403 con `application/problem+json` y `code: "course_forbidden"`. |

## Sesión docente

`GET /auth/session` requiere `Authorization: Bearer <token>`. El token debe tener firma válida según las claves publicadas por el emisor, emisor y audiencia coincidentes, y estar vigente. Se exigen los claims `sub`, `teacher_id` y `role: "docente"`. El endpoint devuelve `subject`, `teacherId` y, si existen, `name` y `email`; no admite que el cliente envíe una identidad en el cuerpo de la solicitud.

Respuesta 200:

```json
{
  "subject": "id-del-proveedor",
  "teacherId": "id-docente",
  "name": "Docente",
  "email": "docente@example.edu"
}
```

Los parámetros se configuran fuera del repositorio con `Identity:Authority` y `Identity:Audience` (por ejemplo, variables `Identity__Authority` e `Identity__Audience`). El emisor debe publicar metadatos OpenID Connect y claves de firma accesibles por HTTPS. Sin un proveedor configurado, no se aceptan tokens. El esquema no implementa emisión ni renovación de tokens.

Ejemplo de respuesta 401:

```json
{
  "type": "https://httpstatuses.com/401",
  "title": "No autenticado",
  "status": 401,
  "code": "unauthorized",
  "detail": "Se requiere un token válido de una identidad docente confiable."
}
```

## Permisos efectivos por curso

La ruta `GET /courses/{courseId}/access` exige la policy `CourseAccess`. El handler compara el `courseId` de la ruta con los claims `course_access` del JWT ya validado. El proveedor confiable debe emitir un claim `course_access` por cada curso al que el docente tenga acceso efectivo; no se aceptan permisos enviados en encabezados ni en el cuerpo. Si el token es válido pero no contiene el grant exacto para el curso, la respuesta es 403. Si no hay token o su identidad no es válida, se conserva la respuesta 401.

Ejemplo de respuesta 403:

```json
{
  "type": "https://httpstatuses.com/403",
  "title": "Acceso denegado",
  "status": 403,
  "code": "course_forbidden",
  "detail": "La identidad autenticada no tiene permisos efectivos sobre el curso solicitado."
}
```

Los permisos del token son la fuente de autorización en esta iteración. El proveedor de identidad debe mantenerlos sincronizados con los permisos efectivos institucionales; la consulta directa a Moodle o a un repositorio de autorizaciones queda pendiente de integrar.

La sonda consulta `HealthCheckService` y solo expresa vida del proceso. No hay verificaciones de Moodle, base de datos u otras dependencias, ni se incluyen detalles internos o secretos en su respuesta. No constituye una comprobación de disponibilidad funcional del MVP.

OpenAPI se genera con el paquete oficial de ASP.NET Core. La publicación del documento se limita mediante `IsDevelopment()`; el entorno debe configurarse correctamente. La documentación local no incorpora Swagger UI.

Consulta los [comandos de ejecución y validación](../../backend/README.md). Los perfiles locales utilizan los puertos 5101 (MoodleIntegration), 5102 (AcademicData) y 5103 (Access).
