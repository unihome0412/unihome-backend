# `POST /api/v1/unidades/unirse`

| Campo | Valor |
|---|---|
| Módulo | M09 – Ocupación |
| HU | HU-049 |
| Operación OpenAPI | `POST /api/v1/unidades/unirse` |
| Controlador | `src/UniHome.Api/Controllers/UnitOccupancyController.cs` → `JoinWithCode` (`JoinUnitWithCodeService.JoinAsync`) |

## Autenticación y autorización

`[Authorize]` (Bearer JWT). No hay restricción de rol: cualquier cuenta autenticada puede intentar unirse, salvo el propietario de la unidad (ver reglas).

## Request

- Body JSON `UnirseConCodigoRequest`: `codigo` (string).
- `[RequestSizeLimit(8 * 1024)]`.

## Response

`200 OK` con `VinculoOcupacionResponse`. `Cache-Control: no-store`.

## Validaciones

- `codigo` obligatorio (`400 ValidationProblemDetails` si está vacío).
- Código mal formado o inexistente: `400`.
- Código vencido: `410`.
- Tamaño de solicitud ≤ 8 KB (`413`).

## Códigos de error

| Código | Causa |
|---|---|
| 400 | `codigo` vacío, inexistente o mal formado. |
| 401 | Sesión inválida o ausente. |
| 403 | El propietario intenta unirse a su propia unidad (`SelfJoinForbidden`). |
| 409 | La unidad ya no tiene lugares disponibles (`UnitFull`). |
| 410 | El código de invitación venció (`ExpiredCode`). |
| 413 | El cuerpo de la solicitud excede 8 KB. |
| 503 | Falla de infraestructura. |

## Reglas de negocio

- Un inquilino ya vinculado recibe `200` de forma idempotente (`JoinAsync_InquilinoYaActivo_EsIdempotenteYNoDuplicaConteo`); uno que había dejado la unidad reactiva el mismo vínculo al volver a unirse (`JoinAsync_InquilinoConVinculoFinalizado_Reactiva`).
- **Cupo protegido con CAS de un solo documento:** `unidades.lugaresOcupados` es un contador atómico mantenido exclusivamente por `IUnitRepository.TryReserveSlotAsync` (`findOneAndUpdate` con filtro `lugaresOcupados < lugares` e `$inc`). Si dos solicitudes de unión llegan al mismo tiempo con un solo lugar libre, MongoDB solo aplica el incremento a una; la otra recibe `409` (README.md, "Sprint 6 / M09: ocupación").
- `vinculos_ocupacion` guarda un único documento por par inquilino/unidad (`_id` determinista `inquilino:unidad`): salir y volver a unirse reactiva el documento existente en vez de crear uno nuevo.

## Controles de seguridad aplicados

- CAS de un solo documento para evitar condición de carrera en el conteo de cupo (consumo de recursos / integridad del inventario de lugares).
- El propietario no puede unirse a su propia unidad (control de lógica de negocio contra abuso).
- `Cache-Control: no-store`.

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/JoinUnitWithCodeServiceTests.cs` (10 casos, incluido `JoinAsync_DosSolicitudesSimultaneasConUnLugar_SoloUnaDebeVincularse`: concurrencia local con un solo lugar).
- Integración: `tests/UniHome.IntegrationTests/UnitOccupancyEndpointTests.cs` (prueba de concurrencia HTTP, según README.md) y `tests/UniHome.IntegrationTests/MongoOccupancyPersistenceTests.cs::TryReserveSlotAsync_DosSolicitudesSimultaneasConUnLugar_SoloUnaDebeReservar` (concurrencia real contra MongoDB, opt-in) y `TryReserveSlotAsync_UnidadSinCampoLugaresOcupados_LoTrataComoCero`.
- Seguridad: el control de concurrencia sobre el cupo (consumo de recursos) está cubierto por las pruebas de concurrencia citadas. Cubierta además dinámicamente por el DAST (`tools/ci/zap_dast.py`) y por RESTler sobre el documento OpenAPI real.
