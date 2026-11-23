# Sprint 6 — Cierre backend M05/M08/M09 (en curso)

**Rama:** `feature/sprint-06-backend-integral`
**Alcance:** HU-033, 036, 048, 049, 067, 068, 069, 070, 071, 086, 089, 092, 093,
094 y 095 (15 HU de la hoja de Sprint Planning). **Fuera de alcance:** M10, M11,
M12 y M13.

Este documento se completa por pasos, igual que Sprints 1–5. Hoy cubre los
pasos 1–4 (controles transversales, M05 Mis propiedades, M09 Ocupación, M08
Chat); la matriz final de las 15 HU (paso 5) se añade al cerrar.

## 1. Estado por paso

| Paso | Alcance | Estado |
|---|---|---|
| 1 | HU-071, HU-086 (controles transversales ya cubiertos desde Sprint 3) | Confirmado, sin cambios de código |
| 2 | HU-033, HU-036, HU-092 (M05: Mis propiedades) | Implementado |
| 3 | HU-048, HU-049, HU-093, HU-094, HU-095 (M09: Ocupación) | Implementado |
| 4 | HU-067, HU-068, HU-069, HU-070, HU-086, HU-089 (M08: Chat) | Implementado |
| 5 | Cierre, matriz de las 15 HU y PR a `main` | Pendiente |

## 2. Decisiones provisionales de M09 (sin aprobación de producto)

Estas decisiones se tomaron para poder implementar M09 sin inventar un
contrato que la spec no fija. Quedan explícitamente marcadas como
provisionales, igual que otros umbrales de Sprints 1–5 (`VerificationCode.MaxConfirmationAttempts`,
`PasswordResetToken.MaxAttempts`, los límites de `Auth:RateLimit`):

- **Vigencia del código de invitación: 7 días.** No hay un valor fijado por la
  spec. Se eligió por analogía con el resto de enlaces temporales del
  proyecto, pero es más largo que los de seguridad (p. ej. 30 minutos de
  recuperación de contraseña) porque el código de invitación no es un
  secreto de un solo uso: se comparte verbalmente o por mensaje con un
  prospecto de inquilino y debe seguir funcionando varios días. El valor
  exacto (7 días) no está validado con el equipo de producto.
- **El código es reutilizable mientras la unidad tenga cupo, no de un solo
  uso.** Se decidió así porque una unidad compartida (`Occupancy.Compartida`,
  hasta 50 lugares) necesita que varias personas se unan con el mismo código;
  convertirlo en un código de un solo uso habría obligado a generar y
  distribuir uno distinto por cada lugar, lo que no se pidió y complica la UX
  sin que la spec lo exija. El código no se "gasta": `GET /invitacion` es
  idempotente (devuelve el vigente) y solo se genera uno nuevo si no hay uno o
  venció. El límite real de cuántas personas pueden entrar lo da
  `unidades.lugaresOcupados < lugares`, no el código.
- **Confirmación obligatoria (`confirmacion: true`) en HU-094 y HU-095.** Se
  exigió en el cuerpo, verificada en el backend y no solo en la UI, replicando
  el patrón ya usado en la eliminación de cuenta (`confirmacionPerdidaInformacion`,
  HU-096). La razón es la misma: dar de baja a un inquilino o abandonar una
  unidad son acciones que no se pueden deshacer con un endpoint propio (no hay
  "reingreso automático"; hay que volver a usar el código de invitación), así
  que el backend no confía en que el cliente ya confirmó.

Ninguna de las tres decisiones tiene aprobación de producto registrada; si el
equipo de producto fija otro valor (por ejemplo, una vigencia distinta o un
código de un solo uso), requiere una historia de cambio explícita porque hoy
está codificado en `UnitInvitation.ValidityDays` y en la ausencia de un
"consumo" del código.

## 3. Protección de cupo con CAS (HU-049)

`unidades.lugaresOcupados` es un contador atómico mantenido únicamente por
`IUnitRepository.TryReserveSlotAsync`/`ReleaseSlotAsync`
(`findOneAndUpdate` con filtro `lugaresOcupados < lugares` e `$inc`). Dos
solicitudes de unión simultáneas con un solo lugar libre no pueden reservar
ambas: MongoDB aplica el incremento a una sola. Los vínculos de ocupación
(`vinculos_ocupacion`) no se usan para contar cupo —contar documentos ahí
sería una condición de carrera entre el conteo y la inserción—, solo para el
detalle por inquilino (`GET /inquilinos`, HU-094).

Cobertura de la protección:

- Prueba de concurrencia local (`JoinUnitWithCodeServiceTests.JoinAsync_DosSolicitudesSimultaneasConUnLugar_SoloUnaDebeVincularse`),
  con hilos reales del pool (`Task.Run`) contra el repositorio en memoria con
  `lock`.
- Prueba de concurrencia a nivel HTTP (`UnitOccupancyEndpointTests.UnirseConCodigo_DosSolicitudesSimultaneasConUnLugar_SoloUnaDebeVincularse`),
  con dos `HttpClient` independientes contra el mismo `TestServer`.
- Prueba real y opt-in contra MongoDB (`MongoOccupancyPersistenceTests.TryReserveSlotAsync_DosSolicitudesSimultaneasConUnLugar_SoloUnaDebeReservar`),
  que prueba el `findOneAndUpdate` real, no simulado. Sigue **BLOCKED** en el
  entorno local sin `mongod`, igual que el resto de pruebas reales del
  proyecto; debe ejecutarse en `mongo-db-test` con autorización.

**Campo `lugaresOcupados` ausente (unidades existentes antes de M09-001):**
el filtro de ambos métodos usa `$ifNull: [lugaresOcupados, 0]` de forma
explícita, así que una unidad sin el campo se trata como 0 lugares ocupados
(`TryReserveSlotAsync` la deja reservar; `ReleaseSlotAsync` nunca la toca,
porque `0 > 0` es falso, de modo que el contador tampoco puede quedar
negativo). En este sprint el único escritor de `lugaresOcupados` es este
mismo repositorio (vía `TryReserveSlotAsync`), así que no puede existir hoy
una unidad con vínculos activos reales y el campo ausente; no se necesitó un
backfill en la migración porque no hay datos legados que reconciliar, solo
el caso de un documento nunca tocado por esta funcionalidad. Cobertura:
`MongoOccupancyPersistenceTests.TryReserveSlotAsync_UnidadSinCampoLugaresOcupados_LoTrataComoCero`
(real y opt-in, borra el campo con `$unset` para simular una unidad previa a
M09-001) y `LeaveUnitServiceTests.ReleaseSlotAsync_SinLugaresOcupados_NuncaQuedaNegativo`
(repositorio en memoria, confirma que soltar dos veces se queda en 0).

## 4. HU-094: cómo se obtiene el `tenantAccountId`

El propietario no podía dar de baja a un inquilino concreto sin conocer su
`accountId`, y no existe ninguna otra ruta que lo exponga (el perfil no
incluye `id`, y `GetMyPropertiesService`/`vista-previa` tampoco). Se agregó
`GET /api/v1/unidades/{unitId}/inquilinos` (owner-only) que lista los
inquilinos **activos** de la unidad con `accountId`, `nombreCompleto` y
`fechaVinculo`; nunca correo ni teléfono. El fixture y el driver real de ZAP
usan este endpoint para obtener el id antes de dar de baja, en vez de leer el
claim `sub` del JWT (que era un atajo exclusivo del script de pruebas, no algo
que Android pudiera hacer).

## 6. M08 Chat: diseño y cobertura

- **Identificador determinista en vez de backfill o CAS nuevo.** El `_id` de
  `conversaciones` se calcula con SHA-256 de `unidad|propietario|solicitante`
  (`Conversation.ComputeId`): mismo par (unidad, propietario, solicitante) →
  mismo `_id`, siempre. No hace falta inventar un índice único nuevo (fuera
  de lo ya observado en Atlas, solo `ix_conversaciones_participantes`):
  la unicidad nativa de `_id` en Mongo basta, igual que el `_id` compuesto
  `inquilino:unidad` de `vinculos_ocupacion`. Dos solicitudes simultáneas
  para iniciar la misma conversación no pueden duplicarla; el riesgo es
  menor que el de HU-049 (cupo), así que no se justificaba una protección
  CAS adicional, solo esta elección de identificador.
- **Límite de frecuencia con fail-open deliberado.** 30 mensajes/minuto por
  cuenta (umbral provisional), usando el mismo `IDistributedRateLimiter`
  que `Auth:RateLimit`. Si el almacén distribuido no está disponible, el
  mensaje se envía igual: a diferencia del cupo de unidades, este límite no
  protege una invariante de negocio, así que se prefiere disponibilidad del
  chat sobre bloquear todo por una caída de Redis.
- **Paginación por cursor de fecha**, no por `skip/take`: `GET /mensajes`
  devuelve lo más reciente primero y un `siguienteCursor` (fecha del mensaje
  más antiguo de la página) para pedir el historial anterior, aprovechando
  el índice compuesto `ix_mensajes_conversacion_fecha`.
- **HU-070 por omisión, no por enmascarado.** Las vistas de chat
  (`ConversationView`, `MessageView`, `ConversationSummaryView`) solo
  proyectan `AccountId`/`FullName`; nunca referencian `Email` ni un campo de
  teléfono (que no existe en `Account`). Mismo patrón que
  `UnitTenantView` (HU-094).
- **HU-089**: toda respuesta de envío/lectura de mensajes incluye
  `"aviso": "Comunicación cifrada en tránsito."`, agregado en la capa Api
  (no en Domain/Application) porque es texto de presentación, no una regla
  de negocio.
- Migración `M08-001`: crea `conversaciones` (`ix_conversaciones_participantes`
  sobre el arreglo `participantes`) y `mensajes`
  (`ix_mensajes_conversacion_fecha`, compuesto), respetando los índices ya
  observados en Atlas. Como M09-001, no declara backfill porque la
  migración versionada de este proyecto solo declara colecciones e índices,
  nunca pasos de datos.
- Cobertura: pruebas de dominio (`ChatDomainTests`), de los 4 servicios de
  Application con repositorios en memoria (`StartConversationServiceTests`,
  `SendMessageServiceTests` incluyendo límite de frecuencia y fail-open,
  `ListConversationMessagesServiceTests`, `ListMyConversationsServiceTests`),
  de los 4 endpoints HTTP (`ChatEndpointTests`, incluyendo HU-070 sin correo
  ni teléfono en el cuerpo de la respuesta) y una prueba real y opt-in
  contra MongoDB (`MongoChatPersistenceTests`), **BLOCKED** localmente sin
  `mongod` igual que el resto de pruebas reales del proyecto. Catálogo
  OpenAPI actualizado de 48 a 52 operaciones; fixture y driver real de ZAP
  actualizados para cubrir los 4 endpoints nuevos.

## 7. Pendientes

1. **CI (paso 5):** `.github/workflows/ci.yml` no se modificó en este paso.
   Los jobs manuales `mongo-db-test`, `dast-zap` y `restler-fuzzing` siguen
   aplicando migraciones solo hasta `M07-001`; sus pasos de inicialización de
   base efímera deben extenderse con `M08-001` y `M09-001` antes de poder
   ejecutarlos. Mientras tanto, si alguno de esos jobs se dispara tal como
   está, los endpoints de M08/M09 fallarían contra una base real por falta
   de las migraciones.
2. Matriz final de las 15 HU, evidencia de pruebas consolidada y PR a `main`
   quedan para el cierre (paso 5).
3. Las tres decisiones provisionales de la sección 2 no tienen aprobación de
   producto.
4. `M08-001` y `M09-001` no se aplicaron ni promovieron a Atlas.
