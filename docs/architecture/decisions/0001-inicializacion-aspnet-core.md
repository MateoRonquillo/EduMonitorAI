# 0001 - Inicializar los puntos de entrada con ASP.NET Core

## Estado

Aceptada para la inicialización del backend.

## Contexto y restricciones

La sección 6 de `Proyecto_EduMonitor_AI_Especificacion.pdf` sugiere ASP.NET Core Web API y admite equivalentes justificados. El repositorio ya separa las responsabilidades de integración Moodle, datos académicos y acceso. Este incremento crea sus hosts, sin implementar requisitos funcionales posteriores ni exigir un despliegue de microservicios.

## Decisión

- Utilizar .NET 10 LTS y `Microsoft.NET.Sdk.Web` con destino `net10.0`.
- Definir `global.json` en la raíz con SDK mínimo 10.0.100, `latestFeature` y versiones preliminares deshabilitadas, para admitir SDK estables de la misma línea 10.0.
- Registrar `MoodleIntegration.API`, `AcademicData.API` y `Access.API` en la solución existente.
- Usar el modelo de alojamiento mínimo de ASP.NET Core y el endpoint técnico `/health` integrado en el framework; no agregar paquetes externos ni rutas de demostración de negocio.
- Mantener perfiles HTTP locales con puertos 5101, 5102 y 5103 para comprobar el arranque simultáneo.

.NET 10 es una versión LTS según la [política oficial de soporte](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core). El modelo de alojamiento sigue la [documentación oficial de Minimal APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-10.0).

## Alternativas consideradas

- .NET 8 o 9: líneas anteriores; se elige la línea LTS 10 para iniciar el proyecto.
- Controladores y paquetes OpenAPI desde el inicio: se posponen hasta definir contratos HTTP; no son necesarios para verificar el arranque.
- Un único host: sigue siendo una alternativa de despliegue futura. En este incremento se crean los tres puntos de entrada solicitados por la estructura del repositorio.

## Consecuencias y criterios de revisión

Evolución posterior: el incremento de health checks y OpenAPI incorpora `Microsoft.AspNetCore.OpenApi` y documentación local limitada a desarrollo. El [contrato técnico actual](../../api/README.md) describe los endpoints; la ausencia de paquetes OpenAPI indicada arriba corresponde exclusivamente a la inicialización original.

Se requiere el SDK .NET 10 para compilar. Los hosts pueden arrancar sin infraestructura externa, pero una respuesta `Healthy` solo acredita vida del proceso. No valida los requisitos RF/RNF ni la disponibilidad de dependencias.

La identidad, persistencia, integración Moodle, contratos, HTTPS de despliegue y ubicación del agente se resolverán en decisiones posteriores. Revisar esta decisión si cambian las restricciones del entorno académico o el modelo de despliegue.
