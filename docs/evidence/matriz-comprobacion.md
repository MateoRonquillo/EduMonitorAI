# Matriz de Comprobación y Datos de Prueba (Fixtures)

Este documento contiene los datos ficticios y estados límite para validar la integración de Moodle con el backend .NET (PR-12). No contiene información real ni datos personales sensibles.

## 1. Usuarios Ficticios
| ID Moodle | Rol | Nombre Completo | Usuario |
| :--- | :--- | :--- | :--- |
| 101 | Docente | Docente Autorizado A | docente.a |
| 102 | Docente | Docente No Autorizado B | docente.b |
| 201 | Estudiante | Estudiante Prueba 1 | est.prueba1 |
| 202 | Estudiante | Estudiante Prueba 2 | est.prueba2 |
| 203 | Estudiante | Estudiante Prueba 3 | est.prueba3 |

## 2. Cursos de Prueba
* **Curso 1 (ID: 50):** Metodologías Ágiles (Asignado a Docente A)
* **Curso 2 (ID: 51):** Redes de Computadoras (Asignado a Docente B)

## 3. Matriz de Calificaciones (Casos Límite)
Se utilizará la escala sobre 10 puntos (Ponderación UTA: 40% Exámenes, 30% Autónomo, 30% APEs).

| Estudiante | Actividad | Estado | Calificación | Notas / Validaciones |
| :--- | :--- | :--- | :--- | :--- |
| Est. Prueba 1 | Examen Parcial 1 | Evaluado | 8.5 | Flujo normal. |
| Est. Prueba 2 | Examen Parcial 1 | Ausente | - | Debe excluirse del promedio, no contar como 0. |
| Est. Prueba 3 | Examen Parcial 1 | Evaluado | 0.0 | Cero real, sí afecta el promedio estadístico. |
| Est. Prueba 1 | Tarea Autónoma 1 | Pendiente | - | Actividad aún no calificada por el docente. |