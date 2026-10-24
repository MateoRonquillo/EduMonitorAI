# EduMonitorAI

Sistema de seguimiento y análisis del rendimiento académico integrado con Moodle. El proyecto propone una aplicación web independiente que consulte información autorizada, calcule indicadores verificables y permita a un docente recibir recomendaciones de un agente de IA como apoyo a su valoración académica.

Proyecto académico de MVP para cuatro integrantes y seis semanas, basado en `Proyecto_EduMonitor_AI_Especificacion.pdf`, especialmente sus secciones 3–7, 10 y 12. Los requisitos siguientes describen el producto esperado; no representan funcionalidades ya implementadas.

## Estado del repositorio

Actualmente existen la estructura de carpetas, documentación, archivos comunes y una solución con tres hosts ASP.NET Core sobre .NET 10 y pruebas automatizadas de arranque. Cada host expone `GET /health` como comprobación de vida y el documento OpenAPI en `GET /openapi/v1.json` solo en desarrollo. `Access.API` valida tokens de un proveedor OpenID Connect confiable y ofrece `GET /auth/session` para obtener la identidad docente verificada. Aún no hay funcionalidad académica, persistencia, integración ejecutable con Moodle, agente, contenedores ni flujos de CI configurados.

Los archivos `.gitkeep` permiten conservar las carpetas vacías en Git. Las variables de [infra/.env.example](infra/.env.example) son propuestas y aún no son consumidas por una aplicación.

## Alcance del MVP

| Requisitos | Resultado esperado |
| --- | --- |
| RF-01 | Autenticación con roles docente y administrador técnico; acceso solo a cursos y estudiantes autorizados. La validación inicial de identidad docente está implementada; los demás controles de acceso están pendientes. |
| RF-02 y RF-03 | Configuración segura de la conexión a Moodle y sincronización manual de cursos, matriculados, ítems, notas y estados de actividad según API y permisos disponibles; registro de fecha y errores. |
| RF-04 | Selección de curso y período, listado de estudiantes, búsqueda y filtros. |
| RF-05 y RF-06 | Dashboard y ficha individual con promedios de escala explícita, distribución, progreso disponible y evolución; distinción entre nota cero, sin entrega, pendiente de calificación y completado. |
| RF-07 y RF-08 | Alertas configurables con evidencia y análisis de tendencias grupales, sin etiquetas definitivas sobre estudiantes. |
| RF-09 y RF-10 | Chat de un agente que consulte herramientas reales, distinga hechos, inferencias y recomendaciones y proponga intervenciones justificadas para revisión docente. |
| RF-11 | Resumen consultable o descargable con período, métricas, hallazgos, recomendaciones y fecha de actualización. |
| RF-12 | Auditoría de sincronizaciones, consultas y herramientas utilizadas sin exponer secretos. |

El escenario mínimo contempla un docente autenticado y uno o varios cursos autorizados. El agente debe seleccionar e invocar al menos tres herramientas reales, validar sus parámetros y registrar resultados y fallos. Los promedios y la detección numérica de alertas se calcularán mediante código determinista; el modelo interpretará los resultados sin inventar notas ni estudiantes.

### Fuera del alcance

- Predicción clínica o psicológica y diagnósticos sobre estudiantes.
- Modificación de calificaciones y automatización de decisiones académicas.
- Mensajería automática a estudiantes.
- Entrenamiento de modelos propios y sistemas multiagente.

Los microservicios no son obligatorios en la especificación. La organización modular de este repositorio no compromete un despliegue independiente por carpeta.

## Estructura modular

```text
EduMonitorAI/
├── frontend/
│   ├── README.md
│   └── src/
│       ├── app/
│       ├── pages/
│       ├── components/
│       └── services/
├── backend/
│   ├── README.md
│   ├── EduMonitorAI.sln
│   ├── src/
│   │   ├── BuildingBlocks/
│   │   └── Services/
│   │       ├── MoodleIntegration.API/
│   │       ├── AcademicData.API/
│   │       └── Access.API/
│   └── tests/
│       ├── UnitTests/
│       └── IntegrationTests/
├── docs/
│   ├── architecture/
│   │   └── decisions/
│   ├── api/
│   ├── requirements/
│   ├── evidence/
│   └── gitflow.md
├── infra/
│   ├── docker/
│   ├── compose/
│   └── .env.example
├── .github/workflows/
├── .editorconfig
├── .gitignore
├── global.json
├── README.md
└── CONTRIBUTING.md
```

| Módulo o carpeta | Responsabilidad prevista |
| --- | --- |
| `frontend/src/app/` | Configuración y composición del cliente web. |
| `frontend/src/pages/` | Vistas de acceso, cursos, dashboard, seguimiento, alertas y chat. |
| `frontend/src/components/` | Elementos reutilizables de interfaz. |
| `frontend/src/services/` | Clientes HTTP para consumir contratos del backend. |
| `backend/src/Services/MoodleIntegration.API/` | Adaptación de servicios web oficiales de Moodle, sincronización y manejo de errores. |
| `backend/src/Services/AcademicData.API/` | Casos de uso académicos, indicadores deterministas, seguimiento y reglas de alertas. |
| `backend/src/Services/Access.API/` | Autenticación y autorización por rol y curso. |
| `backend/src/BuildingBlocks/` | Utilidades técnicas compartidas mínimas, sin centralizar reglas de negocio. |
| `backend/tests/UnitTests/` | Pruebas futuras de indicadores y reglas de negocio. |
| `backend/tests/IntegrationTests/` | Pruebas de arranque, health checks y OpenAPI por entorno; integración Moodle y permisos pendientes. |
| `docs/architecture/` | Diseño y registro de decisiones técnicas. |
| `docs/api/` | Contratos y documentación de endpoints. |
| `docs/requirements/` | Requisitos y futura matriz de cumplimiento RF/RNF. |
| `docs/evidence/` | Evidencias de validación y registro de contribuciones. |
| `infra/` | Configuración de ejemplo y futuros recursos Docker y Compose. |
| `.github/workflows/` | Ubicación reservada para integración continua. |

La solución `backend/EduMonitorAI.sln` contiene los tres proyectos `.API`, cada uno con `Program.cs`, configuración de logs y un perfil de ejecución local. Están disponibles `/health` y OpenAPI en desarrollo; las responsabilidades de negocio de la tabla siguen pendientes. El servicio de agente y su ubicación dentro del repositorio están pendientes de una decisión documentada; no existe todavía una carpeta ni una implementación para ese componente.

## Arquitectura de referencia

La especificación sugiere React para el frontend, ASP.NET Core Web API para el backend y SQL Server para persistencia. Para el agente propone Python/FastAPI con LangGraph o una implementación equivalente, y un modelo local mediante Ollama si el hardware lo permite. Se admiten tecnologías equivalentes justificadas; estas opciones no implican dependencias ya configuradas.

El frontend consumirá el backend; este controlará la autorización y el acceso a datos y a Moodle. El agente invocará herramientas controladas del backend y no accederá directamente a credenciales ni a tablas sin controles. La separación entre API, aplicación, dominio e infraestructura se desarrollará conforme se creen los proyectos, según la [guía de arquitectura](docs/architecture/README.md).

La elección de .NET 10 y los hosts mínimos se registra en la [decisión 0001](docs/architecture/decisions/0001-inicializacion-aspnet-core.md). La integración de identidad se registra en la [decisión 0002](docs/architecture/decisions/0002-identidad-docente-openid-connect.md). Las demás versiones, la distribución de capas, la ubicación del agente y el despliegue se registrarán en [decisiones de arquitectura](docs/architecture/decisions/README.md) antes de introducir las dependencias correspondientes.

## Seguridad y calidad esperadas

- **RNF-01 y RNF-02:** HTTPS al desplegar, secretos en variables de entorno, mínimo privilegio y acceso por curso. Utilizar datos mínimos y anonimizados para pruebas; no enviar información identificable a servicios externos sin autorización ni versionar credenciales o datos personales.
- **RNF-03 y RNF-04:** manejar errores, datos ausentes y sincronizaciones incompletas; mostrar filtros, unidades y mensajes comprensibles sin presentar métricas engañosas.
- **RNF-05 y RNF-06:** mantener módulos y documentación, con pruebas reproducibles de indicadores, API y permisos.
- **RNF-07 y RNF-08:** limitar o paginar consultas, registrar tiempos medidos y mostrar fuente, fecha de sincronización y evidencia de alertas; la IA debe reconocer incertidumbre.

Antes de conectar Moodle se deberán verificar versión, servicios habilitados, permisos, escalas y disponibilidad de finalización de actividades. Si no hay acceso institucional autorizado, se utilizará una instancia de laboratorio con datos ficticios.

## Instalación y validación

Instala el SDK .NET 10 estable y, desde la raíz, restaura y compila la solución:

```bash
dotnet restore backend/EduMonitorAI.sln
dotnet build backend/EduMonitorAI.sln --no-restore
```

El [README del backend](backend/README.md) detalla la ejecución de los tres hosts y la verificación de `/health` en los puertos locales 5101, 5102 y 5103. No se requieren tokens, Moodle ni base de datos para este arranque. El frontend sigue sin implementar.

Para ejecutar las 18 pruebas automatizadas de arranque de los tres servicios:

```bash
dotnet test backend/EduMonitorAI.sln --configuration Release
```

Las pruebas utilizan hosts HTTP en memoria y validan `/health` y OpenAPI en Development, Production y Staging. No sustituyen las futuras pruebas de indicadores, permisos o integración con Moodle.

Al implementar los componentes, este README deberá incorporar versiones y prerrequisitos, instalación, configuración sin secretos, ejecución local, pruebas y despliegue reproducibles. La aceptación del MVP deberá demostrar acceso restringido por curso, datos concordantes con Moodle, estados académicos diferenciados, alertas con evidencia y tres solicitudes del agente mediante herramientas reales, incluyendo el manejo de fallos sin inventar información.

La entrega final también deberá incluir diagrama de arquitectura, esquema de datos, endpoints, guía de uso, evidencias, matriz RF/RNF, registro de contribuciones y video o demostración, conforme a la sección 12 de la especificación. Estos entregables siguen pendientes; las carpetas reservadas no acreditan su cumplimiento.

## Documentación y contribuciones

- [Frontend](frontend/README.md).
- [Backend: compilación, ejecución y comprobaciones](backend/README.md).
- [Endpoints técnicos y OpenAPI local](docs/api/README.md).
- [Arquitectura](docs/architecture/README.md) y [decisiones técnicas](docs/architecture/decisions/README.md).
- [Estrategia de ramas y convenciones de commits](docs/gitflow.md).
- [Pautas de contribución](CONTRIBUTING.md).
