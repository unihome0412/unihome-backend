# `GET /api/v1/admin/universidades`

| Campo | Valor |
|---|---|
| Módulo | M03 – Universidades |
| HU | HU-077 |
| Operación OpenAPI | `GET /api/v1/admin/universidades` |
| Controlador | `src/UniHome.Api/Controllers/AdminUniversitiesController.cs` → `List` (EP-M03-03), bajo `[Authorize(Policy = "Admin")]` |

## Autenticación y autorización
Requiere `Authorization: Bearer` y la política `Admin` revalidada en servidor.

## Request
Query params: `page` (por defecto 1), `pageSize` (por defecto `ListUniversitiesService.DefaultPageSize`).

## Response
`200 OK` con arreglo de `UniversidadResponse` (incluye activas e inactivas). Cabeceras `X-Total-Count`, `X-Page`, `X-Page-Size`.

## Validaciones
- `ListUniversitiesService` valida `page`/`pageSize` → `400` si son inválidos (p. ej. negativos o fuera de rango).

## Códigos de error
| Código | Causa |
|---|---|
| 400 | Paginación inválida. |
| 401 | Sin token. |
| 403 | Sin privilegio `Admin`. |
| 503 | Dependencia no disponible. |

## Reglas de negocio
- A diferencia de la búsqueda pública, el listado administrativo incluye universidades **inactivas**.

## Controles de seguridad aplicados
- Autorización por función revalidada en servidor (`AdminAccessOptions`).
- Paginación acotada en servidor (evita consumo excesivo de recursos con `pageSize` arbitrario).

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/ListUniversitiesServiceTests.cs`.
- Integración: `tests/UniHome.IntegrationTests/AdminUniversitiesEndpointTests.cs` (`Listar_ComoAdmin_DebeIncluirActivasEInactivasYTotalPaginado`, `Listar_ConPaginacionInvalida_DebeResponder400`, `Listar_SinToken_DebeResponder401`).
- Seguridad: recorrido dinámico de ZAP sobre el catálogo OpenAPI real; gramática RESTler generada del mismo documento.
