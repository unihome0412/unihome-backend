# `POST /api/v1/propiedades`

| Campo | Valor |
|---|---|
| Módulo | M05 – Propiedades y unidades |
| HU | HU-029 / HU-030 / HU-014 |
| Operación OpenAPI | `POST /api/v1/propiedades` |
| Controlador | `src/UniHome.Api/Controllers/PropertiesController.cs` → `Create` (EP-M05-01) |

## Autenticación y autorización
Requiere Bearer. Sin sesión responde `401`. Exige mayoría de edad (18+), revalidada en servidor vía `PublishingEligibilityService` (HU-014); no se confía en un flag del cliente.

## Request
Body `CrearPropiedadRequest`: `nombre`, `direccion`, `colonia` (todos `string?`), `tipoPropiedad` (enum `PropertyType`, `[JsonRequired]`), `universidadId` (Guid, `[JsonRequired]`), `latitud`/`longitud` (`double?`, opcionales), `piso`, `numeroUnidad` (`string?`). Límite de tamaño: 8 KB (`RequestSizeLimit`/`Request.ContentLength`).

## Response
`201 Created` con `PropiedadResponse`: datos de la propiedad, `estadoPublicacion` (`Borrador`), `fechaAlta`, `ubicacionSugerida` (true si no se recibieron coordenadas y se usó la ubicación de la universidad).

## Validaciones
`CreatePropertyService.CreateAsync` (`src/UniHome.Application/Properties/CreateProperty/CreatePropertyService.cs`):
- `universidadId` inexistente o inactiva → `400` (`universidadId`).
- Sin `latitud`/`longitud` y sin ubicación sugerida de la universidad → `400` (`latitud`).
- Reglas de dominio de `Property.Create` (p. ej. piso/número de unidad por tipo de propiedad) → `400` si lanzan `ArgumentException`.

## Códigos de error
| Código | Causa |
|---|---|
| 400 | Universidad inválida, sin ubicación para inferir coordenadas, o regla de dominio de `Property.Create` violada. |
| 401 | Sin sesión válida. |
| 403 | La cuenta no tiene 18 años o más (`PublishingEligibilityService`). |
| 409 | Ya existe una propiedad con los mismos datos para esta cuenta (`DuplicatePropertyException`). |
| 413 | Cuerpo mayor a 8 KB. |
| 503 | Falla de dependencia. |

## Reglas de negocio
- La propiedad nace en `Borrador`.
- Si faltan coordenadas, se usa la ubicación opcional del catálogo de universidades y se marca `ubicacionSugerida: true`.
- La edad se recalcula en servidor, nunca se acepta del cliente.

## Controles de seguridad aplicados
- Límite de tamaño de solicitud de 8 KB.
- Revalidación de elegibilidad por edad en servidor (no confía en el cliente).

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/CreatePropertyServiceTests.cs::Create_ConCoordenadas_DebeCrearBorrador`, `::Create_SinCoordenadas_UsaUbicacionSugerida`, `::Create_SinUbicacionSugerida_DebeDevolverValidacion`, `::Create_MenorDeEdad_DebeDevolverForbidden`, `::Create_Duplicado_DebeDevolverConflict`.
- Integración: `tests/UniHome.IntegrationTests/PublicationEndpointTests.cs::FlujoCompleto_DebeCrearPropiedadUnidadFotosServiciosZonaVistaPreviaYTerminos`, `::CrearPropiedad_Duplicada_DebeResponder409`, `::PropiedadEdificio_SinPisoNumero_DebeResponder400`, `::PropiedadCasa_IgnoraPisoYNumero`, `::MenorDeEdad_NoPuedeCrearPropiedadNiAceptarTerminos`.
- Seguridad: cobertura general de ZAP/RESTler sobre el documento OpenAPI real.
