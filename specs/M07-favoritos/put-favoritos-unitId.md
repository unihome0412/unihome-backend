# `PUT /api/v1/favoritos/{unitId}`

| Campo | Valor |
|---|---|
| Módulo | M07 – Favoritos |
| HU | HU-085 |
| Operación OpenAPI | `PUT /api/v1/favoritos/{unitId}` |
| Controlador | `src/UniHome.Api/Controllers/FavoritesController.cs` → `Add` (EP-M07-01) |

## Autenticación y autorización
Requiere Bearer. Sin sesión responde `401`. No exige propiedad de la unidad: cualquier cuenta puede marcar como favorita una unidad que pueda ver (pública) o la suya propia.

## Request
Parámetro de ruta `unitId` (Guid). Sin body.

## Response
`204 No Content`. `Cache-Control: no-store`.

## Validaciones
Ninguna de entrada más allá del formato de `unitId`.

## Códigos de error
| Código | Causa |
|---|---|
| 401 | Sin sesión válida. |
| 404 | La unidad no existe, o no es pública y no pertenece al llamador (`AddFavoriteService`). |
| 503 | Falla de dependencia. |

## Reglas de negocio
- `AddFavoriteService.AddAsync`: no se permite crear favoritos sobre unidades que el usuario no puede ver; el propietario sí puede guardar las suyas en Borrador/Revisión.
- **Idempotente:** agregar dos veces el mismo favorito no falla ni duplica.
- Si una unidad visible deja de serlo después, el favorito existente se conserva (ver `GET /favoritos`).

## Controles de seguridad aplicados
- Control de visibilidad antes de persistir (no permite "favoritar" unidades ajenas no públicas).
- `Cache-Control: no-store`.

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/ExplorationAndFavoritesServiceTests.cs::Favoritos_SonIdempotentesYConservanUnidadNoPublica`.
- Integración: `tests/UniHome.IntegrationTests/ExplorationEndpointTests.cs::Favoritos_SonIdempotentesYConservanEstadoNoPublico`, `::Favoritos_SinSesion_DebenResponder401`, `::Favorito_UnidadNoPublicaAjena_DebeResponder404`.
- Seguridad: cobertura general de ZAP/RESTler sobre el documento OpenAPI real.
