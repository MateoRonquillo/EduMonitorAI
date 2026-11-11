# Conectividad y Servicios Web de Moodle (Laboratorio)

Para la integración con `.NET` (`MoodleIntegration.API`), se ha habilitado el acceso mediante Web Services usando el protocolo **REST**.

## Credenciales de Acceso Local
* **URL Base:** `http://localhost:8080/webservice/rest/server.php`
* **Token Técnico:** `731384b60c38a8aa90073b01b4912887`
* **Protocolo:** REST (retorna JSON agregando `&moodlewsrestformat=json` a las peticiones).

## Privilegios y Limitaciones
* Se utilizó el servicio base `Moodle mobile web service` que incluye permisos de lectura.
* **Seguridad:** El token actual tiene privilegios de administrador para facilitar el desarrollo local. En producción, el equipo de backend deberá solicitar un rol restringido de solo lectura (Read-Only).