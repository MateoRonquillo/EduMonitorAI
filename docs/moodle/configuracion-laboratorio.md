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