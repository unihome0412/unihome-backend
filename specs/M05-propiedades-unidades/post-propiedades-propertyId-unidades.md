# `POST /api/v1/propiedades/{propertyId}/unidades`

| Campo | Valor |
|---|---|
| Módulo | M05 – Propiedades y unidades |
| HU | HU-031 / HU-078 / HU-079 |
| Operación OpenAPI | `POST /api/v1/propiedades/{propertyId}/unidades` |
| Controlador | `src/UniHome.Api/Controllers/UnitsController.cs` → `Create` (EP-M05-02) |

## Autenticación y autorización
Requiere Bearer y mayoría de edad (`PublishingEligibilityService`). La propiedad debe pertenecer al llamador (`property.OwnerAccountId != ownerAccountId` → `403`).

## Request
Parámetro de ruta `propertyId` (Guid). Body `CrearUnidadRequest`: `tipoUnidad`, `ocupacion`, `lugares`, `precioMensualPorLugarMxn`, `tamanoM2`, `paraQuien`, `bano`, `mobiliario`, `muebles[]`, `plantillaBase` (todos opcionales si se usa plantilla). Límite de tamaño: 8 KB.

## Response
`201 Created` con `UnidadResponse` (contrato completo de unidad, en `Borrador`).

## Validaciones
`CreateUnitService.CreateAsync` (`src/UniHome.Application/Units/CreateUnit/CreateUnitService.cs`):
- `PlantillaBase` rellena campos omitidos vía `UnitTemplates.Get`; el cliente puede sobrescribir cualquier campo y el servidor vuelve a validar todo.
- Campos obligatorios tras aplicar plantilla: `tipoUnidad`, `ocupacion`, `paraQuien`, `bano`, `mobiliario` (cada uno con su propio error si falta o es un enum fuera de rango).
- `precioMensualPorLugarMxn` y `tamanoM2` deben ser `> 0`.
- Si `ocupacion == Compartida`, `lugares` es obligatorio.

## Códigos de error
| Código | Causa |
|---|---|
| 400 | Campos obligatorios faltantes/enum inválido, precio/tamaño ≤ 0, `lugares` faltante en ocupación compartida, o regla de dominio de `Unit.Create`. |
| 401 | Sin sesión válida. |
| 403 | La cuenta no es propietaria de `propertyId`, o no tiene 18+. |
| 404 | La propiedad no existe. |
| 413 | Cuerpo mayor a 8 KB. |
| 503 | Falla de dependencia. |

## Reglas de negocio
- Unidad individual fuerza `lugares = 1` sin importar lo recibido.
- La plantilla solo rellena campos omitidos; nunca sobrescribe lo que el cliente envió explícitamente.

## Controles de seguridad aplicados
- Verificación de pertenencia de la propiedad antes de crear la unidad (evita IDOR entre propietarios).
- Límite de tamaño de solicitud de 8 KB.

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/CreateUnitServiceTests.cs::Create_ConPlantilla_RellenaCamposOmitidosYFuerzaUnLugar`, `::Create_ConPropiedadAjena_DebeDevolverForbidden`, `::Create_MenorDeEdad_DebeDevolverForbidden`.
- Integración: `tests/UniHome.IntegrationTests/PublicationEndpointTests.cs::CrearUnidad_IndividualFuerzaUnLugarYPlantillaRellenaCampos`, `::OperacionesSobreRecursoAjeno_DebenResponder403`.
- Seguridad: cobertura general de ZAP/RESTler sobre el documento OpenAPI real; `OperacionesSobreRecursoAjeno_DebenResponder403` ejercita directamente el control BOLA/IDOR de esta ruta.
