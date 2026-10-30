namespace EduMonitorAI.AcademicData.Persistence;

public static class DatabaseSchema
{
    public static readonly string[] Tables = ["curso", "estudiante", "matricula", "actividad", "calificacion"];

    /// <summary>
    /// Script idempotente. La calificación ausente se guarda como NULL ("sin calificar"); la escala máxima
    /// vive en la actividad y las claves únicas impiden duplicados por curso, actividad y estudiante.
    /// </summary>
    public const string CreateScript = """
        CREATE TABLE IF NOT EXISTS curso (
            id             integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            moodle_id      integer NOT NULL UNIQUE,
            nombre_corto   text    NOT NULL,
            nombre         text    NOT NULL,
            sincronizado_en timestamptz NOT NULL DEFAULT now()
        );

        CREATE TABLE IF NOT EXISTS estudiante (
            id              integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            moodle_id       integer NOT NULL UNIQUE,
            nombre_completo text    NOT NULL
        );

        CREATE TABLE IF NOT EXISTS matricula (
            curso_id      integer NOT NULL REFERENCES curso (id) ON DELETE CASCADE,
            estudiante_id integer NOT NULL REFERENCES estudiante (id) ON DELETE CASCADE,
            PRIMARY KEY (curso_id, estudiante_id)
        );

        CREATE TABLE IF NOT EXISTS actividad (
            id            integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            curso_id      integer NOT NULL REFERENCES curso (id) ON DELETE CASCADE,
            moodle_id     integer NOT NULL,
            nombre        text    NOT NULL,
            categoria     text,
            ponderacion   numeric(5,2) CHECK (ponderacion IS NULL OR (ponderacion >= 0 AND ponderacion <= 100)),
            escala_maxima numeric(10,2) NOT NULL CHECK (escala_maxima > 0),
            UNIQUE (curso_id, moodle_id)
        );

        CREATE TABLE IF NOT EXISTS calificacion (
            id              integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            actividad_id    integer NOT NULL REFERENCES actividad (id) ON DELETE CASCADE,
            estudiante_id   integer NOT NULL REFERENCES estudiante (id) ON DELETE CASCADE,
            valor           numeric(10,2) CHECK (valor IS NULL OR valor >= 0),
            sincronizado_en timestamptz NOT NULL DEFAULT now(),
            UNIQUE (actividad_id, estudiante_id)
        );
        """;
}
