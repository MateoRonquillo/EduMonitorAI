# Configuración de Docker — EduMonitorAI

## Descripción

La infraestructura de EduMonitorAI utiliza Docker Compose para ejecutar los tres servicios ASP.NET Core y una base de datos PostgreSQL de manera conjunta.

Esta configuración corresponde al Sprint 1 y permite comprobar el arranque de los servicios. La integración real con Moodle y la persistencia académica se incorporarán posteriormente. La validación de identidad y la policy de grants por curso ya existen en `Access.API`, pero Docker no configura un proveedor de identidad ni grants.

## Requisitos

- Docker Desktop con Docker Engine funcionando.
- Docker Compose.
- Conexión a Internet para descargar las imágenes durante la primera ejecución.

## Servicios disponibles

| Servicio | Tecnología | Puerto local |
| --- | --- | --- |
| MoodleIntegration.API | ASP.NET Core 10 | 5101 |
| AcademicData.API | ASP.NET Core 10 | 5102 |
| Access.API | ASP.NET Core 10 | 5103 |
| PostgreSQL | PostgreSQL 17 | Solo red interna |

Los tres servicios .NET utilizan el puerto 8080 dentro de sus contenedores.

## Configuración inicial

Desde la raíz del repositorio, crear el archivo local de variables:

```bash
cp infra/.env.example infra/compose/.env
```

Abrir `infra/compose/.env` y configurar una contraseña local en `POSTGRES_PASSWORD`.

El archivo `.env` contiene información privada y está excluido del repositorio mediante `.gitignore`.

## Validar la configuración

```bash
docker compose -f infra/compose/compose.yml config --quiet
```

Este comando verifica la configuración sin iniciar contenedores.

## Construir e iniciar los servicios

```bash
docker compose -f infra/compose/compose.yml up -d --build
```

Docker construye las imágenes de las tres APIs y crea el contenedor de PostgreSQL.

## Comprobar el estado

```bash
docker compose -f infra/compose/compose.yml ps
```

Se espera que los tres servicios .NET estén en ejecución y PostgreSQL alcance el estado `healthy`.

## Verificar las APIs

```bash
curl.exe http://localhost:5101/health
curl.exe http://localhost:5102/health
curl.exe http://localhost:5103/health
```

Cada endpoint debe devolver:

`Healthy`

Estas verificaciones únicamente comprueban que las aplicaciones responden. No validan Moodle, persistencia académica ni permisos de curso.

## Consultar registros

Para consultar los registros de todos los servicios:

```bash
docker compose -f infra/compose/compose.yml logs
```

Para consultar únicamente PostgreSQL:

```bash
docker compose -f infra/compose/compose.yml logs postgres
```

## Detener la infraestructura

```bash
docker compose -f infra/compose/compose.yml down
```

Este comando detiene y elimina los contenedores y la red, pero mantiene el volumen de PostgreSQL.

No utilizar `down -v` cuando se desee conservar la información almacenada.

## Validaciones realizadas

Durante el desarrollo del Sprint 1 se comprobó:

- Construcción individual de las tres imágenes ASP.NET Core.
- Ejecución de cada servicio mediante Docker.
- Respuesta `Healthy` de los tres endpoints.
- Ejecución conjunta de cuatro servicios mediante Docker Compose.
- Estado `healthy` de PostgreSQL.
- Detención correcta de los contenedores.

También se verificó localmente la compilación del backend .NET y la ejecución de 18 pruebas automatizadas sin fallos.

## Limitaciones actuales

- La API puede consultar cursos y matrículas activas de Moodle, pero esta configuración requiere un servidor, token y proveedor de identidad externos que Compose no aprovisiona.
- PostgreSQL está preparado, pero los servicios aún no implementan la persistencia académica.
- Los endpoints de salud no comprueban dependencias externas.
- La validación de identidad y la policy inicial de permisos por curso se documentan en `docs/api/README.md`; esta configuración de Docker no aprovisiona ni configura un proveedor de identidad.
- El workflow de GitHub Actions está configurado localmente, pero requiere validación mediante su ejecución en GitHub.

## Archivos relacionados

- `infra/compose/compose.yml`: configuración de los contenedores.
- `infra/docker/`: Dockerfiles de las tres APIs.
- `infra/.env.example`: plantilla de variables de entorno.
- `.github/workflows/ci.yml`: validaciones de integración continua.
