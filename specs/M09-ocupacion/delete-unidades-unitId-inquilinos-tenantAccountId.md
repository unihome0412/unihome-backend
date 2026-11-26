# `DELETE /api/v1/unidades/{unitId}/inquilinos/{tenantAccountId}`

| Campo | Valor |
|---|---|
| Módulo | M09 – Ocupación |
| HU | HU-094 |
| Operación OpenAPI | `DELETE /api/v1/unidades/{unitId}/inquilinos/{tenantAccountId}` |
| Controlador | `src/UniHome.Api/Controllers/UnitOccupancyController.cs` → `RemoveTenant` (`RemoveTenantService.RemoveAsync`) |

## Autenticación y autorización

`[Authorize]`. Solo el **titular de la unidad** puede dar de baja a un inquilino; otra cuenta recibe `403`.

## Request

- Parámetros de ruta: `unitId`, `tenantAccountId` (`Guid`).
- Body JSON `ConfirmacionRequest`: `confirmacion` (bool).
- `[RequestSizeLimit(8 * 1024)]`.

## Response

`204 No Content`.

## Validaciones

- `confirmacion` debe ser `true`; sin ella responde `400` (`ValidationProblemDetails`, `RemoveAsync_SinConfirmacion_DebeDevolverValidationFailed`). La UI puede pedir confirmación, pero el backend también la exige (README.md).
- Tamaño de solicitud ≤ 8 KB (`413`).

## Códigos de error

| Código | Causa |
|---|---|
| 400 | Falta `confirmacion: true`. |
| 401 | Sesión inválida o ausente. |
| 403 | La cuenta no es titular de la unidad. |
| 404 | El inquilino no está vinculado a esta unidad. |
| 413 | El cuerpo de la solicitud excede 8 KB. |
| 503 | Falla de infraestructura. |

## Reglas de negocio

- Finaliza el vínculo y **libera el lugar** (`RemoveAsync_ConConfirmacion_FinalizaVinculoYLiberaLugar`, vía `IUnitRepository.ReleaseSlotAsync`).
- Si la unidad está en mantenimiento, la baja no cambia la disponibilidad mostrada (`RemoveAsync_UnidadEnMantenimiento_NoCambiaDisponibilidad`): el estado manual de mantenimiento no se sobreescribe automáticamente (ver `put-unidades-unitId-mantenimiento.md`).
- Usa `tenantAccountId`, nunca un id enviado como "el inquilino actual"; el propietario obtiene ese id previamente desde `GET /inquilinos` (README.md).

## Controles de seguridad aplicados

- Autorización por titularidad (mitiga BOLA/IDOR: evita que una cuenta ajena desvincule a un inquilino de una unidad que no le pertenece).
- Confirmación explícita obligatoria en el backend, no solo en la UI (control contra acciones destructivas accidentales o CSRF-like).
- `ReleaseSlotAsync` nunca decrementa por debajo de 0 (invariante de integridad del cupo).

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/RemoveTenantServiceTests.cs` (`RemoveAsync_ConConfirmacion_FinalizaVinculoYLiberaLugar`, `RemoveAsync_SinConfirmacion_DebeDevolverValidationFailed`, `RemoveAsync_UnidadAjena_DebeDevolverForbidden`, `RemoveAsync_InquilinoNoVinculado_DebeDevolverNotFound`, `RemoveAsync_UnidadEnMantenimiento_NoCambiaDisponibilidad`).
- Integración: `tests/UniHome.IntegrationTests/UnitOccupancyEndpointTests.cs`.
- Seguridad: la autorización por titularidad (BOLA/IDOR) está cubierta por `RemoveAsync_UnidadAjena_DebeDevolverForbidden`. Cubierta además dinámicamente por el DAST (`tools/ci/zap_dast.py`) y por RESTler sobre el documento OpenAPI real.
