# Estrategia de ramas y convenciones

## Propósito y referencia

Esta guía define un GitFlow ligero para coordinar a los cuatro integrantes durante las seis semanas del MVP de EduMonitor AI.

Referencia: `Proyecto_EduMonitor_AI_Especificacion.pdf`, secciones 5 (RNF-01, RNF-02, RNF-05 y RNF-06), 8 (responsabilidades compartidas), 9 (entregables) y 12 (historial y registro de contribuciones).

La especificación pide repositorio Git, revisión de código, integración continua, evidencias y trazabilidad de contribuciones. La estrategia de ramas y la sintaxis de commits siguientes son convenciones del equipo para cumplir esos objetivos; no son reglas textuales del documento. Su adopción no obliga a implementar microservicios ni amplía el alcance funcional del MVP.

## Ramas

| Rama | Propósito | Origen | Destino mediante pull request |
| --- | --- | --- | --- |
| `main` | Versiones estables y entregables verificados. | Inicialización del repositorio | No aplica |
| `develop` | Integración del siguiente entregable. | `main` | `main` |
| `codex/feature/<descripcion>` | Funcionalidad del MVP. | `develop` | `develop` |
| `codex/fix/<descripcion>` | Corrección durante el desarrollo. | `develop` | `develop` |
| `codex/docs/<descripcion>` | Documentación. | `develop` | `develop` |
| `codex/chore/<descripcion>` | Estructura, configuración o mantenimiento. | `develop` | `develop` |
| `codex/release/<version>` | Estabilización de una entrega. | `develop` | `main` y luego reintegración en `develop` |
| `codex/hotfix/<descripcion>` | Corrección urgente de una versión estable. | `main` | `main` y luego reintegración en `develop` |

Usar minúsculas y guiones, sin espacios ni tildes; por ejemplo, `codex/docs/estrategia-gitflow` o `codex/feature/rf-03-sincronizacion-manual`. El prefijo `codex/` es la convención de las ramas de trabajo de este repositorio.

Mantener ramas breves y centradas en un cambio revisable. Evitar ramas permanentes por integrante o por componente: las contribuciones se atribuyen mediante commits y pull requests.

## Flujo de trabajo

1. Definir la tarea, su alcance y el RF/RNF aplicable, si existe.
2. Actualizar la rama base y crear una rama de trabajo.
3. Realizar commits pequeños y coherentes; incluir documentación y pruebas cuando correspondan.
4. Abrir un pull request hacia la base indicada en la tabla y solicitar revisión a otro integrante.
5. Resolver observaciones y conflictos en la rama de trabajo; repetir la validación afectada.
6. Integrar después de una aprobación y de las verificaciones aplicables. El autor no aprueba su propio cambio.
7. Eliminar la rama de trabajo después de su integración y registrar la contribución y sus evidencias.

Ejemplo para una rama documental, cuando `develop` ya exista en el remoto:

```bash
git switch develop
git pull --ff-only origin develop
git switch -c codex/docs/estrategia-gitflow
# Editar y revisar los archivos.
git add docs/gitflow.md CONTRIBUTING.md README.md
git diff --cached --check
git commit -m "docs(gitflow): documentar estrategia de ramas y convenciones"
git push -u origin codex/docs/estrategia-gitflow
```

Si aún no existe `develop`, el responsable de integración debe crearla desde el estado acordado de `main` y publicarla antes de aplicar este flujo. Esta guía documenta el procedimiento; no crea ramas ni modifica la configuración del remoto.

## Convención de commits

Formato:

```text
tipo(scope): descripción breve en infinitivo

Cuerpo opcional: motivo, comportamiento y restricciones relevantes.

Refs: RF-03, RNF-03
```

El `scope` identifica el área concreta y se omite si el cambio es transversal. Las referencias solo se agregan si corresponden realmente al cambio.

| Tipo | Uso |
| --- | --- |
| `feat` | Nueva funcionalidad. |
| `fix` | Corrección de un defecto. |
| `docs` | Documentación. |
| `test` | Pruebas y sus datos ficticios. |
| `refactor` | Reorganización sin cambiar el comportamiento. |
| `chore` | Mantenimiento y estructura del repositorio. |
| `build` | Dependencias y construcción. |
| `ci` | Integración continua. |
| `style` | Formato sin cambios de comportamiento. |
| `perf` | Mejora de rendimiento. |
| `revert` | Reversión de un cambio, identificando el commit y el motivo. |

Scopes sugeridos: `repo`, `gitflow`, `frontend`, `moodle`, `academic-data`, `access`, `agent`, `infra` y `tests`. Estos nombres no implican que los componentes estén implementados.

Ejemplos:

```text
chore(repo): crear estructura frontend backend docs e infra
docs(gitflow): documentar estrategia de ramas y convenciones
feat(moodle): incorporar sincronización manual de cursos
test(access): verificar restricciones de acceso por curso
```

Para cambios incompatibles en contratos existentes, usar `!` después del scope y explicar la migración en un pie `BREAKING CHANGE:`. No mezclar funcionalidades futuras con cambios de estructura o documentación.

## Pull requests y revisión

Usar un título con el mismo formato de los commits. La descripción debe indicar:

- Problema, alcance y resultado del cambio.
- Tarea y RF/RNF relacionados, cuando correspondan.
- Validación realizada y evidencia reproducible; si no hay pruebas ejecutables aplicables, explicarlo.
- Impacto en contratos, configuración, persistencia y permisos, cuando exista.
- Limitaciones o trabajo pendiente fuera del alcance.

Para cambios documentales, verificar enlaces, coherencia y `git diff --check`. Para código, ejecutar las pruebas relevantes disponibles. La especificación contempla pruebas de indicadores, API y permisos; cada cambio debe aportar la validación que corresponda, sin afirmar que existen herramientas todavía no configuradas.

Preferir merge commits para conservar los commits y su autoría. No integrar commits de trabajo incompletos: limpiar la rama antes de la revisión, sin reescribir ramas compartidas. Si se acuerda squash para una rama con varios autores, conservar la atribución mediante `Co-authored-by:` con identidad autorizada y enlaces al pull request.

## Entregas y correcciones urgentes

Crear una rama `release` solo cuando el incremento esté listo para estabilizarse; incluir únicamente correcciones, pruebas y documentación de esa entrega. Tras la revisión, integrarla en `main`, etiquetar el commit entregado con `vMAJOR.MINOR.PATCH` (por ejemplo, `v0.1.0` para un primer MVP) y reintegrar los cambios en `develop` mediante pull request. Registrar alcance, limitaciones y evidencias de la versión.

Un `hotfix` sigue la misma revisión y validación, parte de `main` y debe reintegrarse en `develop` para evitar perder la corrección. Una entrega semanal solo se publica si supera sus criterios de aceptación; el calendario no sustituye la verificación.

## Historial, seguridad y contribuciones

- No hacer push directo ni force push a `main` o `develop`. En ramas compartidas, resolver errores con nuevos commits o una reversión revisada.
- No versionar tokens de Moodle, secretos, archivos `.env` locales ni datos personales reales. Usar configuración de ejemplo sin secretos y datos ficticios o anonimizados para pruebas y evidencias.
- Revisar el diff antes de publicar: `.gitignore` no protege secretos ya rastreados. Si se publica una credencial, revocarla o rotarla y coordinar la limpieza del historial; borrarla en un commit posterior no elimina la exposición.
- Registrar las contribuciones en `docs/evidence/contributions.md`, al incorporar trabajo, con integrante, fecha, tarea o RF/RNF, enlaces a commits o pull requests y validación. Crear ese registro al documentar la primera contribución; no inventar integrantes ni evidencias.
- La revisión, documentación, seguridad e integración son responsabilidades compartidas, aunque cada integrante tenga un área principal.

## Configuración recomendada del remoto

Cuando se configure el alojamiento, proteger `main` y `develop`: exigir pull request, al menos una aprobación de otro integrante, resolución de conversaciones y bloqueo de force push y borrado. Exigir los checks de CI aplicables cuando existan y se hayan comprobado sus nombres.

Estas son reglas de trabajo documentadas. Las protecciones y los checks no quedan activados por este commit; mientras no existan, el equipo debe aplicar la revisión y validación manualmente.
