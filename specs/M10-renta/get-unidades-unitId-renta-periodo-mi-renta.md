# `GET /api/v1/unidades/{unitId}/renta/{periodo}/mi-renta`

| Campo | Valor |
|---|---|
| Módulo | M10 – Renta y pagos informativos |
| HU | HU-057 |
| Operación OpenAPI | `GET /api/v1/unidades/{unitId}/renta/{periodo}/mi-renta` |
| Controlador | `src/UniHome.Api/Controllers/RentController.cs` → `MyRentShare` (`GetMyRentShareService.GetAsync`) |

## Autenticación y autorización

`[Authorize]`. Exige **vínculo activo del llamador** en la unidad como inquilino; sin él, `403` (`GetAsync_SinVinculoActivo_DebeDevolverForbidden`).

## Request

Parámetros de ruta: `unitId`, `periodo` (`"yyyy-MM"`). Sin cuerpo.

## Response

`200 OK` con `MiRentaResponse`: la propia cuota, lo pagado y el propio saldo — nunca la de otro inquilino. `Cache-Control: no-store`.

## Validaciones

Ninguna de entrada (solo parámetros de ruta).

## Códigos de error

| Código | Causa |
|---|---|
| 401 | Sesión inválida o ausente. |
| 403 | El llamador no tiene vínculo activo en la unidad. |
| 404 | El periodo no está configurado/no tiene cuota asignada, o la unidad no existe (`GetAsync_InquilinoActivoSinCuotaAsignada_DebeDevolverNotFound`, `GetAsync_UnidadInexistente_DebeDevolverNotFound`). |
| 422 | La división del periodo está pendiente (`GetAsync_DivisionPendiente_DebeDevolverUnprocessable`). |
| 503 | Falla de infraestructura. |

## Reglas de negocio

- Devuelve exclusivamente la cuota, lo pagado y el saldo **del propio llamador** (`GetAsync_InquilinoActivoConCuota_DevuelveSaldo`), nunca la de otro inquilino de la unidad.

## Controles de seguridad aplicados

- Autorización por participación activa (mitiga BOLA/IDOR: un inquilino no puede ver la cuota de otro simplemente cambiando `unitId`/`periodo`, porque el filtro es siempre sobre el `accountId` de la sesión, no sobre un parámetro).
- `Cache-Control: no-store`.

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/GetMyRentShareServiceTests.cs` (6 casos, incluido `GetAsync_SinVinculoActivo_DebeDevolverForbidden`).
- Integración: `tests/UniHome.IntegrationTests/RentEndpointTests.cs`.
- Seguridad: la autorización por vínculo activo (BOLA/IDOR) está cubierta por `GetAsync_SinVinculoActivo_DebeDevolverForbidden`. Cubierta además dinámicamente por el DAST (`tools/ci/zap_dast.py`) y por RESTler sobre el documento OpenAPI real.
