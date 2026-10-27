# Modelo de datos académico (HU-05)

Base auxiliar PostgreSQL de `AcademicData.API`. Las tablas se crean de forma idempotente (`CREATE TABLE IF NOT EXISTS`) al arrancar el servicio y antes de cada sincronización; el script está en `backend/src/Services/AcademicData.API/Persistence/DatabaseSchema.cs`.

```text
curso (id, moodle_id UNIQUE, nombre_corto, nombre, sincronizado_en)
estudiante (id, moodle_id UNIQUE, nombre_completo)
matricula (curso_id → curso, estudiante_id → estudiante)            PK (curso_id, estudiante_id)
actividad (id, curso_id → curso, moodle_id, nombre, categoria, ponderacion 0–100, escala_maxima > 0)
                                                                     UNIQUE (curso_id, moodle_id)
calificacion (id, actividad_id → actividad, estudiante_id → estudiante, valor >= 0 | NULL, sincronizado_en)
                                                                     UNIQUE (actividad_id, estudiante_id)
```

- `calificacion.valor` es `NULL` cuando Moodle no tiene nota («sin calificar»); no se usa 0 como sustituto.
- `escala_maxima` vive en la actividad para conservar la escala original de cada nota; la categoría (Examen, Aprendizaje autónomo, APE) y su ponderación quedan en la misma fila.
- Las claves únicas hacen que la sincronización actualice en lugar de duplicar (`INSERT ... ON CONFLICT DO UPDATE`).
- Las tablas guardan una copia de lectura; Moodle sigue siendo la fuente oficial y el sistema no escribe en él.
