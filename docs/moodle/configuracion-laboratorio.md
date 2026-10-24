# Configuración del Laboratorio Moodle - EduMonitor AI

Este documento detalla la instalación, dependencias y configuración reproducible de la instancia Moodle de laboratorio para validación local.

## 1. Dependencias y Entorno
* **Plataforma:** Docker y Docker Compose (recomendado para entorno reproducible).
* **Servidor Web:** Apache o Nginx (incluido en la imagen oficial de Moodle).
* **PHP:** Versión 8.0 o superior.
* **Base de Datos:** PostgreSQL o MariaDB de prueba (estrictamente sin datos de producción).

## 2. Configuración Reproducible (PR-11)
Para inicializar el laboratorio sin exponer secretos:
1. Se utilizará un archivo `docker-compose.yml` base (ubicado en `infra/`) para levantar los contenedores de base de datos y Moodle.
2. Queda estrictamente prohibido subir credenciales reales, tokens o respaldos con datos personales de la universidad al repositorio.

## 3. Configuración de Servicios Web (PR-13)
Para que la API .NET consuma los datos académicos de forma segura, se debe configurar Moodle de la siguiente manera:
* **Habilitar Servicios Web:** Navegar a *Administración del sitio > Características avanzadas* y habilitar la opción.
* **Protocolo:** Habilitar el protocolo REST.
* **Privilegios del Token Técnico:** Crear un rol específico con acceso mínimo (principio de menor privilegio) únicamente a las siguientes funciones de la API externa:
  * `core_course_get_courses`
  * `core_enrol_get_enrolled_users`
  * `gradereport_user_get_grade_items`
* **Matrículas activas:** La integración envía `onlyactive=1` al consultar participantes. El token técnico debe tener las capacidades Moodle requeridas por esta opción, en particular `moodle/course:enrolreview` en los cursos consultados.

## 4. Configuración de MoodleIntegration.API

La API utiliza `Moodle__BaseUrl` (por ejemplo, `https://moodle.example.org`) y `Moodle__Token`. El token se envía por POST al endpoint `/webservice/rest/server.php`; no se guarda en código ni se coloca en la URL. Para desarrollo local, copia las variables de [`infra/.env.example`](../../infra/.env.example) a `infra/compose/.env`; el Compose las pasa al contenedor `moodle-integration`.

También se requiere `Identity__Authority` y `Identity__Audience`. El proveedor debe emitir tokens firmados con `sub`, `teacher_id`, `role: docente` y un claim `course_access` por cada curso permitido. `GET /courses` filtra la respuesta Moodle por esos grants; `GET /courses/{courseId}/participants` valida el grant antes de consultar a Moodle. Consulta el contrato en la [documentación de API](../api/README.md).
