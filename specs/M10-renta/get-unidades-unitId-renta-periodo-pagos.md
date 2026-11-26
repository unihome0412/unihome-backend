# `GET /api/v1/unidades/{unitId}/renta/{periodo}/pagos`

| Campo | Valor |
|---|---|
| Módulo | M10 – Renta y pagos informativos |
| HU | HU-056 |
| Operación OpenAPI | `GET /api/v1/unidades/{unitId}/renta/{periodo}/pagos` |
| Controlador | `src/UniHome.Api/Controllers/RentController.cs` → `ListPayments` (`ListRentPaymentsService.ListAsync`) |

## Autenticación y autorización

`[Authorize]`. Solo el **titular de la unidad** consulta el estado de pagos de todos sus inquilinos; otra cuenta recibe `403`.

## Request

Parámetros de ruta: `unitId`, `periodo` (`"yyyy-MM"`). Sin cuerpo.

## Response

`200 OK` con `EstadoPagosResponse`: por cada inquilino con cuota asignada, lo pagado, el saldo (`pagado − cuota`) y el estado (`Pendiente`/`Parcial`/`Registrado`). `Cache-Control: no-store`.

## Validaciones

Ninguna de entrada (solo parámetros de ruta).

## Códigos de error

| Código | Causa |
|---|---|
| 401 | Sesión inválida o ausente. |
| 403 | La cuenta no es titular de la unidad. |
| 404 | El periodo no existe y no hay un periodo anterior que heredar. |
| 422 | La división de este periodo está pendiente; debe configurarse primero (`ListAsync_DivisionPendiente_DebeDevolverUnprocessable`). |
| 503 | Falla de infraestructura. |

## Reglas de negocio

- Por cada inquilino con cuota asignada se calcula lo pagado, el saldo y el estado: `Pendiente` (nada pagado), `Parcial` (pagado < cuota) o `Registrado` (pagado ≥ cuota, incluido sobrepago) — `RentPeriod.ComputePaymentState` (`ListAsync_PagoParcialYCompleto_ClasificaCorrectamente`, `ListAsync_SobrepagoDejaSaldoAFavor_Registrado`).
- Exige una división ya aplicada; si está pendiente, responde `422` con el detalle de configurarla primero.

## Controles de seguridad aplicados

- Autorización por titularidad (mitiga BOLA/IDOR: evita que un inquilino o un tercero vea el estado de pagos de todos los demás inquilinos de una unidad ajena).
- `Cache-Control: no-store`.

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/ListRentPaymentsServiceTests.cs` (6 casos, incluido `ListAsync_UnidadAjena_DebeDevolverForbidden`).
- Integración: `tests/UniHome.IntegrationTests/RentEndpointTests.cs`.
- Seguridad: la autorización por titularidad está cubierta por `ListAsync_UnidadAjena_DebeDevolverForbidden`. Cubierta además dinámicamente por el DAST (`tools/ci/zap_dast.py`) y por RESTler sobre el documento OpenAPI real.
