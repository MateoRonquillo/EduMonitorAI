-- Esquema Auxiliar EduMonitor AI (PostgreSQL)
CREATE TABLE Cursos (
    Id SERIAL PRIMARY KEY,
    MoodleCourseId INT UNIQUE NOT NULL,
    NombreCompleto VARCHAR(255) NOT NULL,
    NombreCorto VARCHAR(50) NOT NULL,
    UltimaSincronizacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE Participantes (
    Id SERIAL PRIMARY KEY,
    MoodleUserId INT UNIQUE NOT NULL,
    Nombres VARCHAR(255) NOT NULL,
    Email VARCHAR(255) NOT NULL,
    Rol VARCHAR(50) NOT NULL CHECK (Rol IN ('Docente', 'Estudiante'))
);

CREATE TABLE CursoParticipantes (
    CursoId INT REFERENCES Cursos(Id) ON DELETE CASCADE,
    ParticipanteId INT REFERENCES Participantes(Id) ON DELETE CASCADE,
    PRIMARY KEY (CursoId, ParticipanteId)
);

CREATE TABLE Actividades (
    Id SERIAL PRIMARY KEY,
    MoodleAssignmentId INT UNIQUE NOT NULL,
    CursoId INT REFERENCES Cursos(Id) ON DELETE CASCADE,
    Nombre VARCHAR(255) NOT NULL,
    NotaMaxima DECIMAL(5, 2) NOT NULL
);

CREATE TABLE CalificacionesNormalizadas (
    Id SERIAL PRIMARY KEY,
    ActividadId INT REFERENCES Actividades(Id) ON DELETE CASCADE,
    ParticipanteId INT REFERENCES Participantes(Id) ON DELETE CASCADE,
    Nota DECIMAL(5, 2),
    Estado VARCHAR(50) NOT NULL CHECK (Estado IN ('Evaluado', 'Pendiente', 'Ausente')),
    UNIQUE (ActividadId, ParticipanteId)
);