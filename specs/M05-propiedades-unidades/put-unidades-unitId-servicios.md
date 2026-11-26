# `PUT /api/v1/unidades/{unitId}/servicios`

| Campo | Valor |
|---|---|
| Módulo | M05 – Propiedades y unidades |
| HU | HU-081 |
| Operación OpenAPI | `PUT /api/v1/unidades/{unitId}/servicios` |
| Controlador | `src/UniHome.Api/Controllers/UnitsController.cs` → `ConfigureServices` (EP-M05-04) |

## Autenticación y autorización
Requiere Bearer. La unidad debe pertenecer al llamador (`403` si no). Límite de tamaño: 8 KB.

## Request
Body `ServiciosRequest { Servicios: ServicioInput[]? }`, cada `ServicioInput` con `nombre` (enum `ServiceName`), `modalidad` (enum `ServiceMode`: `Incluido`/`Aparte`), `montoMensualMxn` (`decimal?`).

## Response
`200 OK` con `UnidadResponse` actualizada.

## Validaciones
`ConfigureUnitServicesService.ConfigureAsync` (`src/UniHome.Application/Units/Services/ConfigureUnitServicesService.cs`):
- `servicios` nulo → `400` ("La lista de servicios es obligatoria").
- `ServiceCharge.Create` (dominio) rechaza un servicio `Aparte` sin `montoMensualMxn` → `400`.

## Códigos de error
| Código | Causa |
|---|---|
| 400 | Lista nula, o servicio `Aparte` sin monto. |
| 401 | Sin sesión válida. |
| 403 | La unidad no pertenece al llamador. |
| 404 | La unidad no existe. |
| 413 | Cuerpo mayor a 8 KB. |
| 503 | Falla de dependencia. |

## Reglas de negocio
- **Reemplazo completo:** la solicitud reemplaza todo el catálogo de servicios de la unidad, no lo combina con el existente.
- Un servicio `Aparte` no se suma a la renta base; solo un servicio `Incluido` no requiere monto.

## Controles de seguridad aplicados
- Verificación de pertenencia del recurso antes de modificar.
- Límite de tamaño de solicitud de 8 KB.

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/ConfigureUnitServicesServiceTests.cs::Configure_AparteSinMonto_DebeDevolverValidacion`, `::Configure_ConServiciosValidos_DebeReemplazar`, `::Configure_UnidadAjena_DebeDevolverForbidden`.
- Integración: `tests/UniHome.IntegrationTests/PublicationEndpointTests.cs::Servicios_AparteSinMonto_DebeResponder400`, `::FlujoCompleto_DebeCrearPropiedadUnidadFotosServiciosZonaVistaPreviaYTerminos`, `::OperacionesSobreRecursoAjeno_DebenResponder403`.
- Seguridad: cobertura general de ZAP/RESTler sobre el documento OpenAPI real.
