# Arquitectura inicial

## Organización propuesta

La estructura prepara tres servicios con responsabilidades separadas. La separación de carpetas no implica que ya existan servicios ejecutables o despliegues independientes.

| Servicio | Responsabilidad prevista |
| --- | --- |
| MoodleIntegration.API | Adaptar la comunicación con Moodle y coordinar la sincronización. |
| AcademicData.API | Gestionar los datos académicos y sus casos de uso. |
| Access.API | Gestionar las políticas de acceso y la integración con el mecanismo de identidad que se elija. |

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
- Definir autenticación, autorización y tratamiento de datos personales antes de exponer datos académicos.
- Incorporar registros estructurados, identificadores de correlación y comprobaciones de salud al implementar los servicios.
- Definir tiempos de espera, reintentos y sincronización idempotente para Moodle.
- Mantener pruebas unitarias de reglas de negocio y pruebas de integración de los adaptadores y contratos.

## Decisiones pendientes

Versiones y framework del frontend, versión de .NET, persistencia, identidad, comunicación entre servicios y estrategia de despliegue. Registrar las decisiones aceptadas en `decisions/` antes de introducir componentes que dependan de ellas.
