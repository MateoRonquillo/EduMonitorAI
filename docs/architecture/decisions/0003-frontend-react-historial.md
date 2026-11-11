# ADR 0003: Frontend React y rutas del navegador

## Estado

Aceptada para la vista docente del Sprint 1.

## Contexto y restricciones

La especificación del proyecto propone React para el frontend y el requisito de mantenibilidad pide una estructura modular. La primera versión de la vista se había implementado con JavaScript imperativo y estado exclusivo en memoria; por ello, alternar pantallas no creaba entradas en el historial y Atrás podía salir directamente del sitio. La sesión Moodle no debe persistirse en almacenamiento del navegador.

## Decisión

- Usar React con Vite para compilar el frontend y React Router en modo declarativo con `BrowserRouter` para navegar mediante la History API.
- Representar cursos, participantes, calificaciones, contenido y datos sincronizados en rutas explícitas.
- Mantener el token personal solo en estado React durante la sesión. Una recarga requiere volver a iniciar sesión; una ruta protegida sin sesión redirige al formulario.
- Construir el bundle en una etapa Node de Docker y servirlo con Nginx, que conserva el proxy de mismo origen a las APIs.
- Mantener el alcance Sprint 1: no añadir el servicio agente, Ollama, estadísticas, alertas, recomendaciones ni conversación.

## Alternativas consideradas

- Mantener el renderizado manual: no satisface la tecnología React acordada y no ofrece navegación real entre vistas.
- Implementar rutas con `window.history` directamente: duplica funciones del router y aumenta el riesgo de estados de URL/React divergentes.
- Guardar el token en `localStorage` o `sessionStorage`: facilita restaurar una vista tras recargar, pero prolonga la persistencia de una credencial personal; se conserva el requisito de token solo en memoria.

## Consecuencias y criterios de revisión

Atrás y Adelante cambian la ruta y muestran la pantalla correspondiente mientras la sesión está activa. Los enlaces pueden abrirse, copiarse o actualizarse como rutas normales; una actualización no restaura la sesión y conduce al inicio de sesión. Vite agrega una etapa de build al contenedor; Nginx sigue siendo la imagen final de ejecución. Revisar esta decisión si se incorpora SSR, autenticación persistente o integración del frontend dentro de Moodle.
