# `GET /api/v1/universidades`

| Campo | Valor |
|---|---|
| Módulo | M03 – Universidades |
| HU | HU-007 / HU-075 |
| Operación OpenAPI | `GET /api/v1/universidades` |
| Controlador | `src/UniHome.Api/Controllers/UniversitiesController.cs` → `Search` (EP-M03-01) |

## Autenticación y autorización
Pública (`[AllowAnonymous]`). Sin verificación de propiedad ni rol.

## Request
Query param `buscar` (opcional, string).

## Response
`200 OK` con arreglo de `UniversidadResumenResponse` (`id`, `nombreYCampus`); hasta 10 resultados, solo universidades **activas**.

## Validaciones
- `SearchUniversitiesService` valida el parámetro `buscar` (longitud/formato); un valor inválido devuelve `400`.
- Búsqueda insensible a mayúsculas y acentos.

## Códigos de error
| Código | Causa |
|---|---|
| 400 | Parámetro `buscar` inválido. |
| 503 | Dependencia no disponible. |

## Reglas de negocio
- Devuelve como máximo 10 universidades, solo activas: por eso una alta administrativa (`POST /admin/universidades`) aparece de inmediato en el buscador.
- No requiere autenticación: es el autocompletado público usado en registro/mapa.

## Controles de seguridad aplicados
- Sin datos personales ni administrativos en la respuesta (solo `id` y `nombreYCampus` de universidades activas).
- Sin rate limiting propio; endpoint de solo lectura, candidato natural para caché pública de corta duración (ver Bloque 2 de esta auditoría: caché Redis solo para GET públicos).

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/SearchUniversitiesServiceTests.cs`.
- Integración: `tests/UniHome.IntegrationTests/AdminUniversitiesEndpointTests.cs` (`Buscar_DebeIgnorarAcentosMayusculasYLimitarA10`, `Buscar_ConTerminoExcesivo_DebeResponder400`, `Buscar_SinCoincidencias_DebeResponderListaVacia`, `Crear_ComoAdmin_DebeResponder201YAparecerDeInmediatoEnBusqueda`), `tests/UniHome.IntegrationTests/MongoUniversityPersistenceTests.cs` (persistencia).
- Seguridad: recorrido dinámico de ZAP sobre el catálogo OpenAPI real; gramática RESTler generada del mismo documento.
