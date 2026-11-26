# `PUT /api/v1/admin/universidades/{id}`

| Campo | Valor |
|---|---|
| Módulo | M03 – Universidades |
| HU | HU-077 |
| Operación OpenAPI | `PUT /api/v1/admin/universidades/{id}` |
| Controlador | `src/UniHome.Api/Controllers/AdminUniversitiesController.cs` → `Update` (EP-M03-04), bajo `[Authorize(Policy = "Admin")]` |

## Autenticación y autorización
Requiere `Authorization: Bearer` y la política `Admin` revalidada en servidor.

## Request
Parámetro de ruta `id` (`Guid`, restringido por `{id:guid}`). Body JSON (`EditarUniversidadRequest`): `nombreYCampus`. Límite de 8 KB.

## Response
`200 OK` con `UniversidadResponse` actualizado.

## Validaciones
- `UpdateUniversityService` valida `nombreYCampus` no vacío → `400`.
- `id` inexistente → `404`.
- Nombre normalizado duplicado contra otra universidad → `409`.
- Edición concurrente perdedora (otra actualización ganó primero) → `409` (`UpdateUniversityStatus.Conflict`).

## Códigos de error
| Código | Causa |
|---|---|
| 400 | `nombreYCampus` vacío o inválido. |
| 401 | Sin token. |
| 403 | Sin privilegio `Admin`. |
| 404 | `id` inexistente. |
| 409 | Nombre duplicado, o conflicto de concurrencia (la universidad fue modificada por otra operación). |
| 413 | Cuerpo > 8 KB. |
| 503 | Dependencia no disponible. |

## Reglas de negocio
- Misma normalización de nombre (ignora mayúsculas/acentos/espacios) y protección de duplicados que el alta.
- Concurrencia controlada: una segunda edición simultánea sobre la misma universidad pierde con `409` en vez de sobrescribir silenciosamente.

## Controles de seguridad aplicados
- Autorización por función revalidada en servidor.
- Control de concurrencia (evita condiciones de carrera en ediciones simultáneas).

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/UpdateUniversityServiceTests.cs`.
- Integración: `tests/UniHome.IntegrationTests/AdminUniversitiesEndpointTests.cs` (`Editar_ComoAdmin_DebeActualizarYReflejarEnBusqueda`, `Editar_DuplicadoNormalizado_DebeResponder409`, `Editar_Inexistente_DebeResponder404`, `Editar_SinPrivilegioAdmin_DebeResponder403`).
- Seguridad: recorrido dinámico de ZAP sobre el catálogo OpenAPI real; gramática RESTler generada del mismo documento.
