# `GET /api/v1/favoritos`

| Campo | Valor |
|---|---|
| Módulo | M07 – Favoritos |
| HU | HU-085 |
| Operación OpenAPI | `GET /api/v1/favoritos` |
| Controlador | `src/UniHome.Api/Controllers/FavoritesController.cs` → `List` (EP-M07-03) |

## Autenticación y autorización
Requiere Bearer (`[Authorize]`). Sin sesión responde `401`. Solo devuelve los favoritos de la cuenta autenticada (no hay parámetro de cuenta en la query).

## Request
Query: `page` (≥1, por defecto 1), `pageSize` (1–100, por defecto `ListFavoritesService.DefaultPageSize=20`).

## Response
`200 OK` con `FavoritoResponse[]` (`unidadId`, `tarjeta`). Headers `X-Total-Count`, `X-Page`, `X-Page-Size`. `Cache-Control: no-store`.

## Validaciones
`ListFavoritesService.ListAsync` (`src/UniHome.Application/Favorites/ListFavoritesService.cs`):
- `page < 1` → `400`.
- `pageSize` fuera de 1–100 → `400`.

## Códigos de error
| Código | Causa |
|---|---|
| 400 | Paginación inválida. |
| 401 | Sin sesión válida. |
| 503 | Falla de dependencia. |

## Reglas de negocio
- La unidad permanece en la lista aunque deje de ser pública; la tarjeta refleja su estado actual (`ExplorationProjection.CurrentState`) y la foto solo se resuelve si el llamador puede leerla (pública o propia).
- Favoritos aislados por cuenta: nunca se mezclan los de otra cuenta.

## Controles de seguridad aplicados
- `Cache-Control: no-store` (datos propios de la cuenta).
- Aislamiento estricto por `accountId` del token, no por parámetro de cliente.

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/ExplorationAndFavoritesServiceTests.cs::Favoritos_SonIdempotentesYConservanUnidadNoPublica` (ejercita `ListFavoritesService.ListAsync`, incluido el aislamiento entre cuentas).
- Integración: `tests/UniHome.IntegrationTests/ExplorationEndpointTests.cs::Favoritos_SinSesion_DebenResponder401`, `::Favoritos_EstanAisladosPorCuenta`, `::Favoritos_Paginacion_DebeRespetarPaginaYTotal`.
- Seguridad: cobertura general de ZAP/RESTler sobre el documento OpenAPI real (`tools/ci/zap_dast.py`, `tools/ci/restler/normalize_openapi.py`).
