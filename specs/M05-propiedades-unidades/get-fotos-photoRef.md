# `GET /api/v1/fotos/{photoRef}`

| Campo | Valor |
|---|---|
| Módulo | M05 – Propiedades y unidades |
| HU | No identificada explícitamente en el repo (comentario del controlador no cita HU/EP; es la entrega autorizada de las fotos de HU-032, vía DEC-04/Cloudinary) |
| Operación OpenAPI | `GET /api/v1/fotos/{photoRef}` |
| Controlador | `src/UniHome.Api/Controllers/UnitPhotosController.cs` → `Read` |

## Autenticación y autorización
Pública (`[AllowAnonymous]`) para fotos de unidades aprobadas y disponibles. Para cualquier otro estado (Borrador, En revisión, etc.) exige Bearer y que la cuenta sea el propietario de la unidad.

## Request
Parámetro de ruta `photoRef` (string): referencia opaca (no la clave real de almacenamiento) decodificada por `PhotoReference.TryDecode`.

## Response
`200 OK` con el binario de la foto (`File(...)`), `Content-Type` real detectado por firma binaria. `Cache-Control: public, max-age=300` si la unidad es pública; `private, no-store` en otro caso. Siempre `X-Content-Type-Options: nosniff`.

## Validaciones
- `photoRef` no decodificable → `404` (no distingue de "no existe", para no filtrar claves de almacenamiento).
- Unidad no encontrada por `storageKey` → `404`.

## Códigos de error
| Código | Causa |
|---|---|
| 401 | Unidad no pública y sin sesión. |
| 404 | `photoRef` inválido, unidad inexistente, foto no pública y cuenta no es el propietario, o el proveedor de almacenamiento no la tiene. |
| 503 | Falla del proveedor de almacenamiento. |

## Reglas de negocio
- La URL de Cloudinary y sus credenciales nunca salen del backend: el cliente solo ve `photoRef` y el binario se entrega vía este proxy (ver DEC-04, `docs/dec-04-cloudinary.md`).
- El criterio de "pública" es el mismo que M04: `PublicationState.Aprobada` + `UnitAvailability.Disponible`.

## Controles de seguridad aplicados
- Entrega autorizada (no hay URL directa al proveedor externo); un borrador ajeno nunca es accesible aunque se conozca el `photoRef`.
- `X-Content-Type-Options: nosniff` para evitar sniffing de contenido.
- `Cache-Control` diferenciado: `private, no-store` para contenido no público.

## Pruebas que la cubren
- Integración: `tests/UniHome.IntegrationTests/UnitPhotoEndpointTests.cs::FotoPublica_DebeEntregarseSinSesionSinExponerCloudinary`, `::FotoBorrador_SoloDebeEntregarseAlPropietario`, `::FotoOcupada_SoloDebeEntregarseAlPropietario`, `::FotoInexistente_DebeResponder404`; `tests/UniHome.IntegrationTests/ConfiguredCloudinaryPhotoEndpointTests.cs` (integración real con Cloudinary, opt-in).
- Unitarias: no aplica por diseño — `tests/UniHome.UnitTests/UniHome.UnitTests.csproj` no referencia `UniHome.Api` (ningún controlador de este repo tiene prueba unitaria directa; todos se prueban vía `tests/UniHome.IntegrationTests` con `WebApplicationFactory`). Se intentó agregar una prueba de controlador aislada para esta operación y se descartó explícitamente por romper esa convención sin necesidad: la lógica de autorización ya está cubierta de punta a punta por integración (ver arriba). `tests/UniHome.UnitTests/Infrastructure/CloudinaryUnitPhotoStorageTests.cs` cubre el adaptador de almacenamiento que sí vive en `Application`/`Infrastructure`.
- Seguridad: cobertura general de ZAP/RESTler sobre el documento OpenAPI real.
