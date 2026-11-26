# `POST /api/v1/unidades/{unitId}/fotos`

| Campo | Valor |
|---|---|
| Módulo | M05 – Propiedades y unidades |
| HU | HU-032 |
| Operación OpenAPI | `POST /api/v1/unidades/{unitId}/fotos` |
| Controlador | `src/UniHome.Api/Controllers/UnitsController.cs` → `AddPhotos` (EP-M05-03) |

## Autenticación y autorización
Requiere Bearer. La unidad debe pertenecer al llamador (`unit.OwnerAccountId != accountId` → `403`).

## Request
`multipart/form-data` (`AgregarFotosRequest.Fotos: List<IFormFile>`). Límite de tamaño total: `UnitPhotoValidator.MaxRequestBytes` = 52 MB; límite por foto: `UnitPhotoValidator.MaxBytesPerPhoto` = 5 MB.

## Response
`200 OK` con `UnidadResponse` actualizada (incluye `totalFotos`).

## Validaciones
- Cualquier archivo mayor a 5 MB → `400` (`fotos`), verificado antes de leer el contenido completo.
- Lista vacía de fotos → `400` (`AddUnitPhotosService`).
- Más de `Unit.MaxPhotos` (10) fotos en total → `400`.
- Firma binaria real (no extensión/Content-Type declarado): `UnitPhotoValidator.DetectContentType` solo acepta JPEG (`FF D8 FF`) o PNG (cabecera estándar); cualquier otro contenido → `400` ("Solo se permiten fotografías JPG o PNG con contenido válido").

## Códigos de error
| Código | Causa |
|---|---|
| 400 | Foto > 5 MB, lista vacía, > 10 fotos totales, o contenido que no es JPG/PNG real. |
| 401 | Sin sesión válida. |
| 403 | La unidad no pertenece al llamador. |
| 404 | La unidad no existe. |
| 409 | Otra carga modificó las fotos de la unidad concurrentemente (`TryAppendPhotosAsync` con CAS sobre el conteo esperado). |
| 413 | Cuerpo mayor a 52 MB. |
| 415 | Tipo de contenido no soportado (declarado por `[Consumes("multipart/form-data")]`). |
| 503 | Falla del proveedor de almacenamiento o de MongoDB. |

## Reglas de negocio
- El binario se delega a Cloudinary (DEC-04); MongoDB solo guarda metadatos (`storageKey`, `contentType`, `sizeBytes`, `sha256`).
- La primera foto subida cuando la unidad no tiene ninguna se marca `isPrimary`.
- Si falla el almacenamiento o el `CAS` de Mongo tras subir el binario, se limpia lo ya subido en el proveedor externo (`CleanupAsync`) para no dejar huérfanos.

## Controles de seguridad aplicados
- Validación de firma binaria real, no de extensión ni `Content-Type` declarado (mitiga subida de contenido malicioso disfrazado de imagen).
- Verificación de pertenencia del recurso antes de aceptar la carga.
- Límite de tamaño por archivo y por solicitud total.

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/AddUnitPhotosServiceTests.cs::Add_PngValido_DebeGuardarYMarcarPrimeraComoPrincipal`, `::Add_ContenidoFalso_DebeDevolverValidacion`, `::Add_MasDeDiez_DebeDevolverValidacion`, `::Add_CuandoAlmacenamientoFalla_DebePropagar503YLimpiar`, `::Add_CuandoMongoFalla_DebeEliminarLaFotoSubida`, `::Add_UnidadAjena_DebeDevolverForbidden`.
- Integración: `tests/UniHome.IntegrationTests/PublicationEndpointTests.cs::Fotos_ConContenidoFalsoODemasiadas_DebenResponder400`, `::FlujoCompleto_DebeCrearPropiedadUnidadFotosServiciosZonaVistaPreviaYTerminos`.
- Seguridad: cobertura general de ZAP/RESTler sobre el documento OpenAPI real; la validación de firma binaria es, en sí misma, un control de subida de archivos (OWASP API Security: Unrestricted Resource Consumption / malicious file upload).
