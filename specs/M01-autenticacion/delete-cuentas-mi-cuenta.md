# `DELETE /api/v1/cuentas/mi-cuenta`

| Campo | Valor |
|---|---|
| Módulo | M01 – Autenticación y cuenta |
| HU | HU-096 |
| Operación OpenAPI | `DELETE /api/v1/cuentas/mi-cuenta` |
| Controlador | `src/UniHome.Api/Controllers/CuentasController.cs` → `DeleteMyAccount` (EP-M01-09) |

## Autenticación y autorización
Requiere `Authorization: Bearer` (`[Authorize]`). Usa el claim `sub` del propio token; no acepta un id de cuenta por parámetro (no se puede eliminar la cuenta de otro).

## Request
Body JSON (`EliminarCuentaRequest`): `contrasena`, `confirmacionPerdidaInformacion` (debe ser explícitamente `true`; su ausencia u omisión se trata igual que `false`). Límite de 8 KB.

## Response
`204 No Content`, sin cuerpo. Cabecera `Cache-Control: no-store`.

## Validaciones
- `confirmacionPerdidaInformacion` ausente o `false` → `400` (campo `confirmacionPerdidaInformacion`).
- Contraseña incorrecta → `401`.

## Códigos de error
| Código | Causa |
|---|---|
| 400 | Falta la confirmación explícita de pérdida de información. |
| 401 | Sesión inválida o contraseña incorrecta. |
| 403 | La cuenta es administrativa (`AdminCannotSelfDelete`): **un administrador no puede eliminar su propia cuenta por esta vía**. |
| 409 | Vínculo de ocupación activo como inquilino (M09), o alguna propiedad propia todavía con inquilinos activos. |
| 413 | Cuerpo > 8 KB. |
| 503 | Dependencia no disponible. |

## Reglas de negocio
- Borrado lógico (`AccountStatus.Deleted`); revoca **todas** las sesiones en todos los dispositivos (a diferencia de `logout`, que solo revoca la sesión actual).
- Tras eliminar, la cuenta no puede iniciar sesión, renovar tokens ni seguir usando un access token emitido antes (BE-SEC-008: el estado se revisa en cada petición autenticada).
- `409` si la cuenta tiene un vínculo de ocupación activo como inquilino, o si alguna de sus propiedades todavía tiene inquilinos activos (hay otra persona cuya ocupación depende de esta cuenta).
- **Pendiente:** la política de retención/anonimización de datos (ADR-005) no está implementada — ver Bloque 4 de esta auditoría.

## Controles de seguridad aplicados
- Confirmación explícita de pérdida de información exigida por el backend (no solo por la UI), igual criterio que HU-094/HU-095.
- Re-verificación de contraseña antes de un borrado destructivo.
- **Un administrador no puede autoeliminarse** (`IAdminAccessChecker`, mismo privilegio de M13/ARQ-004) — control de seguridad ya implementado, confirmado en este bloque mediante lectura directa del controlador (ver además el pendiente equivalente de autosuspensión en Bloque 4).
- Revocación total de sesiones al eliminar.

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/DeleteAccountServiceTests.cs`.
- Integración: `tests/UniHome.IntegrationTests/DeleteAccountEndpointTests.cs`.
- Seguridad: recorrido dinámico de ZAP sobre el catálogo OpenAPI real; esta ruta además está en el set `AUTHENTICATED_OPERATIONS` de `tools/ci/zap_dast.py` como caso explícito de operación autenticada; gramática RESTler generada del mismo documento.
