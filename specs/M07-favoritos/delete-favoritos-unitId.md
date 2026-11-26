# `DELETE /api/v1/favoritos/{unitId}`

| Campo | Valor |
|---|---|
| Módulo | M07 – Favoritos |
| HU | HU-085 |
| Operación OpenAPI | `DELETE /api/v1/favoritos/{unitId}` |
| Controlador | `src/UniHome.Api/Controllers/FavoritesController.cs` → `Remove` (EP-M07-02) |

## Autenticación y autorización
Requiere Bearer. Sin sesión responde `401`. Solo opera sobre favoritos de la propia cuenta (no recibe `accountId` de cliente).

## Request
Parámetro de ruta `unitId` (Guid). Sin body.

## Response
`204 No Content` siempre que haya sesión válida, exista o no el favorito previamente. `Cache-Control: no-store`.

## Validaciones
Ninguna de entrada más allá del formato de `unitId`.

## Códigos de error
| Código | Causa |
|---|---|
| 401 | Sin sesión válida. |
| 503 | Falla de dependencia. |

## Reglas de negocio
- **Baja idempotente:** `RemoveFavoriteService.RemoveAsync` responde `204` aunque el favorito no existiera; no hay `404` por diseño (a diferencia de `PUT`, que sí valida visibilidad antes de crear).

## Controles de seguridad aplicados
- Aislamiento por `accountId` del token.
- `Cache-Control: no-store`.

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/ExplorationAndFavoritesServiceTests.cs::Favoritos_SonIdempotentesYConservanUnidadNoPublica` (ejercita `RemoveFavoriteService.RemoveAsync` dos veces seguidas sobre el mismo favorito).
- Integración: `tests/UniHome.IntegrationTests/ExplorationEndpointTests.cs::RecursosInexistentes_DebenResponder404YDeleteIdempotente204`, `::Favoritos_SinSesion_DebenResponder401`.
- Seguridad: cobertura general de ZAP/RESTler sobre el documento OpenAPI real.
