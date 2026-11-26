# `GET /api/v1/unidades/{unitId}/vista-previa`

| Campo | Valor |
|---|---|
| Módulo | M05 – Propiedades y unidades |
| HU | HU-080 |
| Operación OpenAPI | `GET /api/v1/unidades/{unitId}/vista-previa` |
| Controlador | `src/UniHome.Api/Controllers/UnitsController.cs` → `Preview` (EP-M05-06) |

## Autenticación y autorización
Requiere Bearer. La unidad debe pertenecer al llamador (`403` si no).

## Request
Parámetro de ruta `unitId` (Guid).

## Response
`200 OK` con `UnidadVistaPreviaResponse` (una tarjeta: foto principal, precio, tipo, distancia, `paraQuien`, `arrendadorVerificado`, `esFavorito: null`, estado de publicación, estado de ocupación).

## Validaciones
Ninguna de entrada más allá del formato de `unitId`.

## Códigos de error
| Código | Causa |
|---|---|
| 401 | Sin sesión válida. |
| 403 | La unidad no pertenece al llamador. |
| 404 | La unidad o su propiedad no existen. |
| 503 | Falla de dependencia. |

## Reglas de negocio
- `GetUnitPreviewService.GetAsync`: la distancia solo se calcula con coordenadas reales de la propiedad y la universidad; el tiempo de traslado queda siempre `null` porque no existe un motor de rutas aprobado (no se inventa un valor).
- No declara publicada una unidad en `Borrador`: es solo una vista previa para el propio titular, en cualquier estado.
- `arrendadorVerificado` refleja el expediente M06 aprobado del propio llamador.

## Controles de seguridad aplicados
- Verificación de pertenencia del recurso antes de construir la vista.

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/GetUnitPreviewServiceTests.cs::Preview_DebeConstruirDesdeDatosPersistidosSinDeclararPublicada`, `::Preview_UnidadAjena_DebeDevolverForbidden`.
- Integración: `tests/UniHome.IntegrationTests/PublicationEndpointTests.cs::FlujoCompleto_DebeCrearPropiedadUnidadFotosServiciosZonaVistaPreviaYTerminos` (incluye una llamada previa a `vista-previa` antes de crear la propiedad, usada como fixture de precondición).
- Seguridad: cobertura general de ZAP/RESTler sobre el documento OpenAPI real.
