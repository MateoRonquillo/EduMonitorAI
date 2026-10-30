# Arquitectura inicial

## Organización propuesta

La solución contiene tres hosts ASP.NET Core con responsabilidades separadas, un endpoint técnico `/health` en cada uno y documentación OpenAPI habilitada solo en desarrollo. La lógica de negocio sigue pendiente; estos puntos de entrada no comprometen un despliegue independiente por servicio. Consulta la [inicialización del backend](../../backend/README.md) y los [contratos técnicos](../api/README.md).

| Servicio | Responsabilidad prevista |
| --- | --- |
| MoodleIntegration.API | Adaptar la comunicación de Moodle REST; validar identidad docente y restringir consultas a grants de curso firmados. |
| AcademicData.API | Gestionar los datos académicos y sus casos de uso. |
| Access.API | Gestionar políticas de acceso y la integración con identidad; su implementación funcional sigue pendiente en esta rama. |

El frontend consumirá contratos HTTP documentados. Cada servicio debe ser propietario de sus reglas y datos; se evitará acceder directamente a la persistencia de otro servicio.

## Organización interna al implementar

Se recomienda separar API, aplicación, dominio e infraestructura conforme crezca cada servicio:

- API: transporte HTTP, validación de entrada y composición de dependencias.
- Aplicación: casos de uso y puertos para dependencias externas.
- Dominio: entidades, invariantes y reglas de negocio.
- Infraestructura: persistencia y adaptadores para servicios externos.

Las dependencias deben apuntar hacia el dominio y la aplicación. Los detalles de infraestructura no deben introducirse en el dominio. Cuando se creen proyectos separados, mantener el proyecto `.API` como entrada y registrar los proyectos relacionados en la solución.

`BuildingBlocks` debe limitarse a necesidades técnicas compartidas demostradas; no debe convertirse en un dominio común que acople los servicios.

## Aspectos transversales

- Configurar secretos fuera del repositorio y documentar solo ejemplos sin valores reales.
- Validar identidad y grants de curso antes de exponer datos de Moodle; confirmar con la institución la frescura y exactitud de esos grants.
- Incorporar registros estructurados, identificadores de correlación y comprobaciones de salud al implementar los servicios.
- Definir tiempos de espera, reintentos y sincronización idempotente para Moodle.
- Mantener pruebas unitarias de reglas de negocio y pruebas de integración de los adaptadores y contratos.

## Decisiones pendientes

Se adoptó .NET 10 para los hosts en la [decisión 0001](decisions/0001-inicializacion-aspnet-core.md) y JWT OpenID Connect con grants `course_access` para consultas Moodle en la [decisión 0002](decisions/0002-integracion-moodle-protegida.md). Siguen pendientes versiones y framework del frontend, persistencia, integración funcional de `Access.API`, comunicación entre servicios y estrategia de despliegue. Registrar decisiones aceptadas en `decisions/` antes de introducir componentes que dependan de ellas.
