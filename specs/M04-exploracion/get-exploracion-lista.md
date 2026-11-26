# `GET /api/v1/exploracion/lista`

| Campo | Valor |
|---|---|
| Módulo | M04 – Exploración pública |
| HU | HU-037..043 / HU-084 |
| Operación OpenAPI | `GET /api/v1/exploracion/lista` |
| Controlador | `src/UniHome.Api/Controllers/ExplorationController.cs` → `List` (EP-M04-02) |

## Autenticación y autorización
Pública (`[AllowAnonymous]`). Si llega un Bearer válido, se usa solo para calcular `esFavorito` por tarjeta; sin sesión ese campo es `null`. No hay autorización de recurso porque el resultado es el mismo catálogo público.

## Request
Query (`ExploracionQuery`): igual que `mapa`, más `page` (≥1, por defecto 1) y `pageSize` (1–100, por defecto 20).

## Response
`200 OK` con `UnidadTarjetaResponse[]` (`id`, `fotoPrincipalUrl`, `precioMensualMxn`, `tipoUnidad`, `distanciaKm`, `tiempoTraslado`, `paraQuien`, `arrendadorVerificado`, `esFavorito`). Headers `X-Total-Count`, `X-Page`, `X-Page-Size`. Si hay sesión, `Cache-Control: no-store` (la lista personalizada no debe compartirse entre cuentas).

## Validaciones
`ExploreUnitsService.ListAsync` (mismo archivo que `mapa`):
- Mismas reglas de `universidadId`/`distanciaMaxKm`/`precioMaxMxn` que `mapa`.
- `page < 1` → `400` (`page`).
- `pageSize` fuera de 1–100 (`ExploreUnitsService.MaxPageSize`) → `400` (`pageSize`).

## Códigos de error
| Código | Causa |
|---|---|
| 400 | Filtros o paginación inválidos. |
| 404 | La universidad no existe o está inactiva. |
| 503 | Falla de dependencia (fail-closed). |

## Reglas de negocio
- Mismo filtro de visibilidad pública (`Aprobada` + `Disponible`) que `mapa`.
- Orden: sin distancia al final, luego por distancia, luego por precio, luego por `Id` (orden determinista para paginar).
- `arrendadorVerificado` viene de `IVerificationRepository.ListApprovedOwnerIdsAsync` (expediente M06 aprobado).

## Controles de seguridad aplicados
- `Cache-Control: no-store` cuando hay sesión, para no exponer `esFavorito` de una cuenta a otra vía caché compartida.
- Nunca expone correo/teléfono del propietario (solo campos de `UnidadTarjetaResponse`).

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/ExplorationAndFavoritesServiceTests.cs::Lista_Combinada_DebeFiltrarPorDistanciaPrecioOcupacionYPublico`, `::Lista_SinUbicacionUniversidad_DistanciaNulaYFiltroDistanciaFalla`, `::UmbralDeTraslado_DebeSerInclusivoEnUnPuntoCinco`.
- Integración: `tests/UniHome.IntegrationTests/ExplorationEndpointTests.cs::Exploracion_Publica_FiltraAprobadasDisponiblesYNoExponePii`, `::Exploracion_Validaciones_DebenResponder400`, `::Exploracion_SinUbicacionUniversidad_DistanciaNulaYFiltro400`; `tests/UniHome.IntegrationTests/ExplorationWithoutStorageTests.cs` (sin almacenamiento de fotos configurado).
- Seguridad: cobertura general de ZAP/RESTler sobre el documento OpenAPI real (ver nota en `get-exploracion-mapa.md`).
