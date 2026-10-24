# Backend de EduMonitorAI

La solución contiene tres proyectos ASP.NET Core sobre .NET 10. Cada proyecto dispone de un punto de entrada en `Program.cs` y una comprobación de vida en `GET /health`. `MoodleIntegration.API` expone consultas protegidas de cursos y participantes de Moodle.

## Requisitos y compilación

Instalar el SDK .NET 10 estable. El `global.json` de la raíz admite las bandas estables de SDK 10.0 desde 10.0.100 y selecciona la más reciente instalada. El arranque y `/health` no requieren Moodle ni credenciales; las rutas académicas sí.

Desde la raíz del repositorio:

```bash
dotnet restore backend/EduMonitorAI.sln
dotnet build backend/EduMonitorAI.sln --no-restore
```

Los proyectos utilizan el framework compartido de ASP.NET Core y el paquete oficial `Microsoft.AspNetCore.OpenApi` 10.0.13 para generar la documentación. `MoodleIntegration.API` usa además `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.13 y el cliente HTTP integrado de .NET. La primera restauración requiere acceso a NuGet o una caché con sus dependencias.

## Ejecución local

Ejecutar cada servicio en una terminal distinta desde la raíz:

```bash
dotnet run --project backend/src/Services/MoodleIntegration.API --launch-profile http
```

```bash
dotnet run --project backend/src/Services/AcademicData.API --launch-profile http
```

```bash
dotnet run --project backend/src/Services/Access.API --launch-profile http
```

| Proyecto | URL de comprobación local |
| --- | --- |
| MoodleIntegration.API | `http://localhost:5101/health` |
| AcademicData.API | `http://localhost:5102/health` |
| Access.API | `http://localhost:5103/health` |

```bash
curl --fail http://localhost:5101/health
curl --fail http://localhost:5102/health
curl --fail http://localhost:5103/health
```

Cada comprobación debe devolver HTTP 200 y `Healthy`. Solo confirma que el proceso atiende solicitudes; no verifica Moodle ni permisos. Las rutas `GET /courses` y `GET /courses/{courseId}/participants` requieren un JWT de docente y configuración válida de Moodle.

El endpoint usa `HealthCheckService`: responde texto plano con estado `Healthy` o `Degraded` y HTTP 200, o `Unhealthy` y HTTP 503. No hay sondas de dependencias registradas en este alcance, por lo que un proceso operativo devuelve `Healthy`.

Los perfiles `http` fijan puertos distintos, enlazan a localhost y usan el entorno `Development`. La configuración de logs está en cada `appsettings.json`; el host admite sobreescrituras mediante la configuración estándar de ASP.NET Core. `infra/.env.example` no se carga automáticamente.

## OpenAPI en desarrollo

Cada servicio genera un documento JSON en `GET /openapi/v1.json`, únicamente cuando el entorno es `Development`:

```bash
curl --fail http://localhost:5101/openapi/v1.json
curl --fail http://localhost:5102/openapi/v1.json
curl --fail http://localhost:5103/openapi/v1.json
```

El documento incluye `GET /health`, su propósito y las respuestas 200 y 503 en texto plano. Puede abrirse directamente o importarse en un cliente compatible con OpenAPI. No se incorpora una interfaz Swagger UI ni recursos de un CDN.

Para comprobar la restricción de entorno, detén el servicio que use el puerto y arráncalo sin perfil de desarrollo. Ejemplo con Access.API:

```bash
dotnet run --project backend/src/Services/Access.API --no-launch-profile -- --environment Production --urls http://localhost:5103
```

Desde otra terminal:

```bash
curl -i http://localhost:5103/health
curl -i http://localhost:5103/openapi/v1.json
```

Se esperan HTTP 200 y `Healthy` para `/health`, y HTTP 404 para OpenAPI. Se aplica la misma restricción en cualquier entorno distinto de `Development`. Para verificar los otros servicios, sustituye el proyecto y su puerto según la tabla.

Consulta el [contrato de los endpoints técnicos](../docs/api/README.md).

## Pruebas automatizadas de arranque

Desde la raíz del repositorio:

```bash
dotnet test backend/EduMonitorAI.sln --configuration Release
```

Para ejecutar únicamente el proyecto de integración y guardar resultados TRX:

```bash
dotnet test backend/tests/IntegrationTests/EduMonitorAI.IntegrationTests.csproj --configuration Release --logger trx
```

El proyecto utiliza xUnit y `Microsoft.AspNetCore.Mvc.Testing` para arrancar los puntos de entrada reales con `WebApplicationFactory` y un servidor HTTP en memoria. No requiere levantar servicios previamente, reservar puertos, configurar secretos ni disponer de Moodle o SQL Server. La primera ejecución restaura las dependencias de pruebas desde NuGet.

Los 18 casos (seis por servicio) comprueban:

- `/health`: HTTP 200, contenido `text/plain` y cuerpo `Healthy` en Development, Production y Staging.
- OpenAPI en Development: HTTP 200, JSON válido, operación `GET /health` y respuestas 200/503 en texto plano documentadas.
- OpenAPI en Production y Staging: HTTP 404.

Las pruebas establecen el entorno por host y deshabilitan redirecciones automáticas para comprobar las respuestas originales. Los hosts y clientes se liberan al terminar cada caso. Estas pruebas validan el arranque y los contratos técnicos; no prueban sockets de Kestrel, TLS, perfiles locales ni dependencias externas. Los escenarios de estados de salud degradado o fallido quedan fuera de estas pruebas básicas.

## Consultas protegidas a Moodle

Configura `Identity__Authority` y `Identity__Audience` para validar el JWT, además de `Moodle__BaseUrl` y `Moodle__Token` para el servicio web REST. La URL base debe ser HTTPS, excepto para loopback local. Moodle debe habilitar REST y autorizar `core_course_get_courses` y `core_enrol_get_enrolled_users`; los detalles están en la [guía del laboratorio](../docs/moodle/configuracion-laboratorio.md).

`GET /courses` filtra la respuesta del token técnico mediante los claims `course_access` del JWT firmado. `GET /courses/{courseId}/participants` verifica el mismo grant antes de consultar matrículas activas. Los errores de Moodle se devuelven como Problem Details 502/503 sin incluir mensajes internos. `infra/.env.example` no se carga automáticamente al ejecutar `dotnet run`; Compose sí pasa estas variables al contenedor MoodleIntegration.

## Límites actuales

No se implementan persistencia, sincronización en segundo plano, indicadores, agente ni Swagger UI. El proveedor de identidad y el token Moodle deben configurarse en cada entorno. Las pruebas actuales cubren únicamente el arranque; `UnitTests` y `BuildingBlocks` siguen reservadas sin proyectos.

Estos perfiles son para desarrollo local. Antes de un despliegue deberán configurarse HTTPS, identidad, autorización y las demás medidas de la especificación. No hay despliegue ni infraestructura configurados en este alcance.
