# EduMonitorAI

Base de organización para una plataforma de seguimiento académico con integración a Moodle.

## Estructura

- `frontend/src/`: aplicación cliente; `app` contiene la configuración y composición, `pages` las vistas, `components` los elementos reutilizables y `services` los clientes de API.
- `backend/EduMonitorAI.sln`: solución .NET inicial, todavía sin proyectos.
- `backend/src/Services/`: servicios de integración con Moodle, datos académicos y acceso.
- `backend/src/BuildingBlocks/`: componentes técnicos compartidos mínimos, sin reglas de negocio de los servicios.
- `backend/tests/`: pruebas unitarias y de integración.
- `docs/`: arquitectura, contratos de API, requisitos y evidencias.
- `infra/`: recursos de Docker, Compose y ejemplo de variables de entorno.
- `.github/workflows/`: ubicación de futuros flujos de integración continua.

## Estado inicial

Este repositorio contiene la estructura y las convenciones iniciales. Aún no hay aplicaciones ejecutables, proyectos .NET, dependencias, contenedores ni flujos de CI configurados.

Antes de implementar, se deben elegir las versiones de .NET y del frontend, los mecanismos de autenticación y la persistencia. Después se podrán crear los proyectos y registrarlos en la solución.

Consulta [la guía de arquitectura](docs/architecture/README.md) y [las pautas de contribución](CONTRIBUTING.md).
