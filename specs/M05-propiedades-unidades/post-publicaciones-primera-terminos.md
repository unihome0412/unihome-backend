# `POST /api/v1/publicaciones/primera/terminos`

| Campo | Valor |
|---|---|
| Módulo | M05 – Propiedades y unidades |
| HU | HU-091 |
| Operación OpenAPI | `POST /api/v1/publicaciones/primera/terminos` |
| Controlador | `src/UniHome.Api/Controllers/PublicationsController.cs` → `AcceptTerms` (EP-M05-07, mismo código de referencia que `detalle`; ambos EP-M05-07 aparecen en el repo para operaciones distintas) |

## Autenticación y autorización
Requiere Bearer y mayoría de edad (`PublishingEligibilityService`, `403` si no se cumple). No hay verificación de un recurso ajeno porque es un registro propio de la cuenta.

## Request
Body `AceptarTerminosRequest { VersionTerminos: string? }`. Límite de tamaño: 8 KB.

## Response
`200 OK` con `AceptarTerminosResponse { Aceptada: true, VersionTerminos, FechaAceptacion }`.

## Validaciones
`AcceptTermsService.AcceptAsync` (`src/UniHome.Application/Publications/AcceptTerms/AcceptTermsService.cs`): `TermsAcceptance.Create` rechaza una versión vacía/invalida (`ArgumentException` → `400`, campo `versionTerminos`).

## Códigos de error
| Código | Causa |
|---|---|
| 400 | `versionTerminos` vacío o inválido. |
| 401 | Sin sesión válida. |
| 403 | La cuenta no tiene 18 años o más. |
| 413 | Cuerpo mayor a 8 KB. |
| 422 | Los términos de esta versión ya fueron aceptados (el controlador traduce `Conflict` del dominio a `422`, no a `409`). |
| 503 | Falla de dependencia. |

## Reglas de negocio
- Registra la aceptación **verificable** de una versión concreta de términos; no envía nada a revisión ni crea expedientes de M06 (eso lo hace `publicaciones/primera/enviar`).
- Repetir la aceptación de la misma versión es un conflicto explícito (`422`), no un 200 idempotente: el controlador detecta `OperationStatus.Conflict` y lo traduce a `422 Unprocessable Entity` en vez del `409` por defecto de `ToActionResult`.

## Controles de seguridad aplicados
- Revalidación de elegibilidad por edad en servidor.
- Límite de tamaño de solicitud de 8 KB.

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/AcceptTermsServiceTests.cs::Accept_VersionValida_DebeRegistrarAceptacion`, `::Accept_VersionRepetida_DebeDevolverConflict`, `::Accept_MenorDeEdad_DebeDevolverForbidden`.
- Integración: `tests/UniHome.IntegrationTests/PublicationEndpointTests.cs::FlujoCompleto_DebeCrearPropiedadUnidadFotosServiciosZonaVistaPreviaYTerminos`, `::MenorDeEdad_NoPuedeCrearPropiedadNiAceptarTerminos`; también ejercitado como precondición en `tests/UniHome.IntegrationTests/MongoPublicationPersistenceTests.cs` (opt-in).
- Seguridad: cobertura general de ZAP/RESTler sobre el documento OpenAPI real.
