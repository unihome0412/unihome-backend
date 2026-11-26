# `POST /api/v1/unidades/{unitId}/reparaciones`

| Campo | Valor |
|---|---|
| Módulo | M11 – Reparaciones |
| HU | HU-062 / HU-063 |
| Operación OpenAPI | `POST /api/v1/unidades/{unitId}/reparaciones` |
| Controlador | `src/UniHome.Api/Controllers/ReparacionesController.cs` → `Create` (`CreateReparacionService.CreateAsync`) |

## Autenticación y autorización

`[Authorize]`. **Acceso compartido:** el titular de la unidad o un inquilino con vínculo de ocupación activo (M09) puede crear un reporte; cualquier otra cuenta recibe `403` (`CreateAsync_CuentaSinAccesoALaUnidad_DevuelveForbidden`).

## Request

- Parámetro de ruta: `unitId` (`Guid`).
- `multipart/form-data` (`[Consumes("multipart/form-data")]`), `[FromForm] CrearReparacionRequest`: `titulo`, `descripcion`, `foto` (archivo opcional).
- `[RequestSizeLimit(UnitPhotoValidator.MaxRequestBytes)]` (límite mayor que el resto del módulo, por la foto).

## Response

`201 Created` con `ReparacionResponse`.

## Validaciones

- `titulo` vacío: `400` (`ValidationProblemDetails`, `CreateAsync_ConTituloVacio_DevuelveValidationFailed`).
- Si se envía `foto`, se reutiliza la validación de contenido real de fotos de unidad (`UnitPhotoValidator`); una foto inválida (p. ej. excede `MaxBytesPerPhoto` o no pasa la validación de firma binaria) responde `400` **sin guardar el reporte ni almacenar la imagen** (`CreateAsync_ConFotoInvalida_DevuelveValidationFailedSinGuardarNiAlmacenar`).
- Tamaño total de la solicitud limitado a `UnitPhotoValidator.MaxRequestBytes` (`413` si se excede).

## Códigos de error

| Código | Causa |
|---|---|
| 400 | `titulo` vacío o `foto` inválida. |
| 401 | Sesión inválida o ausente. |
| 403 | La cuenta no tiene acceso a la unidad (no es titular ni inquilino con vínculo activo). |
| 404 | La unidad no existe. |
| 413 | El cuerpo de la solicitud excede el límite permitido. |
| 503 | Falla de infraestructura. |

## Reglas de negocio

- **Acceso compartido, cambio de estado exclusivo del titular:** levantar un reporte no requiere ser el propietario, basta con tener un vínculo de ocupación activo en la unidad o ser su titular (README.md, "Sprint 8 ... M11"; `CreateAsync_ComoPropietarioSinFoto_DebeCrearReporteEnPendiente`, `CreateAsync_ComoInquilinoActivoConFoto_DebeAlmacenarLaFoto`).
- La fotografía (cuando se envía) reutiliza la validación de contenido y el almacenamiento de Cloudinary de las fotos de unidad.

## Controles de seguridad aplicados

- Autorización por pertenencia a la unidad (titular o inquilino activo), no solo por sesión válida (mitiga BOLA/IDOR sobre `unitId`).
- Validación de la foto por firma binaria real (vía `UnitPhotoValidator`, la misma usada en `POST /unidades/{unitId}/fotos`), no por extensión/MIME declarado — mitigación de subida de archivos maliciosos.
- Falla atómica: una foto inválida no deja el reporte creado a medias (sin guardar ni almacenar).

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/CreateReparacionServiceTests.cs` (6 casos, incluido `CreateAsync_CuentaSinAccesoALaUnidad_DevuelveForbidden` y `CreateAsync_ConFotoInvalida_DevuelveValidationFailedSinGuardarNiAlmacenar`).
- Integración: `tests/UniHome.IntegrationTests/ReparacionesEndpointTests.cs`.
- Seguridad: la autorización por pertenencia (BOLA/IDOR) y la validación de archivo subido están cubiertas por las pruebas unitarias citadas. Cubierta además dinámicamente por el DAST (`tools/ci/zap_dast.py`) y por RESTler sobre el documento OpenAPI real.
