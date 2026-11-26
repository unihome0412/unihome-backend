# `GET /api/v1/unidades/{unitId}/detalle`

| Campo | Valor |
|---|---|
| Módulo | M05 – Propiedades y unidades |
| HU | HU-035 |
| Operación OpenAPI | `GET /api/v1/unidades/{unitId}/detalle` |
| Controlador | `src/UniHome.Api/Controllers/UnitsController.cs` → `Detail` (EP-M05-07) |

## Autenticación y autorización
Requiere Bearer. La unidad debe pertenecer al llamador (`403` si no).

## Request
Parámetro de ruta `unitId` (Guid).

## Response
`200 OK` con `UnidadResponse` (contrato completo: servicios, zona/reglas, fotos, estado de publicación/ocupación), en cualquier estado de publicación.

## Validaciones
Ninguna de entrada más allá del formato de `unitId`.

## Códigos de error
| Código | Causa |
|---|---|
| 401 | Sin sesión válida. |
| 403 | La unidad no pertenece al llamador. |
| 404 | La unidad no existe. |
| 503 | Falla de dependencia. |

## Reglas de negocio
- `GetUnitDetailForOwnerService.GetAsync`: a diferencia de la vista previa (HU-080, una tarjeta), devuelve el mismo contrato `UnidadResponse` que los endpoints de alta/edición de M05, pero como una simple lectura, en cualquier estado de publicación (incluido `Borrador`).
- Pantalla de administración a la que el titular llega desde `GET /api/v1/mis-propiedades` (HU-033).

## Controles de seguridad aplicados
- Verificación de pertenencia del recurso antes de leer.

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/GetUnitDetailForOwnerServiceTests.cs::GetAsync_TitularDeLaUnidad_DevuelveLaUnidad`, `::GetAsync_UnidadAjena_DebeDevolverForbidden`, `::GetAsync_UnidadInexistente_DebeDevolverNotFound`.
- Integración: `tests/UniHome.IntegrationTests/PublicationEndpointTests.cs::Detalle_PropiaUnidad_DevuelveTodosLosCamposSinExponerCorreo`, `::Detalle_UnidadInexistente_DebeResponder404`, `::Detalle_SinSesion_DebeResponder401`.
- Seguridad: cobertura general de ZAP/RESTler sobre el documento OpenAPI real.
