# `GET /api/v1/unidades/{unitId}/ficha`

| Campo | Valor |
|---|---|
| Módulo | M04 – Exploración pública |
| HU | HU-044 / HU-045 (y HU-071: nunca correo ni teléfono del arrendador) |
| Operación OpenAPI | `GET /api/v1/unidades/{unitId}/ficha` |
| Controlador | `src/UniHome.Api/Controllers/ExplorationController.cs` → `Detail` (EP-M04-04) |

## Autenticación y autorización
Requiere Bearer (`[Authorize]`). Sin sesión responde `401` antes de consultar el repositorio. No exige ser el propietario: cualquier cuenta autenticada puede ver la ficha de una unidad pública.

## Request
Parámetro de ruta `unitId` (Guid).

## Response
`200 OK` con `UnidadFichaResponse`: foto principal, galería, precio, tipo, distancia/tiempo de traslado, `paraQuien`, `arrendadorVerificado`, `esFavorito`, ocupación, lugares, tamaño, baño, mobiliario, servicios incluidos/aparte, tipo de propiedad. `Cache-Control: no-store` siempre (datos ligados a la sesión).

## Validaciones
Ninguna de entrada más allá del formato de `unitId` y de la sesión.

## Códigos de error
| Código | Causa |
|---|---|
| 401 | Sin sesión válida. |
| 404 | La unidad no existe o no es públicamente visible. |
| 503 | Falla de dependencia. |

## Reglas de negocio
- Misma regla de visibilidad pública que `tarjeta` (`ExplorationProjection.IsPublic`).
- `GetUnitDetailService` separa servicios `Incluido` vs `Aparte` y nunca proyecta correo/teléfono del propietario (allowlist de campos en `UnidadFichaResponse`).

## Controles de seguridad aplicados
- Requiere sesión (a diferencia de `tarjeta`), con `Cache-Control: no-store`.
- Allowlist explícita de campos de respuesta: no hay forma de que el DTO filtre datos de contacto del arrendador.
- El test `OpenApi_DebeIncluirLosEndpointsDeM04YM07` (`tests/UniHome.IntegrationTests/OpenApiTests.cs`) verifica además que el esquema OpenAPI declare `security: Bearer` en esta ruta.

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/ExplorationAndFavoritesServiceTests.cs::Ficha_DebeSepararServiciosYGaleria`.
- Integración: `tests/UniHome.IntegrationTests/ExplorationEndpointTests.cs::EstadosNoPublicos_NoAparecenEnM04YFichaDevuelve404`; `tests/UniHome.IntegrationTests/OpenApiTests.cs::OpenApi_DebeIncluirLosEndpointsDeM04YM07` (contrato de seguridad).
- Seguridad: cobertura general de ZAP/RESTler sobre el documento OpenAPI real.
