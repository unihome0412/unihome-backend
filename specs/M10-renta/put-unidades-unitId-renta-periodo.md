# `PUT /api/v1/unidades/{unitId}/renta/{periodo}`

| Campo | Valor |
|---|---|
| Módulo | M10 – Renta y pagos informativos |
| HU | HU-050 / HU-051 / HU-052 |
| Operación OpenAPI | `PUT /api/v1/unidades/{unitId}/renta/{periodo}` |
| Controlador | `src/UniHome.Api/Controllers/RentController.cs` → `Configure` (`ConfigureRentPeriodService.ConfigureAsync`) |

## Autenticación y autorización

`[Authorize]`. Solo el **titular de la unidad** configura la renta; cualquier otra cuenta recibe `403`.

## Request

- Parámetros de ruta: `unitId` (`Guid`), `periodo` (string, formato `"yyyy-MM"`).
- Body JSON `ConfigurarRentaRequest`: `rentaTotalMxn`, `diaCorte`, `conceptos[]` (`nombre`, `esFijo`).
- `[RequestSizeLimit(8 * 1024)]`.

## Response

`200 OK` con `RentaPeriodoResponse`. `Cache-Control: no-store`. Toda respuesta del módulo incluye el aviso de que UniHome no procesa dinero ni interviene en transacciones económicas entre las partes (HU-090).

## Validaciones

- `periodo` inválido (no `"yyyy-MM"`): `400` (`ConfigureAsync_PeriodoInvalido_DebeRechazarse`).
- `conceptos` nulo: `400` (`ConfigureAsync_ConceptosNulos_DebeRechazarse`).
- Tamaño de solicitud ≤ 8 KB (`413`).

## Códigos de error

| Código | Causa |
|---|---|
| 400 | Periodo con formato inválido o conceptos nulos. |
| 401 | Sesión inválida o ausente. |
| 403 | La cuenta no es titular de la unidad. |
| 404 | La unidad no existe. |
| 413 | El cuerpo de la solicitud excede 8 KB. |
| 503 | Falla de infraestructura. |

## Reglas de negocio

- **Este endpoint nunca hereda:** siempre usa los valores explícitos de la solicitud y **reinicia la división a pendiente** (`ConfigureAsync_PeriodoExistenteConDivision_ReinicioLaDivision`), porque unas cuotas calculadas contra la renta anterior ya no suman correctamente contra la nueva (README.md, "Sprint 7 / M10-M12").
- Un periodo nuevo se crea con división pendiente (`ConfigureAsync_PeriodoNuevo_CreaConDivisionPendiente`).
- Solo el titular de la unidad configura la renta y su división; cualquier otra cuenta recibe `403`, y una unidad inexistente, `404`.

## Controles de seguridad aplicados

- Autorización por titularidad (mitiga BOLA/IDOR sobre `unitId`).
- `Cache-Control: no-store`; sin datos personales de terceros en la respuesta.

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/ConfigureRentPeriodServiceTests.cs` (6 casos: periodo nuevo, reinicio de división, conceptos nulos, periodo inválido, unidad ajena → `Forbidden`, unidad inexistente → `NotFound`).
- Integración: `tests/UniHome.IntegrationTests/RentEndpointTests.cs`.
- Seguridad: la autorización por titularidad está cubierta por `ConfigureAsync_UnidadAjena_DebeDevolverForbidden`. Cubierta además dinámicamente por el DAST (`tools/ci/zap_dast.py`) y por RESTler sobre el documento OpenAPI real.
