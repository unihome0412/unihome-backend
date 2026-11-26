# `GET /api/v1/unidades/{unitId}/inquilinos`

| Campo | Valor |
|---|---|
| Módulo | M09 – Ocupación |
| HU | HU-094 |
| Operación OpenAPI | `GET /api/v1/unidades/{unitId}/inquilinos` |
| Controlador | `src/UniHome.Api/Controllers/UnitOccupancyController.cs` → `ListTenants` (`ListUnitTenantsService.ListAsync`) |

## Autenticación y autorización

`[Authorize]`. Solo el **titular de la unidad** puede listar sus inquilinos activos; otra cuenta recibe `403`.

## Request

Parámetro de ruta: `unitId` (`Guid`). Sin cuerpo.

## Response

`200 OK` con `IReadOnlyList<InquilinoResponse>`: nombre y fecha de vínculo de cada inquilino activo. `Cache-Control: no-store`.

## Validaciones

`unitId` GUID válido.

## Códigos de error

| Código | Causa |
|---|---|
| 401 | Sesión inválida o ausente. |
| 403 | La cuenta no es titular de la unidad. |
| 404 | La unidad no existe. |
| 503 | Falla de infraestructura. |

## Reglas de negocio

- **Nunca correo ni teléfono** (comentario XML del controlador, HU-094): solo expone `AccountId` y `FullName`.
- Permite obtener el `tenantAccountId` necesario para dar de baja a un inquilino concreto (`DELETE /inquilinos/{tenantAccountId}`): el backend no expone ese id por ninguna otra vía (README.md, "Sprint 6 / M09: ocupación").

## Controles de seguridad aplicados

- Autorización por titularidad (mitiga BOLA/IDOR).
- Minimización de datos: nunca expone correo ni teléfono del inquilino.
- `Cache-Control: no-store`.

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/ListUnitTenantsServiceTests.cs` (`ListAsync_UnidadPropia_DevuelveSoloInquilinosActivosSinCorreoNiTelefono`, `ListAsync_UnidadAjena_DebeDevolverForbidden`, `ListAsync_UnidadInexistente_DebeDevolverNotFound`, `ListAsync_LlamanteNoElegible_DebeDevolverForbidden`) — agregada en el Bloque 1 de esta auditoría para cerrar el hueco detectado (antes solo había cobertura de integración).
- Integración: `tests/UniHome.IntegrationTests/UnitOccupancyEndpointTests.cs` cubre `200` (propio), `403` (cuenta ajena) y `401` (sin sesión) contra la ruta real.
- Seguridad: la autorización por titularidad (BOLA/IDOR) está cubierta por la prueba de integración de `403` citada arriba. Cubierta además dinámicamente por el DAST (`tools/ci/zap_dast.py`) y por RESTler sobre el documento OpenAPI real.
