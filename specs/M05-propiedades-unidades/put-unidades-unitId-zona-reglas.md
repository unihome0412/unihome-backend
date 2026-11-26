# `PUT /api/v1/unidades/{unitId}/zona-reglas`

| Campo | Valor |
|---|---|
| Módulo | M05 – Propiedades y unidades |
| HU | HU-082 / HU-083 |
| Operación OpenAPI | `PUT /api/v1/unidades/{unitId}/zona-reglas` |
| Controlador | `src/UniHome.Api/Controllers/UnitsController.cs` → `ConfigureZoneRules` (EP-M05-05) |

## Autenticación y autorización
Requiere Bearer. La unidad debe pertenecer al llamador (`403` si no). Límite de tamaño: 8 KB.

## Request
Body `ZonaReglasRequest { InformacionZona: string?, ReglasConvivencia: string? }`.

## Response
`200 OK` con `UnidadResponse` actualizada.

## Validaciones
`ConfigureUnitZoneRulesService.ConfigureAsync` (`src/UniHome.Application/Units/ZoneRules/ConfigureUnitZoneRulesService.cs`): delega en `unit.ConfigureZoneRules`, que lanza `ArgumentException` (→ `400`) si el dominio rechaza los valores (p. ej. longitud de texto).

## Códigos de error
| Código | Causa |
|---|---|
| 400 | Valores rechazados por la regla de dominio `Unit.ConfigureZoneRules`. |
| 401 | Sin sesión válida. |
| 403 | La unidad no pertenece al llamador. |
| 404 | La unidad no existe. |
| 413 | Cuerpo mayor a 8 KB. |
| 503 | Falla de dependencia. |

## Reglas de negocio
- No publica ni cambia el estado de publicación de la unidad; solo actualiza texto informativo.

## Controles de seguridad aplicados
- Verificación de pertenencia del recurso antes de modificar.
- Límite de tamaño de solicitud de 8 KB.

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/ConfigureUnitZoneRulesServiceTests.cs::Configure_DatosValidos_DebePersistirZonaYReglas`, `::Configure_UnidadAjena_DebeDevolverForbidden`.
- Integración: `tests/UniHome.IntegrationTests/PublicationEndpointTests.cs::FlujoCompleto_DebeCrearPropiedadUnidadFotosServiciosZonaVistaPreviaYTerminos`, `::OperacionesSobreRecursoAjeno_DebenResponder403`.
- Seguridad: cobertura general de ZAP/RESTler sobre el documento OpenAPI real.
