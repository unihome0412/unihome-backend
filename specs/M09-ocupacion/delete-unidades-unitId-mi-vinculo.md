# `DELETE /api/v1/unidades/{unitId}/mi-vinculo`

| Campo | Valor |
|---|---|
| Módulo | M09 – Ocupación |
| HU | HU-095 |
| Operación OpenAPI | `DELETE /api/v1/unidades/{unitId}/mi-vinculo` |
| Controlador | `src/UniHome.Api/Controllers/UnitOccupancyController.cs` → `LeaveUnit` (`LeaveUnitService.LeaveAsync`) |

## Autenticación y autorización

`[Authorize]`. El propio inquilino deja la unidad; usa el claim `sub` de la sesión, nunca un id enviado por el cliente (README.md, HU-095).

## Request

- Parámetro de ruta: `unitId` (`Guid`).
- Body JSON `ConfirmacionRequest`: `confirmacion` (bool).
- `[RequestSizeLimit(8 * 1024)]`.

## Response

`204 No Content`.

## Validaciones

- `confirmacion` debe ser `true`; sin ella responde `400` (`LeaveAsync_SinConfirmacion_DebeDevolverValidationFailed`).
- Tamaño de solicitud ≤ 8 KB (`413`).

## Códigos de error

| Código | Causa |
|---|---|
| 400 | Falta `confirmacion: true`. |
| 401 | Sesión inválida o ausente. |
| 404 | No tienes un vínculo activo con esta unidad, o la unidad no existe (`LeaveAsync_SinVinculoActivo_DebeDevolverNotFound`, `LeaveAsync_UnidadInexistente_DebeDevolverNotFound`). |
| 413 | El cuerpo de la solicitud excede 8 KB. |
| 503 | Falla de infraestructura. |

## Reglas de negocio

- Finaliza el vínculo propio y libera el lugar (`LeaveAsync_ConConfirmacion_FinalizaVinculoYLiberaLugar`).
- `ReleaseSlotAsync` nunca decrementa por debajo de 0 (`ReleaseSlotAsync_SinLugaresOcupados_NuncaQuedaNegativo`), misma invariante que la baja de inquilino (HU-094).
- Mismo requisito de `confirmacion: true` que `confirmacionPerdidaInformacion` en la eliminación de cuenta y que la baja de inquilino (README.md).

## Controles de seguridad aplicados

- El `accountId` se toma del claim `sub` del token, nunca de un parámetro del cliente (evita que una cuenta finalice el vínculo de otra — mitigación de asignación masiva/IDOR).
- Confirmación explícita obligatoria en el backend.

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/LeaveUnitServiceTests.cs` (`LeaveAsync_ConConfirmacion_FinalizaVinculoYLiberaLugar`, `ReleaseSlotAsync_SinLugaresOcupados_NuncaQuedaNegativo`, `LeaveAsync_SinConfirmacion_DebeDevolverValidationFailed`, `LeaveAsync_SinVinculoActivo_DebeDevolverNotFound`, `LeaveAsync_UnidadInexistente_DebeDevolverNotFound`).
- Integración: `tests/UniHome.IntegrationTests/UnitOccupancyEndpointTests.cs`.
- Seguridad: cubierta dinámicamente por el DAST (`tools/ci/zap_dast.py`) y por RESTler sobre el documento OpenAPI real; sin prueba de seguridad dedicada adicional a la unitaria citada.
