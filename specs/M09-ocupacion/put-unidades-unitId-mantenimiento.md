# `PUT /api/v1/unidades/{unitId}/mantenimiento`

| Campo | Valor |
|---|---|
| Módulo | M09 – Ocupación |
| HU | HU-093 |
| Operación OpenAPI | `PUT /api/v1/unidades/{unitId}/mantenimiento` |
| Controlador | `src/UniHome.Api/Controllers/UnitOccupancyController.cs` → `SetMaintenance` (`SetUnitMaintenanceService.SetAsync`) |

## Autenticación y autorización

`[Authorize]`. Solo el **titular de la unidad** puede marcar/desmarcar mantenimiento; otra cuenta recibe `403`.

## Request

- Parámetro de ruta: `unitId` (`Guid`).
- Body JSON `MantenimientoRequest`: `enMantenimiento` (bool).
- `[RequestSizeLimit(8 * 1024)]`.

## Response

`200 OK` con `UnidadResponse` (contrato completo de unidad). `Cache-Control: no-store`.

## Validaciones

- `unitId` GUID válido.
- Tamaño de solicitud ≤ 8 KB (`413`).
- No hay validación adicional de cuerpo más allá del tipo bool (no se declara `400` en el contrato).

## Códigos de error

| Código | Causa |
|---|---|
| 401 | Sesión inválida o ausente. |
| 403 | La cuenta no es titular de la unidad. |
| 404 | La unidad no existe. |
| 413 | El cuerpo de la solicitud excede 8 KB. |
| 503 | Falla de infraestructura. |

## Reglas de negocio

- `Mantenimiento` es un estado manual del propietario que no se sobreescribe automáticamente mientras esté activo (`SetAsync_Activa_MarcaMantenimientoSinImportarOcupacion`); al desactivarlo se recalcula `estadoOcupacion` desde `lugaresOcupados` frente a `lugares` (`SetAsync_Desactiva_RecalculaDesdeLugaresOcupados`, `SetAsync_Desactiva_SinOcupacion_QuedaDisponible`, README.md).

## Controles de seguridad aplicados

- Autorización por titularidad (mitiga BOLA/IDOR sobre `unitId`).
- `Cache-Control: no-store`.

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/SetUnitMaintenanceServiceTests.cs` (incluye `SetAsync_UnidadAjena_DebeDevolverForbidden`, `SetAsync_UnidadInexistente_DebeDevolverNotFound`).
- Integración: `tests/UniHome.IntegrationTests/UnitOccupancyEndpointTests.cs`.
- Seguridad: la autorización por titularidad está cubierta por `SetAsync_UnidadAjena_DebeDevolverForbidden`. Cubierta además dinámicamente por el DAST (`tools/ci/zap_dast.py`) y por RESTler sobre el documento OpenAPI real.
