# `POST /api/v1/unidades/{unitId}/renta/{periodo}/pagos/{tenantAccountId}`

| Campo | Valor |
|---|---|
| Módulo | M10 – Renta y pagos informativos |
| HU | HU-055 |
| Operación OpenAPI | `POST /api/v1/unidades/{unitId}/renta/{periodo}/pagos/{tenantAccountId}` |
| Controlador | `src/UniHome.Api/Controllers/RentController.cs` → `RegisterPayment` (`RegisterRentPaymentService.RegisterAsync`) |

## Autenticación y autorización

`[Authorize]`. Solo el **titular de la unidad** registra pagos; otra cuenta recibe `403`.

## Request

- Parámetros de ruta: `unitId`, `periodo` (`"yyyy-MM"`), `tenantAccountId` (`Guid`).
- Body JSON `RegistrarPagoRequest`: `montoMxn`, `fechaRecepcion`.
- `[RequestSizeLimit(8 * 1024)]`.

## Response

`201 Created` con `PagoResponse`. `Cache-Control: no-store`.

## Validaciones

- Monto no positivo o fecha de recepción futura: `400` (`RegisterAsync_MontoNoPositivoOFechaFutura_DebeRechazarse`).
- `tenantAccountId` debe tener vínculo activo en la unidad: `400` si no (`RegisterAsync_InquilinoSinVinculoActivo_DebeRechazarse`).
- Si el periodo no existe y no hay uno anterior que heredar: `404` (`RegisterAsync_SinPeriodoNiAnterior_DebeDevolverNotFound`).

## Códigos de error

| Código | Causa |
|---|---|
| 400 | Monto no positivo, fecha futura, o inquilino sin vínculo activo. |
| 401 | Sesión inválida o ausente. |
| 403 | La cuenta no es titular de la unidad. |
| 404 | El periodo no existe y no hay un periodo anterior que heredar. |
| 413 | El cuerpo de la solicitud excede 8 KB. |
| 503 | Falla de infraestructura. |

## Reglas de negocio

- **Es una bitácora, no un cobro:** puede haber varios pagos para el mismo (periodo, inquilino) y se suman contra la cuota; un pago que exceda la cuota **no se rechaza**, solo deja saldo a favor (`RegisterAsync_MontoMayorQueLaCuota_NoSeRechaza`, README.md).
- El periodo se resuelve igual que en la división (existente o heredado del periodo anterior más reciente).
- UniHome no procesa pagos ni interviene en la transacción económica entre las partes (HU-090): el registro es meramente informativo.

## Controles de seguridad aplicados

- Autorización por titularidad (mitiga BOLA/IDOR).
- Validación de que el pago solo se registre contra un inquilino con vínculo activo real en la unidad (mitigación de asignación masiva).

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/RegisterRentPaymentServiceTests.cs` (6 casos, incluido `RegisterAsync_UnidadAjena_DebeDevolverForbidden`).
- Integración: `tests/UniHome.IntegrationTests/RentEndpointTests.cs`.
- Seguridad: la autorización por titularidad está cubierta por `RegisterAsync_UnidadAjena_DebeDevolverForbidden`. Cubierta además dinámicamente por el DAST (`tools/ci/zap_dast.py`) y por RESTler sobre el documento OpenAPI real.
