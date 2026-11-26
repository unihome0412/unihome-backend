# `POST /api/v1/admin/universidades`

| Campo | Valor |
|---|---|
| Módulo | M03 – Universidades |
| HU | HU-075 |
| Operación OpenAPI | `POST /api/v1/admin/universidades` |
| Controlador | `src/UniHome.Api/Controllers/AdminUniversitiesController.cs` → `Create` (EP-M03-02), bajo `[Authorize(Policy = "Admin")]` a nivel de controlador |

## Autenticación y autorización
Requiere `Authorization: Bearer` **y** la política `Admin` (`AdminAccessOptions.PolicyName`, `src/UniHome.Api/Configuration/AdminAccessOptions.cs`): el claim `sub` del token se revalida en el servidor contra `Admin__AccountIds__0..N`. Nunca se acepta un rol/claim de administrador enviado por el cliente.

## Request
Body JSON (`CrearUniversidadRequest`): `nombreYCampus`. Límite de 8 KB.

## Response
`201 Created` con `UniversidadResponse` (`id`, `nombreYCampus`, `activa`, `fechaAlta`).

## Validaciones
- `CreateUniversityService` valida `nombreYCampus` no vacío → `400`.
- Nombre normalizado (ignora mayúsculas, acentos y espacios sobrantes) duplicado → `409`.

## Códigos de error
| Código | Causa |
|---|---|
| 400 | `nombreYCampus` vacío. |
| 401 | Sin token. |
| 403 | Token válido sin privilegio `Admin`. |
| 409 | Nombre normalizado duplicado. |
| 413 | Cuerpo > 8 KB. |
| 503 | Dependencia no disponible. |

## Reglas de negocio
- El alta aparece de inmediato en el buscador público (`GET /universidades`) porque ambos leen el mismo catálogo activo.
- Nace siempre `activa: true`.

## Controles de seguridad aplicados
- Autorización por función revalidada en servidor (`AdminAccessOptions`), no por claim del cliente — control específico de autorización por función exigido en el Bloque 4 de esta auditoría (OWASP API5 Broken Function Level Authorization).
- Lista vacía de `Admin__AccountIds` deniega todas las rutas `/admin/*`.
- Límite de tamaño de cuerpo.

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/CreateUniversityServiceTests.cs`.
- Integración: `tests/UniHome.IntegrationTests/AdminUniversitiesEndpointTests.cs` (`Crear_ComoAdmin_DebeResponder201YAparecerDeInmediatoEnBusqueda`, `Crear_SinToken_DebeResponder401`, `Crear_ConUsuarioSinPrivilegioAdmin_DebeResponder403`, `Crear_ConClaimAdminFalso_DebeResponder403`, `Crear_ConNombreYCampusVacio_DebeResponder400`, `Crear_ConLongitudExcesiva_DebeResponder400`, `Crear_DuplicadoNormalizado_DebeResponder409`, `Crear_ConCuerpoExcesivo_DebeResponder413`).
- Seguridad: la prueba `Crear_ConClaimAdminFalso_DebeResponder403` cubre directamente el riesgo de escalamiento de privilegio por claim falsificado (OWASP API5); recorrido dinámico de ZAP y gramática RESTler sobre el catálogo OpenAPI real.
