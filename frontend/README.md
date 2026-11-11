# Vista docente React

Aplicación React compilada con Vite, servida por Nginx en Docker Compose y disponible en http://localhost:5170.

## Alcance Sprint 1

El [Sprint 2](../docs/requirements/sprint2.md) agrega el panel académico, perfiles autorizados, estadísticas, alertas, contenidos con evidencia e integración por API configurable para interpretación acotada. La descripción siguiente conserva el alcance original.

- Iniciar sesión con Moodle y consultar solo cursos autorizados por Access.API.
- Sincronizar un curso o todos los cursos permitidos y mostrar origen, fecha y estado.
- Consultar participantes, notas de actividades en la escala original y unidades/actividades del curso.
- Mostrar los casos sin nota como “Sin nota visible”; la vista no infiere entrega ni retraso.

La vista no calcula indicadores, crea alertas, genera recomendaciones ni ofrece chat con el agente. Esos elementos corresponden a sprints posteriores. No modifica Moodle.

## Navegación y sesión

React Router mantiene las vistas en rutas normales del navegador, de modo que Atrás y Adelante recorren los cursos, calificaciones, contenido y datos sincronizados visitados. Nginx resuelve las rutas de la SPA al documento de inicio y enruta `/moodle/`, `/access/`, `/integration/` y `/academic/` por el mismo origen.

El token personal se guarda solo en memoria React, hasta cerrar sesión o recargar la página. Al recargar, la vista solicita iniciar sesión otra vez. El token no se guarda en almacenamiento local ni de sesión.

## Estructura y ejecución

- `src/main.jsx`: arranque React y configuración del router.
- `src/App.jsx`: rutas, sesión y vistas del Sprint 1.
- `styles.css`: estilos compartidos de la vista.
- `vite.config.js`: compilación SPA.
- `nginx.conf`: fallback de rutas y proxy hacia servicios Docker.
- `Dockerfile`: build multi-stage Node.js + Nginx.

Docker Compose publica el frontend solo en `127.0.0.1:5170`. Para desarrollo local, con Node.js y pnpm: `pnpm install --frozen-lockfile`, luego `pnpm dev`; para compilar: `pnpm build`.
