# `PUT /api/v1/admin/universidades/{id}/estado`

| Campo | Valor |
|---|---|
| Módulo | M03 – Universidades |
| HU | HU-077 |
| Operación OpenAPI | `PUT /api/v1/admin/universidades/{id}/estado` |
| Controlador | `src/UniHome.Api/Controllers/AdminUniversitiesController.cs` → `ChangeStatus` (EP-M03-05), bajo `[Authorize(Policy = "Admin")]` |

## Autenticación y autorización
Requiere `Authorization: Bearer` y la política `Admin` revalidada en servidor.

## Request
Parámetro de ruta `id` (`Guid`). Body JSON (`CambiarEstadoUniversidadRequest`): `activa` (bool, `[JsonRequired]`). Límite de 8 KB.

## Response
`200 OK` con `UniversidadResponse` actualizado.

## Validaciones
- `activa` es requerido en el JSON (`[JsonRequired]`); su ausencia produce un error de deserialización, no un `400` de validación de negocio con este contrato (no hay `ProducesResponseType 400` declarado en este endpoint).
- `id` inexistente → `404`.

## Códigos de error
| Código | Causa |
|---|---|
| 401 | Sin token. |
| 403 | Sin privilegio `Admin`. |
| 404 | `id` inexistente. |
| 413 | Cuerpo > 8 KB. |
| 503 | Dependencia no disponible. |

## Reglas de negocio
- Activación/desactivación **lógica**, sin borrado.
- Al desactivar, la universidad deja de aparecer en la búsqueda pública (`GET /universidades`) pero se conserva en el catálogo administrativo (`GET /admin/universidades`), visible junto con las activas.

## Controles de seguridad aplicados
- Autorización por función revalidada en servidor.
- No hay borrado físico: la desactivación es reversible, reduce el riesgo de pérdida de datos por un alta duplicada o un alta errónea.

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/ChangeUniversityStatusServiceTests.cs`.
- Integración: `tests/UniHome.IntegrationTests/AdminUniversitiesEndpointTests.cs` (`CambiarEstado_Desactivar_DebeOcultarDeBusquedaYConservarEnAdmin`, `CambiarEstado_SinActiva_DebeResponder400`, `CambiarEstado_Inexistente_DebeResponder404`, `CambiarEstado_ConClaimAdminFalso_DebeResponder403`).
- Seguridad: la prueba `CambiarEstado_ConClaimAdminFalso_DebeResponder403` cubre el riesgo de escalamiento de privilegio por claim falsificado (OWASP API5); recorrido dinámico de ZAP y gramática RESTler sobre el catálogo OpenAPI real.

> Nota de evidencia: la prueba `CambiarEstado_SinActiva_DebeResponder400` indica que, en la práctica, omitir `activa` sí produce `400` (probablemente vía el binder de `System.Text.Json`/`[ApiController]`, que traduce errores de deserialización a `ValidationProblemDetails` automáticamente), aunque el controlador no declara `ProducesResponseType(400)` para esta acción. Ver Bloque 1 (matriz de pruebas) para dejar constancia de esta discrepancia de documentación OpenAPI vs. comportamiento real.
