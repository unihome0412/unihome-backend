# `GET /api/v1/perfil/mi-perfil`

| Campo | Valor |
|---|---|
| Módulo | M02 – Perfil |
| HU | HU-002 / HU-008 |
| Operación OpenAPI | `GET /api/v1/perfil/mi-perfil` |
| Controlador | `src/UniHome.Api/Controllers/ProfileController.cs` → `GetMyProfile` (EP-M02-01), bajo `[Authorize]` a nivel de controlador |

## Autenticación y autorización
Requiere `Authorization: Bearer`. Usa el claim `sub` del propio token; siempre devuelve el perfil del llamador, nunca de otra cuenta.

## Request
Sin parámetros.

## Response
`200 OK` con `PerfilResponse`: `nombreCompleto`, `correo`, `fechaNacimiento`, `genero`, `universidadActual` (resumen o `null`), `etiquetaCuenta`, `vinculadoAUnidad`, `puedePublicar`, `estadoPrimeraPublicacion`. No expone hash de contraseña ni claims administrativos. Cabecera `Cache-Control: no-store` aplicada por el middleware global de endurecimiento para el prefijo `/api/v1/perfil` (`src/UniHome.Api/Program.cs:814-823`), no por un `SetNoStore()` propio del controlador.

## Validaciones
- Claim `sub` debe parsear como `Guid` en formato `"N"`; si no, o si el perfil no existe, `401`.

## Códigos de error
| Código | Causa |
|---|---|
| 401 | Token inválido/ausente, o la cuenta detrás del claim ya no existe. |
| 503 | Dependencia no disponible. |

## Reglas de negocio
- `universidadActual` refleja `Account.CurrentUniversityId` (HU-087): `null` hasta que la cuenta la fija con `PUT /perfil/mi-universidad` o desde el mapa.
- `vinculadoAUnidad` es `true` cuando la cuenta tiene al menos un vínculo de ocupación activo como inquilino (M09).
- La barra de navegación y la visibilidad del panel son responsabilidad de Android; el backend solo entrega los indicadores y revalida cada ruta administrativa en servidor.

## Controles de seguridad aplicados
- Requiere Bearer; usa exclusivamente el claim de sesión, nunca un id de cuenta recibido del cliente (previene BOLA/IDOR sobre perfiles de otras cuentas).
- Respuesta sin datos administrativos ni credenciales.
- `Cache-Control: no-store` por middleware global (no por el controlador), igual que `/api/v1/auth`, `/api/v1/cuentas`, `/api/v1/expedientes`, `/api/v1/admin/expedientes`, `/api/v1/publicaciones` y `/api/v1/mis-propiedades`. **Nota de auditoría:** otras rutas autenticadas con datos propios (`/api/v1/favoritos`, `/api/v1/unidades/*`, `/api/v1/propiedades`, `/api/v1/conversaciones`, `/api/v1/notificaciones`, `/api/v1/admin/universidades`, `/api/v1/admin/cuentas`, `/api/v1/mi-resumen*`) no están en esa lista de prefijos; cada una depende de que su propio controlador llame `SetNoStore()`/`Response.Headers.CacheControl`. Se revisa caso por caso en cada spec de este catálogo y se consolida en el Bloque 4 (revisión de encabezados de seguridad).

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/GetProfileServiceTests.cs`.
- Integración: `tests/UniHome.IntegrationTests/ProfileEndpointTests.cs`.
- Seguridad: recorrido dinámico de ZAP sobre el catálogo OpenAPI real (`tools/ci/zap_dast.py`); gramática RESTler generada del mismo documento.
