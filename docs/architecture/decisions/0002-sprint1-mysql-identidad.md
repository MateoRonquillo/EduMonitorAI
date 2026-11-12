# ADR 0002: MySQL, identidad Moodle y despliegue Sprint 1

Estado: aceptada para este incremento, 22 de octubre de 2026.

Se conserva .NET 10 y la distribución inicial Access/MoodleIntegration/AcademicData. Se usa MySQL 8.4 por solicitud explícita del usuario, reemplazando PostgreSQL del Sprint 1 y la sugerencia SQL Server de la especificación general. No se interpreta el término mysqlserver como Microsoft SQL Server.

Moodle 5.0.3 y PHP 8.3 forman el laboratorio versionado. MySQL 8.4 cumple los requisitos de esa versión: https://moodledev.io/general/releases/5.0. El código se obtiene de la etiqueta oficial v5.0.3; no se incorpora la distribución local no versionada.

La identidad se delega a tokens personales Moodle con servicio de funciones de lectura. Un complemento local calcula cursos de matrícula activa con permisos efectivos. Cada microservicio revalida el permiso; no se confía en IDs, roles o cabeceras proporcionadas por el cliente. Revocar permisos corta también la consulta del almacenamiento auxiliar; una caída Moodle impide servirlo.

El token se propaga por petición y no se persiste. Esto simplifica revocación y evita introducir JWT propios o claves de firma en Sprint 1, a cambio de dependencia de Moodle para cada consulta. El laboratorio admite HTTP solo mediante opción explícita y loopback; producción requiere HTTPS.

AcademicData usa MySqlConnector y SQL parametrizado. Es el único propietario del esquema auxiliar. Los snapshots se sustituyen atómicamente por curso, con claves naturales compuestas; no son un historial académico. La auditoría conserva fecha, actor y resultado, sin datos sensibles de errores externos.

La vista docente de este incremento se limita a cursos, sincronización, participantes, notas individuales y actividades. No se introducen panel estadístico, motor de alertas ni agente antes de los siguientes sprints. Las actividades se guardan con su forma original y las notas preservan valores/escala; null se representa como unknown hasta disponer de evidencia de entrega.
