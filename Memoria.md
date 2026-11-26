# Memoria del proyecto

Este documento recoge aprendizajes, decisiones e incidentes reales ocurridos
durante el desarrollo de UniHome Backend, con sus commits/PR como evidencia.
Es memoria institucional para quien retome el proyecto (incluido el auditor
de la sección Backend de la «Auditoría de evaluación v2»): no repite lo que
ya documentan `README.md`, `docs/sprint-0X-cierre.md` o `docs/*.md`, solo
señala dónde mirar y qué se aprendió.

## Convenciones del proyecto

- **Idioma:** código, rutas, DTOs y mensajes de error en español (dominio de
  negocio); commits y PR también en español. Comentarios XML/código fuente
  mezclan español e inglés técnico según el archivo existente; no se fuerza
  una migración retroactiva.
- **Arquitectura:** Clean Architecture estricta — `Domain` sin dependencias
  externas, `Application` solo depende de `Domain`, `Infrastructure` depende
  de `Application`/`Domain`, `Api` compone ambas. Verificado por
  `tests/UniHome.UnitTests/Architecture/ArchitectureTests.cs`.
- **Persistencia:** MongoDB vía `MongoDB.Driver`, aislado detrás de puertos
  de `Application`. Cada módulo que crea colecciones nuevas lo hace con una
  migración versionada (`tools/UniHome.MongoMigrator`), nunca con
  `EnsureCollection`/`CreateIndex` ad hoc en la API; los adaptadores
  verifican el esquema en modo lectura al arrancar y **fallan cerrado**
  (`503`) si falta una colección o un índice esperado — ver «El 503 del
  schema guard» abajo.
- **Rama por bloque, PR sin merge automático:** cada unidad de trabajo vive
  en su propia rama desde `main` (`fix/...`, `feature/...`, `docs/...`,
  `security/...`, `infra/...`) y se integra por PR; el merge lo decide la
  responsable de la cuenta (nunca `gh pr merge` automático — está en el
  `deny` de `.claude/settings.local.json`).
- **Puerta de calidad antes de cada commit:** `dotnet build -warnaserror`,
  `dotnet format --verify-no-changes` y la suite completa (unitarias +
  integración + Python de `tools/ci`) en verde. En CI además: CodeQL ×2,
  Semgrep, Gitleaks, SonarCloud (0 avisos nuevos, hotspots revisados,
  cobertura de línea nueva >80 %) y el PR Quality Gate.
- **Nunca secretos en el repo.** Configuración sensible vía User Secrets o
  variables de entorno de Container Apps; `.env*`, `scratch/` y
  `.claude/settings.local.json` están en `.gitignore`. Ver «La credencial
  expuesta en `scratch/`» abajo para la vez que esto falló.
- **Decisiones de producto pendientes se marcan explícitamente** en el
  código/README como «provisional, sin aprobación de producto» en vez de
  fijar un valor silencioso; varias (`DEC-09`, `DEC-10`, `ADR-005`) siguen
  abiertas — ver `README.md` y `docs/sprint-0{3,4,5}-cierre.md`.

## Incidentes y aprendizajes

### 1. La contraseña compartida entre el migrador y la API

**Qué pasó:** el primer despliegue de Sprint 6 (imagen `34c69ae`, sufijo de
revisión `sprint06-20261124`) nunca llegó a `Ready`: el health check de
almacenamiento fallaba con `MongoAuthenticationException`. Un rollback de
control a la imagen que ya corría sana dio exactamente el mismo error,
confirmando que la causa no era el código ni la imagen nueva.

**Causa raíz:** el usuario de Atlas `unihome_verificacion_migrador` estaba
compartido entre `MongoDb__ConnectionString` (secreto de Container Apps, lo
usa la API) y `MongoDb:MigrationConnectionString` (User Secrets local, lo usa
el migrador). La protección del migrador que rechaza promover con la misma
identidad en ambas rutas no cubre este caso: compara las dos cadenas que el
migrador conoce *en el momento de un `promote`*, no un cambio posterior y
directo sobre el usuario de Atlas hecho fuera de esa sesión (alguien rotó o
tocó la contraseña del usuario compartido por otra vía).

**Impacto:** dominio público sin responder; el modo `Single` de revisiones
activas de Container Apps impidió redirigir tráfico a la revisión sana
existente mientras se diagnosticaba.

**Resolución:** corrección del secreto de Container Apps + reinicio de la
revisión activa, sin desplegar una imagen nueva.

**Aprendizaje:** un usuario de base de datos compartido entre dos procesos
con ciclos de vida de credencial distintos (API de larga vida vs. migrador
de ejecución puntual) es una dependencia oculta que ningún `diff` de código
detecta. Queda como pendiente separar el usuario operativo de Atlas del
usuario de migraciones, cada uno con privilegios mínimos.

**Evidencia:** commit `ea1516a` (`docs(sprint-06): registrar despliegue de
Sprint 6 e incidente de credencial compartida`), detalle completo en
`docs/sprint-06-cierre.md`.

### 2. El 503 del schema guard

**Qué pasó:** `GET /api/v1/conversaciones`, `GET /api/v1/notificaciones` y
`GET /api/v1/unidades/{unitId}/renta/mis-pagos` respondían `503` en vez de
`401` cuando se llamaban sin sesión. La causa no era de autorización: el
adaptador de MongoDB de esos módulos falla cerrado con `503` si al arrancar
no encuentra la colección/índice que espera (diseño intencional: nunca
operar silenciosamente contra un esquema divergente). El problema real era
de **orden de evaluación** — el guard de esquema se ejecutaba antes de que
`[Authorize]` tuviera oportunidad de cortar la petición sin sesión con `401`.

**Resolución:** PR #63 ("el fix de Omar") reordena la evaluación para que la
autorización se resuelva primero; confirmado en producción comparando la
revisión anterior (`unihome-api--fix-guard-20261125`, ya con el fix) contra
la revisión de despliegue de Sprint 8: `conversaciones` /`notificaciones`/
`renta/mis-pagos` responden `401` sin sesión, no `503`.

**Aprendizaje:** "fallar cerrado" ante un esquema inesperado es correcto,
pero el orden de los middlewares/filtros importa tanto como la lógica de
cada uno: un guard de infraestructura nunca debe ejecutarse antes que la
autenticación, o un `503` filtra información (que el backend intentó tocar
Mongo) a un llamador no autenticado.

**Evidencia:** `docs/sprint-08-cierre.md` §13.3, tabla de verificación
posterior al despliegue (líneas con "antes `503`").

### 3. El orden de promoción de M11 frente a M12

**Qué pasó:** al promover la migración `M11-001` (reparaciones) en
producción, el preflight la rechazó: *"Colección no administrada por las
migraciones anteriores: notificaciones. Versión de ledger no registrada o
posterior a la migración: M12-001."* `M12-001` (notificaciones) ya estaba
promovida desde Sprint 7, pero el número de módulo ordena *después* de
`M11-001` alfabéticamente — M11 no existía cuando M12 se diseñó y se
desplegó, así que el número de módulo no refleja el orden real de
despliegue. `MongoPromotionPreflightEvaluator` solo construía las
colecciones permitidas con `registry.GetUpTo(versión)`, que excluye
cualquier migración posterior en el orden del registro aunque ya esté
aplicada y sea válida en la base real.

**Diagnóstico sin tocar Atlas ni imprimir datos sensibles:** se agregó
temporalmente un `Console.Error.WriteLine` de las "issues" del preflight
directamente en el código (no en una copia desplegada), se reconstruyó el
binario localmente, se confirmó la causa exacta, y el cambio de diagnóstico
se revirtió de inmediato antes de cualquier escritura en Atlas.

**Resolución (PR #66, rama `fix/migrator-orden-promocion`):**
`MongoPromotionPreflightEvaluator` ahora también admite las colecciones de
una migración posterior si, y solo si, esa migración está registrada en el
ledger con el checksum esperado y cada una de sus colecciones existe con
exactamente los índices y el validador declarados (`IsAppliedAndValid`).
Cualquier otra colección —desconocida, o de una migración posterior no
aplicada o divergente— sigue rechazada igual que antes. Tres pruebas nuevas
en `MongoPromotionIncrementalTests` cubren los tres casos.

**Aprendizaje:** numerar migraciones por módulo funcional (M01…M13) es
legible para negocio, pero no garantiza el orden real de despliegue; una
herramienta de promoción no debe asumir que "posterior en el número" implica
"posterior en el tiempo". M11-001 no se escribió en Atlas hasta que el fix
estuvo en `main` (disciplina de "nada se promueve sin el fix mergeado").

**Evidencia:** `docs/sprint-08-cierre.md` §13.2; PR #66 (CI verde completo,
SonarCloud `new_coverage` 88.4 %, 0 avisos nuevos).

### 4. El falso positivo de Gitleaks con etiquetas de imagen ACR

**Qué pasó:** el PR #62 falló en Gitleaks (y por tanto en el PR Quality
Gate): la regla `generic-api-key` marcó 4 veces las etiquetas de imagen ACR
(`acrunihomedev.azurecr.io/unihome-api:<SHA de 40 hex>`) dentro de
`docs/sprint-07-cierre.md`. No eran secretos: son referencias de trazabilidad
de despliegue (qué imagen se desplegó), con la misma forma hexadecimal larga
que dispara la heurística genérica.

**Resolución:** `.gitleaks.toml` en la raíz con `[extend] useDefault = true`
(ninguna regla default se desactiva ni se relaja) más un único `allowlist`
acotado con `AND` (`targetRules=["generic-api-key"]`, `regexTarget="line"`,
regex específicas para la etiqueta ACR y el digest `sha256:...`,
`paths=["docs/.*\.md"]`). Probado localmente con el mismo binario fijado en
CI (gitleaks 8.30.1, `--no-git` contra un directorio aislado): la etiqueta y
el digest dentro de `docs/*.md` dejan de marcarse; la misma etiqueta fuera de
`docs/*.md` sigue marcada; un secreto genérico distinto o un token de Stripe
dentro de `docs/*.md` siguen marcados por sus reglas correspondientes.
Repetido el escaneo completo del historial real: 0 hallazgos (antes: 4).

**Aprendizaje:** un *allowlist* de Gitleaks debe acotarse por regla +
patrón + ruta a la vez (`AND`), nunca solo por ruta — de lo contrario se
abre la puerta a que un secreto real futuro en esos mismos archivos pase
desapercibido. Verificar siempre contra el binario y la versión exactos de
CI, no una instalación local distinta.

**Evidencia:** commit `cdb431a` (`ci(gitleaks): permitir etiquetas de imagen
ACR y digests en docs`); un caso previo relacionado (huellas de un commit de
otra rama, también etiqueta de imagen, no secreto) se documentó en
`.gitleaksignore` vía commit `bb7bad9`.

### 5. La credencial expuesta en `scratch/`

**Qué pasó:** el commit `0d7ba2a` (`chore(scratch): add diagnostic scripts,
verification tools, and seed utilities`) agregó a `main` ~20 scripts
temporales de diagnóstico, entre ellos `scratch/update_passwords.py`, que
contenía **en texto plano** la cadena de conexión completa de MongoDB Atlas
(usuario y contraseña del usuario `unihome0412_db_user`) usada para
sembrar/actualizar cuentas de prueba en `unihome_desarrollo`. El commit llegó
a `main` y por tanto al remoto público antes de detectarse.

**Resolución inmediata:** PR #64 (`fix/remove-scratch-from-main`, commit
`b03104a`) eliminó los ~20 scripts de `scratch/` de `main` y agregó
`scratch/` a `.gitignore` para impedir que vuelva a ocurrir. La credencial de
Atlas expuesta se rotó (ya no es válida) una vez detectado el incidente.

**Riesgo residual documentado, no escondido:** eliminar los archivos en un
commit posterior **no borra el historial** — `0d7ba2a` sigue siendo
ancestro de `main` (confirmado con
`git merge-base --is-ancestor 0d7ba2a main`) y la cadena de conexión sigue
presente en el historial de git tal cual se subió. Rotar la credencial quita
el riesgo de uso indebido, pero el texto del secreto (ya inválido) sigue
siendo recuperable por quien clone el repositorio completo. El Bloque 4 de
esta auditoría (seguridad) añade a `.gitleaks.toml` una regla para URIs
`mongodb(+srv)://usuario:contraseña@` y, si la rotación ya ocurrió, registra
esta huella en `.gitleaksignore` con su justificación — nunca se oculta el
hallazgo, se documenta como mitigado.

**Aprendizaje:** `scratch/`, por definición, no debería existir en `main`.
Cualquier script de diagnóstico o seed con una credencial real debe vivir
fuera del repositorio (o usar una variable de entorno/placeholder) desde el
primer commit, no corregirse después de que ya es público. Un secreto en
git history es, a efectos prácticos, un secreto que no se puede "borrar" sin
reescribir el historial (`filter-repo`/BFG) — algo que este proyecto no ha
hecho y que no se hace sin aprobación explícita.

**Evidencia:** commits `0d7ba2a` (introduce el secreto) y `b03104a` / PR #64
(lo elimina del árbol de trabajo); verificación de ancestría con
`git merge-base --is-ancestor`.

## Dónde mirar para más contexto

- Decisiones de producto/seguridad abiertas: `DEC-09`, `DEC-10`, `ADR-005`
  (buscar en `README.md` y `docs/sprint-03-cierre.md`,
  `docs/sprint-04-cierre.md`, `docs/sprint-05-cierre.md`).
- Cierre y matriz de HU por sprint: `docs/sprint-01-cierre.md` …
  `docs/sprint-08-cierre.md`.
- Especificación operación por operación (HU, seguridad, pruebas): `specs/`.
- Agentes de IA, skills y MCP usados en este proyecto: `docs/ia-desarrollo.md`.
