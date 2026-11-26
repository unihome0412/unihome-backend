# `GET /api/v1/exploracion/mapa`

| Campo | Valor |
|---|---|
| Módulo | M04 – Exploración pública |
| HU | HU-037..043 / HU-084 |
| Operación OpenAPI | `GET /api/v1/exploracion/mapa` |
| Controlador | `src/UniHome.Api/Controllers/ExplorationController.cs` → `Map` (EP-M04-01) |

## Autenticación y autorización
Pública (`[AllowAnonymous]`). No requiere Bearer; no hay verificación de propiedad porque el resultado es el mismo para cualquier visitante.

## Request
Query (`ExploracionQuery`, `src/UniHome.Api/Contracts/Exploration/ExploracionQuery.cs`):
- `universidadId` (Guid, obligatorio).
- `distanciaMaxKm` (decimal, opcional).
- `precioMaxMxn` (decimal, opcional).
- `soloIndividual` (bool, opcional).
- `paraQuien` (enum `ForWhom` por nombre string, opcional; `ForWhomQueryModelBinder` rechaza valores numéricos).
- `page` / `pageSize` (no se usan en este endpoint, solo en `lista`).

## Response
`200 OK` con `UnidadMapaResponse[]`: `id`, `latitud`, `longitud`, `precioMensualMxn`. Solo unidades `Aprobada` + `Disponible` con coordenadas de su propiedad.

## Validaciones
En `ExploreUnitsService.ValidateFilters` (`src/UniHome.Application/Exploration/ExploreUnitsService.cs`):
- `universidadId` vacío → `400` (`"universidadId": "La universidad es obligatoria."`).
- `distanciaMaxKm <= 0` → `400`.
- `precioMaxMxn <= 0` → `400`.
- Si se pide `distanciaMaxKm` pero la universidad no tiene ubicación registrada → `400` (`distanciaMaxKm`).

## Códigos de error
| Código | Causa |
|---|---|
| 400 | Filtros inválidos (ver Validaciones). |
| 404 | La universidad no existe o está inactiva. |
| 503 | Falla de MongoDB u otra dependencia (fail-closed). |

## Reglas de negocio
- Solo unidades en `PublicationState.Aprobada` y `UnitAvailability.Disponible` (`ExploreUnitsService.LoadCandidatesAsync`).
- La distancia usa Haversine (`ExplorationProjection`), nunca un motor de rutas externo; sin ubicación de universidad no hay distancia.
- No expone datos del propietario (correo, teléfono).

## Controles de seguridad aplicados
- Endpoint público de solo lectura, sin datos personales.
- Filtra estrictamente por estado público antes de proyectar coordenadas (evita exponer borradores/unidades no aprobadas).

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/ExplorationAndFavoritesServiceTests.cs::Mapa_NoIncluyeUnidadesNoPublicasNiSinCoordenadas`.
- Integración: `tests/UniHome.IntegrationTests/ExplorationEndpointTests.cs::Exploracion_Validaciones_DebenResponder400`, `::UniversidadInexistenteOInactiva_DebeResponder404`, `::EstadosNoPublicos_NoAparecenEnM04YFichaDevuelve404`; también `tests/UniHome.IntegrationTests/MongoExplorationPersistenceTests.cs` (persistencia real, opt-in).
- Seguridad: cubierta dinámicamente por el DAST de ZAP (`tools/ci/zap_dast.py`), que recorre todas las rutas `/api/v1/*` del documento OpenAPI real; y por la gramática RESTler generada del mismo documento (`tools/ci/restler/normalize_openapi.py`). No hay un caso dedicado adicional más allá de la cobertura general.
