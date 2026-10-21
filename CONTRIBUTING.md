# Contribuir a EduMonitorAI

Sigue la [estrategia de ramas y convenciones de commits](docs/gitflow.md) para crear ramas, preparar pull requests, revisar cambios y registrar contribuciones.

1. Describe el requisito o problema antes de implementar cambios.
2. Mantén los cambios centrados en un servicio o responsabilidad.
3. No agregues credenciales, datos personales ni archivos `.env` al repositorio.
4. Documenta los cambios de contratos en `docs/api/` y las decisiones relevantes en `docs/architecture/decisions/`.
5. Agrega pruebas para las reglas de negocio y las integraciones que implementes.
6. En cada propuesta de cambio, indica el propósito, la validación realizada y las limitaciones conocidas.

## Convenciones

- Usa nombres que expresen el propósito de clases, funciones y módulos.
- Mantén la lógica de negocio independiente de los controladores HTTP y de los proveedores externos.
- Evita dependencias directas entre implementaciones internas de distintos servicios.
- Respeta `.editorconfig` y los formateadores del stack cuando se configuren.

Los comandos de compilación y ejecución de los hosts están en el [README del backend](backend/README.md). Todavía no hay proyectos de pruebas automatizadas; para cambios en la inicialización, compila la solución y verifica `/health` en los tres hosts.
