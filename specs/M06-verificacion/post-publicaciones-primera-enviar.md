# `POST /api/v1/publicaciones/primera/enviar`

| Campo | Valor |
|---|---|
| Módulo | M06 – Verificación documental |
| HU | HU-013/HU-027 |
| Operación OpenAPI | `POST /api/v1/publicaciones/primera/enviar` |
| Controlador | `src/UniHome.Api/Controllers/PublicationsController.cs` → `SubmitFirstPublication` (EP-M05-08) |

## Autenticación y autorización

Requiere `Authorization: Bearer` (`[Authorize]`). Opera siempre sobre la
primera unidad del `accountId` del token (`SubmitFirstPublicationService.SubmitAsync`),
nunca sobre un id de unidad recibido del cliente.

## Request

Sin body (`RequestSizeLimit` de 8 KB igual que el resto del controlador,
aunque no se envía payload).

## Response

`200 OK` con `PublicacionResponse` (`PublicacionResponse.FromView`): estado
de la publicación tras el envío a revisión.

## Validaciones

- Exige que exista una primera publicación ya creada (unidad propia);
  si no, `404`.
- Exige, en `SubmitFirstPublicationService`: términos aceptados, al menos
  una foto cargada y **ambos** documentos de verificación (INE y
  comprobante) recibidos; si falta alguno, `422`.
- Exige mayoría de edad (elegibilidad de publicación); si no, `403`.
- Si el expediente está `Pendiente` (documentos incompletos) o
  `Rechazado`, responde `422` (no se puede enviar a revisión).
- Transición de estado con CAS; si la unidad no está en el estado
  esperado al momento de transicionar, responde `409`.

## Códigos de error

| Código | Causa |
|---|---|
| 401 | Sin sesión válida. |
| 403 | La cuenta no es elegible para publicar (menor de edad). |
| 404 | No existe una primera publicación para enviar. |
| 409 | Conflicto de transición de estado (carrera concurrente). |
| 413 | Solicitud excede 8 KB. |
| 422 | Faltan términos, foto o documentos de verificación; o el expediente está pendiente/rechazado. |
| 503 | Preflight de esquema Mongo fallido. |

## Reglas de negocio

- La unidad nace en `Borrador`; este endpoint es el disparador manual
  (además del disparador automático al completar ambos documentos en
  `UploadVerificationDocumentService.TrySubmitPublicationAsync`) que la
  mueve a `EnRevision`.
- Operación idempotente si ya está `EnRevision` con todo completo
  (`EnviarConTodoCompleto_DebeSerIdempotenteYEnRevision`).
- Mientras está `EnRevision`, la unidad **no aparece** en M04 (exploración
  pública); solo el propio titular la ve vía
  `GET /mi-publicacion/estado-mapa`.

## Controles de seguridad aplicados

- Opera exclusivamente sobre la unidad del `accountId` del token
  (anti-IDOR: no hay id de unidad en el payload ni en la ruta).
- Verificación de mayoría de edad antes de habilitar publicación.
- Límite de tamaño de solicitud (8 KB) como mitigación genérica de abuso.

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/VerificationServiceTests.cs`
  (`EnviarSinTerminosFotoODocumentos_DebeResponderUnprocessable`,
  `EnviarSinUnidad_DebeResponderNotFound`,
  `EnviarSinFoto_DebeResponderUnprocessable`,
  `EnviarConExpedientePendiente_DebeResponderUnprocessable`,
  `EnviarConExpedienteRechazado_DebeResponderUnprocessable`,
  `EnviarConTransicionFallida_DebeResponderConflict`,
  `EnviarDesdeBorradorConExpedienteEnRevision_DebeAuditarYEnRevision`,
  `EnviarConTodoCompleto_DebeSerIdempotenteYEnRevision`) — ejercitan
  directamente `SubmitFirstPublicationService.SubmitAsync` (el archivo
  agrupa las pruebas unitarias de todo el módulo de verificación, no solo
  la subida de documentos).
- Integración: `tests/UniHome.IntegrationTests/VerificationEndpointTests.cs`
  (`FlujoCompleto_DocumentosPasanARevisionYLaUnidadNoEsPublica` para el
  camino feliz `200`, `EnvioSinDocumentosTerminosOFoto_DebeResponder422`
  para `422`, `EnvioSinPublicacionYAdminConParametrosInvalidos_DebenFallarControladamente`
  para `404`). No se encontró una prueba de integración HTTP dedicada para
  el `403` de elegibilidad por edad ni para el `409` de conflicto de
  transición sobre este endpoint específico (sí están cubiertos a nivel
  unitario); ver `docs/matriz-pruebas-endpoints.md`.
- Seguridad: cubierto por el recorrido dinámico de ZAP y por la gramática
  RESTler sobre el documento OpenAPI; sin prueba de seguridad dedicada
  adicional.
