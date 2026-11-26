# Agentes de IA, skills y MCP en el desarrollo de UniHome Backend

Este documento describe, sin adornos, qué herramientas de IA se usaron
realmente para construir y operar este backend, para que la auditoría
pueda verificarlo contra el repositorio (commits, PR, `.claude/`) en vez de
tomar una lista genérica de "posibles" herramientas.

## Agente principal: Claude Code (terminal)

Claude Code, ejecutado en la terminal sobre este repositorio, es el agente
que escribe código, pruebas y documentación, corre `dotnet build`/`dotnet
format`/`dotnet test`, crea ramas, hace commits y abre PR con `gh pr
create`. Evidencia verificable:

- Coautoría explícita en los mensajes de commit:
  `Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>` (ver, por
  ejemplo, `cdb431a`, `bb7bad9` y los commits de este mismo bloque de
  auditoría en `git log`).
- `.claude/settings.local.json`: lista de `deny` que acota lo que el agente
  puede ejecutar sin confirmación humana explícita (merges de PR, push a
  `main`, operaciones destructivas de git, cambios a la Container App de
  producción, `dotnet run` de promoción de migraciones, `dotnet
  user-secrets`). Este archivo nunca se versiona (está en `.gitignore`), así
  que sus reglas son locales a cada máquina de trabajo, pero el patrón es el
  mismo en todas: nada irreversible sin confirmación.
- `.claude/skills/`: las skills del proyecto (ver abajo), que documentan
  flujos que el agente ya seguía de facto en sprints anteriores.

El agente nunca ejecuta merges de PR, push directo a `main`, ni operaciones
contra infraestructura productiva de Azure sin que la responsable de la
cuenta lo autorice explícitamente para esa ventana de trabajo —
precedente documentado en `docs/sprint-08-cierre.md` §13.4 ("Candados
temporales").

## Claude en Chrome (automatización de navegador)

Se usa para tareas que viven fuera de este repositorio pero que informan el
trabajo en él:

- La hoja de **Sprint Planning** (seguimiento de HU, sprints y pendientes
  entre sesiones).
- El **portal de Azure** (consulta de estado de recursos en `rg-unihome-dev`
  — revisiones de Container Apps, ACR, Log Analytics — como verificación
  visual complementaria a los comandos `az` documentados en
  `docs/sprint-0X-cierre.md`).
- **MongoDB Atlas** (consulta del estado real de colecciones/índices como
  verificación cruzada de lo que reporta `UniHome.MongoMigrator`).
- **GitHub** (revisión de PR, checks de CI y Dependabot desde la interfaz
  web cuando es más rápido que `gh`).

Claude en Chrome actúa siempre sobre un navegador ya autenticado por la
persona responsable; no crea ni gestiona credenciales, y no se usa contra
el panel de administración de UniHome en producción.

## CLI de GitHub (`gh`)

Usada por el agente principal desde la terminal para: crear PR
(`gh pr create`), consultar su estado y sus checks de CI
(`gh pr view`/`gh pr checks`), leer comentarios y revisiones
(`gh api repos/.../pulls/.../comments`), y consultar los PR de Dependabot.
Nunca para `gh pr merge` (bloqueado explícitamente en
`.claude/settings.local.json`).

## MCP

No se usa ningún servidor MCP de terceros contra este repositorio ni contra
infraestructura productiva. La automatización de navegador (Claude en
Chrome) y las herramientas de archivos/shell del agente principal cubren el
flujo real descrito arriba; si en el futuro se incorpora un MCP real (por
ejemplo, para Azure o Atlas), debe documentarse aquí con la misma evidencia
verificable que el resto de esta página, no antes.

## Skills del proyecto (`.claude/skills/`)

Cada skill documenta un flujo que este proyecto ya seguía en los sprints
anteriores (visible en `git log` y en `docs/sprint-0X-cierre.md`); no son
flujos aspiracionales.

| Skill | Cuándo se dispara | Qué fija |
|---|---|---|
| `nuevo-endpoint` | Agregar/cambiar una operación de la API | spec primero en `specs/`, orden Domain→Application→Infrastructure→Api, pruebas unitarias+integración+contrato OpenAPI, puerta de calidad local, commit en español, PR sin merge |
| `promocion-migracion` | Llevar una migración de `UniHome.MongoMigrator` a una base real | `audit`/`plan`/`verify` de solo lectura siempre primero, credencial exclusiva de migración, aprobación humana con `--approval-ref`, diagnóstico sin escrituras ni datos sensibles si el preflight rechaza |
| `despliegue-azure` | Desplegar una imagen nueva a la Container App de `rg-unihome-dev` | build/push con etiqueta por SHA de commit, verificación explícita (salud, rutas nuevas, códigos esperados) antes de considerar el despliegue exitoso, rollback preparado pero no ejecutado salvo falla |
| `revision-seguridad` | Antes de un PR sensible o al preparar una auditoría de seguridad | checklist OWASP API Top 10 por endpoint, herramientas reales del repo (Gitleaks, SonarCloud, ZAP, RESTler, Dependabot) y qué evidencia guardar |

## Convención de verificación

Nada en este documento describe una herramienta "disponible en teoría": cada
fila tiene su propia evidencia citada arriba o en `Memoria.md`. Si una
herramienta deja de usarse o cambia de flujo, este documento se actualiza en
el mismo PR que hace el cambio, no después.
