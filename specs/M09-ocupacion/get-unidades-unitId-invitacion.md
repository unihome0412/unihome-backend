# `GET /api/v1/unidades/{unitId}/invitacion`

| Campo | Valor |
|---|---|
| Módulo | M09 – Ocupación |
| HU | HU-048 |
| Operación OpenAPI | `GET /api/v1/unidades/{unitId}/invitacion` |
| Controlador | `src/UniHome.Api/Controllers/UnitOccupancyController.cs` → `GetInvitation` (`GetUnitInvitationService.GetAsync`) |

## Autenticación y autorización

`[Authorize]` (Bearer JWT). Solo el **titular de la unidad** puede consultar/generar su código de invitación; otra cuenta recibe `403`.

## Request

Parámetro de ruta: `unitId` (`Guid`). Sin cuerpo.

## Response

`200 OK` con `InvitacionUnidadResponse`: código de invitación vigente y su fecha de vigencia. `Cache-Control: no-store`.

## Validaciones

`unitId` debe ser GUID válido (restricción de ruta).

## Códigos de error

| Código | Causa |
|---|---|
| 401 | Sesión inválida o ausente. |
| 403 | La cuenta no es titular de la unidad. |
| 404 | La unidad no existe. |
| 409 | No se pudo generar un código de invitación (reintentar). |
| 503 | Falla de infraestructura (MongoDB no disponible). |

## Reglas de negocio

- El código de invitación es de la unidad, no de un inquilino concreto: se reutiliza mientras no venza y no se consume al unirse (README.md, "Sprint 6 / M09: ocupación").
- `GET /invitacion` es **idempotente**: devuelve el código vigente si existe, o genera uno nuevo (8 caracteres, sin `0`/`O`/`1`/`I`) si no hay uno o venció. Vigencia de 7 días (umbral provisional sin aprobación de producto). Los códigos anteriores no se borran, quedan vencidos.

## Controles de seguridad aplicados

- Autorización por titularidad (mitiga BOLA/IDOR: una cuenta no puede leer el código de invitación de una unidad ajena).
- `Cache-Control: no-store`.

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/GetUnitInvitationServiceTests.cs` (`GetAsync_SinInvitacionPrevia_GeneraUnaNueva`, `GetAsync_ConInvitacionVigente_DevuelveLaMismaSinGenerarOtra`, `GetAsync_ConInvitacionVencida_GeneraUnaNuevaYConservaLaAnterior`, `GetAsync_UnidadAjena_DebeDevolverForbidden`, `GetAsync_UnidadInexistente_DebeDevolverNotFound`).
- Integración: `tests/UniHome.IntegrationTests/UnitOccupancyEndpointTests.cs`.
- Seguridad: la autorización por titularidad (BOLA/IDOR) está cubierta por `GetAsync_UnidadAjena_DebeDevolverForbidden`. Cubierta además dinámicamente por el DAST (`tools/ci/zap_dast.py`) y por RESTler sobre el documento OpenAPI real.
