# `POST /api/v1/admin/expedientes/{id}/aprobar`

| Campo | Valor |
|---|---|
| Módulo | M06 – Verificación documental |
| HU | HU-024 |
| Operación OpenAPI | `POST /api/v1/admin/expedientes/{id}/aprobar` |
| Controlador | `src/UniHome.Api/Controllers/AdminVerificationController.cs` → `Approve` (EP-M06-07) |

## Autenticación y autorización

Policy **Admin** a nivel de clase (ver [`get-admin-expedientes.md`](get-admin-expedientes.md)).
Solo una decisión humana explícita; no hay aprobación automática por
ninguna señal de OCR/ELA.

## Request

Parámetro de ruta `id` (`Guid`). Sin body (`RequestSizeLimit(8 * 1024)`
igual que el resto del controlador, aunque no se envía payload).

## Response

`200 OK` con `ExpedienteDetalleResponse` (el mismo contrato que
`GET /{id}`), tras recalcular el detalle posterior a la decisión.

## Validaciones

- El expediente debe existir; si no, `404`.
- El expediente debe estar `EnRevision` **y** con `DocumentsReceived=true`;
  en cualquier otro estado responde `409` (decisión duplicada, expediente
  pendiente, o ya decidido).
- La unidad vinculada debe existir, pertenecer al mismo `OwnerAccountId`
  del expediente y estar `EnRevision`; si no, `409`.

## Códigos de error

| Código | Causa |
|---|---|
| 401 | Sin sesión válida. |
| 403 | La cuenta no tiene privilegio administrativo. |
| 404 | El expediente no existe. |
| 409 | El expediente no está `EnRevision`/completo, ya fue decidido, o la unidad no está en el estado esperado (carrera de concurrencia). |
| 413 | Solicitud excede 8 KB. |
| 503 | Fallo de almacenamiento no recuperable tras compensar (`StorageUnavailableException`). |

## Reglas de negocio

- **CAS de un solo documento + compensación:** el expediente se actualiza
  primero con `TryReplaceAsync` contra su `UpdatedAt` previo; si la unidad
  no puede transicionar de `EnRevision` a `Aprobada` (`TryTransitionPublicationStateAsync`
  con filtro de estado esperado) y no quedó ya en el estado destino por
  otra carrera, la decisión del expediente se **revierte**
  (`RevertDecisionForCompensation`) para no dejar un expediente aprobado
  con una unidad que no se publicó. Si la compensación también falla,
  propaga `503` y la publicación permanece oculta — nunca se publica por
  accidente.
- Toda decisión se audita (`VerificationAuditActions.ExpedienteApproved` /
  `DecisionCompensated`).
- Tras aprobar, la unidad pasa a `PublicationState.Aprobada` y aparece en
  M04 (exploración pública); el perfil del titular cambia a
  `ConEspaciosPublicados` (etiqueta usada también por M13).
- Una segunda decisión sobre el mismo expediente responde `409`
  (idempotencia negativa: no se puede re-decidir).

## Controles de seguridad aplicados

- Autorización por función en `/admin/*`.
- Decisión humana obligatoria; ninguna señal automática (OCR/ELA/comparación
  de nombres) puede aprobar por sí sola.
- Consistencia transaccional compensada entre dos agregados (expediente y
  unidad) sin transacciones multi-documento de Mongo, evitando que una
  publicación quede visible sin que su expediente esté realmente aprobado.
- Auditoría explícita de quién decidió y cuándo.

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/DecideExpedienteServiceTests.cs`
  (`Aprobar_DebeRegistrarDecisionAuditoriaYPublicacionVisible`,
  `DecisionDuplicada_DebeResponderConflict`,
  `ExpedientePendiente_NoPuedeAprobarse`,
  `Decision_ExpedienteInexistenteOAdminVacio_DebeRechazarse`,
  `UnidadAusenteOCasFallido_DebeResponderConflict`,
  `UnidadYaAprobada_NoPuedeRecibirOtraDecision`,
  `FalloDeAuditoria_DebeCompensarDecisionYPublicacion`,
  `TransicionFalsaPeroUnidadYaEstaEnEstadoFinal_DebeSerExitosa`,
  `TransicionFalsaYUnidadSigueEnRevision_DebeCompensarYResponderConflict`,
  `TransicionFalsaYUnidadEnEstadoInesperado_DebeLanzarStorageUnavailable`,
  `UnidadDeOtroPropietario_DebeResponderConflict`).
- Integración: `tests/UniHome.IntegrationTests/VerificationEndpointTests.cs`
  (`Aprobar_DebePublicarUnidadCambiarPerfilYSerTerminal`,
  `AprobacionesConcurrentes_SoloUnaDebeGanar`,
  `Decision_RequiereAdminYNoAceptaExpedienteInexistente`).
- Seguridad: cubierto por el recorrido dinámico de ZAP y la gramática
  RESTler sobre el documento OpenAPI; la prueba de concurrencia
  (`AprobacionesConcurrentes_SoloUnaDebeGanar`) actúa además como prueba
  de condición de carrera (resource-consumption/race condition del
  OWASP API Top 10).
