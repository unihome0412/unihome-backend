# `POST /api/v1/admin/expedientes/{id}/rechazar`

| Campo | Valor |
|---|---|
| Módulo | M06 – Verificación documental |
| HU | HU-025 |
| Operación OpenAPI | `POST /api/v1/admin/expedientes/{id}/rechazar` |
| Controlador | `src/UniHome.Api/Controllers/AdminVerificationController.cs` → `Reject` (EP-M06-08) |

## Autenticación y autorización

Policy **Admin** a nivel de clase (ver [`get-admin-expedientes.md`](get-admin-expedientes.md)).

## Request

Parámetro de ruta `id` (`Guid`). Body JSON `RechazarExpedienteRequest` con
`Motivo` (string, obligatorio y no vacío). `RequestSizeLimit(8 * 1024)`.

## Response

`200 OK` con `ExpedienteDetalleResponse` recalculado tras la decisión,
incluyendo el motivo de rechazo.

## Validaciones

- `Motivo` obligatorio y no vacío/espacios; si falta → `400`
  (`ValidationFailed` sobre el campo `motivo`).
- Mismas precondiciones que aprobar: el expediente debe existir (`404`),
  estar `EnRevision` y completo, y la unidad vinculada debe estar
  `EnRevision` y pertenecer al mismo propietario; si no, `409`.

## Códigos de error

| Código | Causa |
|---|---|
| 400 | `Motivo` vacío o inválido. |
| 401 | Sin sesión válida. |
| 403 | La cuenta no tiene privilegio administrativo. |
| 404 | El expediente no existe. |
| 409 | El expediente no está `EnRevision`/completo, ya fue decidido, o la unidad no está en el estado esperado. |
| 413 | Solicitud excede 8 KB. |
| 503 | Fallo de almacenamiento no recuperable tras compensar. |

## Reglas de negocio

- Mismo mecanismo de CAS + compensación que aprobar (ver
  [`post-admin-expedientes-id-aprobar.md`](post-admin-expedientes-id-aprobar.md)),
  pero la unidad transiciona a `PublicationState.Rechazada` y **nunca se
  publica**.
- El motivo es obligatorio y se persiste en el expediente; el solicitante
  puede consultarlo vía `GET /expedientes/mi-expediente`.
- Auditoría explícita (`VerificationAuditActions.ExpedienteRejected`) con
  el motivo incluido en el detalle de auditoría.

## Controles de seguridad aplicados

- Autorización por función en `/admin/*`.
- Motivo de rechazo obligatorio (trazabilidad de la decisión humana).
- Misma consistencia compensada que la aprobación; una unidad rechazada
  nunca queda en un estado ambiguo.

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/DecideExpedienteServiceTests.cs`
  (`Rechazar_DebeGuardarMotivoYNoPublicar`,
  `RechazarSinMotivo_DebeFallarValidacionYSinCambiarEstado`,
  `RechazoConMotivoInvalido_DebeFallarValidacion`, y las pruebas
  compartidas de CAS/compensación/concurrencia ya citadas en la
  aprobación).
- Integración: `tests/UniHome.IntegrationTests/VerificationEndpointTests.cs`
  → `Rechazar_ExigeMotivoYNoPublicaLaUnidad`.
- Seguridad: cubierto por el recorrido dinámico de ZAP y la gramática
  RESTler sobre el documento OpenAPI; sin prueba de seguridad dedicada
  adicional.
