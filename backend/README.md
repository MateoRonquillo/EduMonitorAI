# Backend de EduMonitorAI

La solución contiene tres proyectos ASP.NET Core sobre .NET 10. Cada proyecto dispone de un punto de entrada en `Program.cs` y una comprobación de vida en `GET /health`.

## Requisitos y compilación

Instalar el SDK .NET 10 estable. El `global.json` de la raíz admite las bandas estables de SDK 10.0 desde 10.0.100 y selecciona la más reciente instalada. No se necesitan base de datos, Moodle ni credenciales para este arranque.

Desde la raíz del repositorio:

```bash
dotnet restore backend/EduMonitorAI.sln
dotnet build backend/EduMonitorAI.sln --no-restore
```

Los proyectos utilizan el framework compartido de ASP.NET Core y el paquete oficial `Microsoft.AspNetCore.OpenApi` 10.0.13 para generar la documentación. La primera restauración requiere acceso a NuGet o una caché con sus dependencias.

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

Cada comprobación debe devolver HTTP 200 y `Healthy`. Solo confirma que el proceso atiende solicitudes; no verifica Moodle, persistencia ni permisos. Las rutas de negocio y `/` no están mapeadas y devuelven HTTP 404.

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

## Límites de esta inicialización

No se implementan autenticación, autorización, sincronización, indicadores, agente, acceso a datos, contratos de negocio, Swagger UI ni proyectos de pruebas automatizadas. `Access.API` todavía no autentica usuarios. Las carpetas de pruebas y `BuildingBlocks` siguen reservadas sin proyectos.

Estos perfiles son para desarrollo local. Antes de un despliegue deberán configurarse HTTPS, identidad, autorización y las demás medidas de la especificación. No hay despliegue ni infraestructura configurados en este alcance.
