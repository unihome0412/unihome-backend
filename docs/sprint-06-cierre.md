# Sprint 6 — Cierre backend M05/M08/M09

**Rama:** `feature/sprint-06-backend-integral`
**Base:** `2d3022b` (Sprint 5 en `main`, PR #54)
**Alcance:** HU-033, 036, 048, 049, 067, 068, 069, 070, 071, 086, 089, 092, 093,
094 y 095 (15 HU de la hoja de Sprint Planning). **Fuera de alcance:** M10, M11,
M12 y M13 (quedan para Sprint 7/8, fuera de este cierre).

## 1. Estado de las 15 HU

| HU | Módulo | Estado | Entrega backend | Pruebas |
|---|---|---|---|---|
| HU-071 | M04 (ya existente) | Confirmado, sin cambios de código | Ficha/tarjeta/favoritos nunca exponen correo ni teléfono del propietario (allowlist de campos desde Sprint 3) | Cubierto desde Sprint 3/5 |
| HU-086 | Transversal (ya existente + M08) | Confirmado para M01-M09; M08 lo cierra en chat | `[Authorize]` en todos los controladores; M08 lo exige en sus 4 endpoints nuevos | `RutasDeChat_SinSesion_DebenResponder401` + equivalentes de M05/M09 |
| HU-033 | M05 | Implementada | `GET /api/v1/mis-propiedades`: propiedades del titular con total de habitaciones, disponibles y estado por habitación; filtra por tipo y por "al menos una habitación" en el estado pedido | Unitarias `GetMyPropertiesServiceTests` + integración |
| HU-036 | M05 | Implementada (reutiliza M05) | Alta de propiedad reutiliza `POST /propiedades` existente; sin endpoint nuevo | Cubierto por las pruebas de M05 ya existentes |
| HU-092 | M05 | Implementada (reutiliza M05) | Alta de habitación reutiliza `POST /propiedades/{propertyId}/unidades` existente; sin endpoint nuevo | Cubierto por las pruebas de M05 ya existentes |
| HU-048 | M09 | Implementada | `GET /unidades/{unitId}/invitacion`: código de invitación vigente de una unidad propia (7 días, provisional) | Unitarias + integración `GetUnitInvitationService`/`UnitOccupancyEndpointTests` |
| HU-049 | M09 | Implementada | `POST /unidades/unirse`: vincula con código; cupo protegido con CAS de un solo documento (`lugaresOcupados`) | Unitarias, concurrencia local, concurrencia HTTP y prueba real opt-in contra MongoDB |
| HU-093 | M09 | Implementada | `PUT /unidades/{unitId}/mantenimiento`: marca/desmarca mantenimiento; no se sobreescribe mientras está activo | Unitarias `SetUnitMaintenanceServiceTests` + integración |
| HU-094 | M09 | Implementada | `DELETE /unidades/{unitId}/inquilinos/{tenantAccountId}` (con `confirmacion: true`) y `GET /unidades/{unitId}/inquilinos` para obtener el `tenantAccountId`; nunca correo ni teléfono | Unitarias `RemoveTenantServiceTests`/`ListUnitTenantsService` + integración |
| HU-095 | M09 | Implementada | `DELETE /unidades/{unitId}/mi-vinculo` (con `confirmacion: true`); usa el claim `sub`, nunca un id del cliente | Unitarias `LeaveUnitServiceTests` + integración |
| HU-067 | M08 | Implementada | `POST /unidades/{unitId}/conversaciones`: inicia o reutiliza la conversación con el propietario de una unidad aprobada; prohíbe contactar la propia unidad | Unitarias `StartConversationServiceTests` + integración + dominio |
| HU-068 | M08 | Implementada | `GET`/`POST /conversaciones/{conversationId}/mensajes`: solo participantes, límite de 1000 caracteres, límite de 30 mensajes/min por cuenta, paginación por cursor compuesto (fecha, id) | Unitarias `SendMessageServiceTests`/`ListConversationMessagesServiceTests` (incluye el caso de mensajes con la misma fecha) + integración |
| HU-069 | M08 | Implementada | `GET /conversaciones`: conversaciones propias con nombre del otro participante, último mensaje, hora y contexto de la unidad | Unitarias `ListMyConversationsServiceTests` + integración |
| HU-070 | M08 | Implementada | Las vistas de chat solo proyectan `AccountId`/`FullName`; nunca correo ni teléfono | Integración `ListarConversaciones_..._NuncaCorreoNiTelefono` |
| HU-089 | M08 | Implementada | Toda respuesta de envío/lectura de mensajes incluye `"aviso": "Comunicación cifrada en tránsito."` | Integración `EnviarMensaje_Participante_GuardaYDevuelveAvisoCifrado` |

## 2. Endpoints nuevos

| Método | Ruta | Autorización | Éxito | Errores |
|---|---|---|---|---|
| GET | `/api/v1/mis-propiedades` | Bearer titular | 200 | 401, 503 |
| GET | `/api/v1/unidades/{unitId}/invitacion` | Bearer propietario | 200 | 401, 403, 404, 409, 503 |
| POST | `/api/v1/unidades/unirse` | Bearer solicitante | 200 | 400, 401, 403, 409, 410, 413, 503 |
| PUT | `/api/v1/unidades/{unitId}/mantenimiento` | Bearer propietario | 200 | 401, 403, 404, 413, 503 |
| DELETE | `/api/v1/unidades/{unitId}/inquilinos/{tenantAccountId}` | Bearer propietario | 204 | 400, 401, 403, 404, 413, 503 |
| DELETE | `/api/v1/unidades/{unitId}/mi-vinculo` | Bearer inquilino | 204 | 400, 401, 404, 413, 503 |
| GET | `/api/v1/unidades/{unitId}/inquilinos` | Bearer propietario | 200 | 401, 403, 404, 503 |
| POST | `/api/v1/unidades/{unitId}/conversaciones` | Bearer solicitante | 200 | 401, 403, 404, 503 |
| GET | `/api/v1/conversaciones` | Bearer participante | 200 | 401, 503 |
| GET | `/api/v1/conversaciones/{conversationId}/mensajes` | Bearer participante | 200 | 401, 403, 404, 503 |
| POST | `/api/v1/conversaciones/{conversationId}/mensajes` | Bearer participante | 201 | 400, 401, 403, 404, 429, 503 |

`POST /propiedades` y `POST /propiedades/{propertyId}/unidades` (HU-036/HU-092)
no son endpoints nuevos: ya existían desde Sprint 2 (M05) y Sprint 6 solo los
reutiliza para el alta inicial de propiedad/habitación.

## 3. Decisiones provisionales (sin aprobación de producto)

Sin un valor fijado por la spec, igual estatus que otros umbrales de Sprints
1–5 (`VerificationCode.MaxConfirmationAttempts`, `PasswordResetToken.MaxAttempts`,
los límites de `Auth:RateLimit`):

- **Vigencia del código de invitación: 7 días** (`UnitInvitation.ValidityDays`,
  M09). Es más largo que los enlaces de seguridad (p. ej. 30 minutos de
  recuperación de contraseña) porque el código de invitación no es un secreto
  de un solo uso: se comparte verbalmente o por mensaje con un prospecto y
  debe seguir funcionando varios días.
- **El código de invitación es reutilizable mientras la unidad tenga cupo, no
  de un solo uso** (M09). Una unidad compartida (hasta 50 lugares) necesita
  que varias personas se unan con el mismo código; convertirlo en un código
  de un solo uso habría obligado a generar y distribuir uno distinto por cada
  lugar, lo que no se pidió. `GET /invitacion` es idempotente; el límite real
  de cuántas personas entran lo da `lugaresOcupados < lugares`, no el código.
- **Confirmación obligatoria (`confirmacion: true`) en HU-094 y HU-095** (M09).
  Replica el patrón de `confirmacionPerdidaInformacion` en la eliminación de
  cuenta: dar de baja a un inquilino o abandonar una unidad no se puede
  deshacer con un endpoint propio, así que el backend no confía en que el
  cliente ya confirmó.
- **Límite de frecuencia del chat: 30 mensajes/minuto por cuenta, fail-open**
  (M08, `SendMessageService`). Usa el mismo `IDistributedRateLimiter`
  compartido con `Auth:RateLimit`. Es fail-open —si el almacén distribuido no
  está disponible, el mensaje se envía igual— porque, a diferencia del cupo
  de unidades (HU-049), este límite no protege una invariante de negocio: no
  hay sobreventa ni inconsistencia de datos si un mensaje se envía sin pasar
  por el contador, solo se pierde la protección contra espeteo mientras Redis
  esté caído. Se prefirió no bloquear todo el chat por una dependencia
  externa caída.

Ninguna de estas cuatro decisiones tiene aprobación de producto registrada. Si
el equipo de producto fija otro valor, requiere una historia de cambio
explícita: hoy están codificadas en `UnitInvitation.ValidityDays`, en la
ausencia de un "consumo" del código, en la validación de `confirmacion` de
`RemoveTenantService`/`LeaveUnitService`, y en las constantes
`SendMessageService.MessagesPerWindow`/`WindowSeconds`.

## 4. Protección de cupo con CAS (HU-049)

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

## 5. HU-094: cómo se obtiene el `tenantAccountId`

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
- **Límite de frecuencia con fail-open deliberado** (razón completa en la
  sección 3): 30 mensajes/minuto por cuenta, con el mismo
  `IDistributedRateLimiter` que `Auth:RateLimit`; si el almacén distribuido
  no está disponible, el mensaje se envía igual.
- **Paginación por cursor compuesto (fecha, id), no solo por fecha.**
  `GET /mensajes` devuelve lo más reciente primero. La primera versión del
  cursor usaba solo la fecha del último mensaje de la página
  (`fecha < cursorFecha`); si dos o más mensajes comparten exactamente la
  misma fecha y el corte de página cae en medio de ese grupo, el filtro
  `<` (estricto) descarta permanentemente los que comparten la fecha del
  cursor pero no se devolvieron en la página anterior, porque ninguno de los
  dos lados de la comparación los distingue. Se corrigió desempatando por
  `MessageId` (`fecha < cursorFecha OR (fecha == cursorFecha AND id <
  cursorId)`, ordenado por `fecha DESC, id DESC`), aprovechando el mismo
  índice compuesto `ix_mensajes_conversacion_fecha`. Verificado revirtiendo
  temporalmente el desempate y confirmando que
  `ListConversationMessagesServiceTests.ListAsync_VariosMensajesConLaMismaFecha_NingunoSeSaltaEnElCorteDePagina`
  falla sin él (un mensaje desaparece) y pasa con él.
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
  (`ix_mensajes_conversacion_fecha`, compuesto), con los nombres de índice ya
  observados en Atlas. Como M09-001, no declara backfill porque la migración
  versionada de este proyecto solo declara colecciones e índices, nunca pasos
  de datos. **El nombre de los campos del índice (`participantes`;
  `conversacionId`+`fecha`) es una elección de este sprint, no algo
  confirmado contra Atlas** — ver sección 8, pendiente 2.
- Cobertura: pruebas de dominio (`ChatDomainTests`), de los 4 servicios de
  Application con repositorios en memoria (`StartConversationServiceTests`,
  `SendMessageServiceTests` incluyendo límite de frecuencia y fail-open,
  `ListConversationMessagesServiceTests` incluyendo el caso de mensajes con
  la misma fecha, `ListMyConversationsServiceTests`), de los 4 endpoints
  HTTP (`ChatEndpointTests`, incluyendo HU-070 sin correo ni teléfono en el
  cuerpo de la respuesta) y una prueba real y opt-in contra MongoDB
  (`MongoChatPersistenceTests`), **BLOCKED** localmente sin `mongod` igual
  que el resto de pruebas reales del proyecto.

## 7. Cambios MongoDB

- **`M08-001` (nueva):** `conversaciones` (`ix_conversaciones_participantes`)
  y `mensajes` (`ix_mensajes_conversacion_fecha`).
- **`M09-001` (nueva):** `vinculos_ocupacion`
  (`ix_ocupacion_inquilino_estado`, `ix_ocupacion_unidad_estado`) e
  `invitaciones_unidad` (`ix_invitaciones_unidad`, `ux_invitaciones_codigo`
  único).
- **Sin migración nueva para M05 (HU-033/036/092):** `unidades` añade el
  campo flexible `lugaresOcupados` (ver sección 4) sobre `M05-001`, que
  conserva su checksum. `GetMyPropertiesService` solo lee campos existentes.
- Ninguna de las dos migraciones nuevas se aplicó a Atlas. El orden completo
  para un entorno que parte de Sprint 5 es `M01-001`, `M01-002`, `M03-001`,
  `M05-001`, `M06-001`, `M07-001`, `M08-001`, `M09-001`. No saltar `M08-001`
  ni `M09-001`.

## 8. Pendientes

1. **CI:** `.github/workflows/ci.yml` ya se actualizó en este cierre para
   aplicar/verificar `M08-001` y `M09-001` en `mongo-db-test`, `dast-zap` y
   `restler-fuzzing` (después de `M07-001`, antes del resto de cada job). No
   se ejecutó el workflow en GitHub; queda **PENDING_CI** hasta que se
   disparen esos jobs.
2. **Verificar contra Atlas los campos reales de `ix_conversaciones_participantes`
   e `ix_mensajes_conversacion_fecha`.** La auditoría de Atlas (`docs/mongodb-m01.md`)
   solo registró los *nombres* de estos dos índices como preexistentes, no
   sus campos clave. `MongoChatSchema` asume `participantes` (arreglo de
   `accountId`) para el primero y `conversacionId` ascendente + `fecha`
   descendente para el segundo, por ser el diseño más consistente con el
   resto de M08/M09, pero esto **no está confirmado contra la definición
   real del índice en Atlas**. Antes de promover, ejecutar `plan`/`verify`
   de `M08-001` contra la base autorizada: `MongoIndexDefinition.Matches`
   compara contra el documento real de `listIndexes` y la migración falla
   cerrado (rechaza, no sobrescribe) si los campos no coinciden. Si Atlas
   tiene otros campos, se necesita una migración nueva, no editar
   `MongoChatSchema` directamente.
3. Matriz de esta sección (1) consolida las 15 HU de Sprint 6; la matriz de
   las 56 HU de Sprints 1–5 vive en `docs/sprint-05-cierre.md`, sección 10.
4. Las cuatro decisiones provisionales de la sección 3 no tienen aprobación
   de producto.
5. `M08-001` y `M09-001` no se aplicaron ni promovieron a Atlas.
6. Pruebas Mongo reales (`MongoOccupancyPersistenceTests`,
   `MongoChatPersistenceTests`) requieren `mongod` aislado o el job
   `mongo-db-test` autorizado; quedan **BLOCKED** localmente.
7. Mismo alcance que Sprint 5: Docker build/Container Smoke no ejecutados
   localmente; ZAP, RESTler, Sonar, CodeQL, Semgrep y Pact remotos:
   **PENDING_CI**.

## 9. Pruebas locales

| Control | Resultado |
|---|---|
| `dotnet build -c Release -warnaserror` | PASS, 0 warnings/errores |
| `dotnet format --verify-no-changes` | PASS |
| Unitarias | **869/869 PASS** (4 omitidas: OCR/PDF reales, requieren binarios locales) |
| Integración | **296/296 ejecutadas PASS**; 13 opt-in Mongo/Cloudinary omitidas explícitamente |
| OpenAPI | PASS: 52 operaciones (41 de Sprints 1–5 + 11 de Sprint 6: 1 de M05, 6 de M09, 4 de M08) |
| Python CI (`tools/ci`) | **167/167 PASS** |
| HU-049 (concurrencia) | PASS: local, HTTP y real-Mongo opt-in (3 niveles) |
| HU-068 (paginación) | PASS: incluye el caso de mensajes con la misma fecha, sin saltos |
| Mongo real (`mongo-db-test`) | **BLOCKED**: no hay `mongod` local |
| ZAP/RESTler/Sonar/CodeQL/Semgrep remotos | **PENDING_CI** |

## 10. Archivos principales

**Nuevos (Sprint 6)**
- `src/UniHome.Domain/Chat/{Conversation,Message}.cs`
- `src/UniHome.Application/Chat/**` (4 servicios + abstracciones)
- `src/UniHome.Application/Properties/MyProperties/GetMyPropertiesService.cs`
- `src/UniHome.Domain/OccupancyLinks/**`, `src/UniHome.Application/OccupancyLinks/**`
- `src/UniHome.Infrastructure/Persistence/Mongo/{Chat,OccupancyLinks}/**`
  (esquema, índices, migraciones `M08-001`/`M09-001`, repositorios)
- `src/UniHome.Api/Controllers/{ChatController,UnitOccupancyController,MyPropertiesController}.cs`
- `src/UniHome.Api/Contracts/{Chat,OccupancyLinks}/**`
- Tests de dominio, Application (con dobles en memoria), integración HTTP y
  Mongo real opt-in para M05/M08/M09.
- `docs/sprint-06-cierre.md` (este documento).

**Modificados**
- `MongoMigrationRegistry`, `OperationResult`/`AuthenticatedControllerBase`
  (nuevo estado `RateLimited` → 429), `Program.cs`, `DependencyInjection.cs`.
- `.github/workflows/ci.yml`: `M08-001`/`M09-001` en `mongo-db-test`,
  `dast-zap` y `restler-fuzzing`.
- `tools/ci/zap_dast.py` y `tools/ci/test_zap_dast.py`: catálogo de 52
  operaciones, flujo real de M09/M08.
- `README.md`: secciones de Sprint 6 (M05, M09, M08).

## 11. Hotfix de bloqueos del preflight productivo (23-nov-2026)

El preflight contra el SHA `34c69ae` detectó tres bloqueos y se corrigieron en
la rama `fix/sprint-06-m08-m09-hardening`:

- **B-01, chat M08 inoperante:** `MongoIdentitySchemaGuard` no reconocía
  `conversaciones` ni `mensajes`; los repositorios de chat lanzaban
  `StorageUnavailableException` y los cuatro endpoints respondían 503. El guard
  incorpora ahora `MongoChatIndexes.Definitions`, con una prueba unitaria de
  regresión y una prueba real M08 que inicia conversación, envía, lee, pagina y
  verifica autorización entre participantes.
- **B-02, doble liberación de cupo:** salida y baja leían el vínculo activo y
  luego lo guardaban con `ReplaceOne`, permitiendo dos liberaciones simultáneas.
  El repositorio expone `TryEndActiveAsync`, una transición atómica
  `Activo -> Finalizado`; solo la solicitud que gana libera cupo. La activación
  y reactivación usan `TryActivateAsync`, y la reserva perdedora se compensa.
  Se añadieron pruebas reales contra MongoDB para salida concurrente, baja
  concurrente, salida frente a baja y reactivación concurrente.
- **B-03, Quality Gate incompleto:** el job `mongo-db-test` aplicaba M08/M09 pero
  no ejecutaba sus pruebas de persistencia. El filtro incluye ahora
  `MongoChatPersistenceTests`, `MongoOccupancyPersistenceTests` y
  `MongoOccupancyConcurrencyPersistenceTests`; el resumen TRX exige 15 pruebas
  y falla si falta cualquiera o queda omitida. El job dejó de ser exclusivamente
  manual: se ejecuta y es obligatorio en PR y push a `main` cuando hay cambios
  de código, por lo que una regresión como B-01 bloquea el Quality Gate antes
  del merge.

Evidencia local: compilación Release con `-warnaserror`, 875 unitarias y 299
integradas aprobadas; MongoDB efímero real con M01-001, M01-002, M03-001,
M05-001, M06-001, M07-001, M08-001 y M09-001, **15/15** pruebas reales
aprobadas; OpenAPI 56/56. Stryker.NET, ZAP, RESTler, Sonar,
Semgrep, Gitleaks y CodeQL quedan **PENDIENTE_CI** hasta la ejecución manual
autorizada; no se declaran aprobados anticipadamente.
