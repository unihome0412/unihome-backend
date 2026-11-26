# `POST /api/v1/auth/logout`

| Campo | Valor |
|---|---|
| Módulo | M01 – Autenticación y cuenta |
| HU | HU-005 |
| Operación OpenAPI | `POST /api/v1/auth/logout` |
| Controlador | `src/UniHome.Api/Controllers/AuthController.cs` → `Logout` (EP-M01-05) |

## Autenticación y autorización
Requiere `Authorization: Bearer` (`[Authorize]`). Usa los claims `sub` (cuenta) y `sid` (sesión) del propio JWT; no acepta un identificador de sesión enviado por el cliente.

## Request
Sin body. Requiere cabecera `Authorization: Bearer <jwt>`.

## Response
`204 No Content`, sin cuerpo. Cabecera `Cache-Control: no-store`.

## Validaciones
- Claims `sub`/`sid` deben parsear como `Guid` en formato `"N"`; si no, `401`.

## Códigos de error
| Código | Causa |
|---|---|
| 401 | Token inválido/ausente, o la sesión (`sid`) ya no existe/fue revocada. |
| 413 | Cuerpo > 8 KB (no aplica cuerpo, defensivo). |
| 503 | Dependencia no disponible. |

## Reglas de negocio
- **Revoca únicamente la sesión del dispositivo actual** (a diferencia de la eliminación de cuenta, que revoca todas las sesiones).
- Es la base de la revocación inmediata de JWT vía denylist de `sid` en Redis, prevista para el Bloque 2 de esta auditoría (actualmente el estado de sesión se valida contra `sesiones_refresh` en MongoDB, BE-SEC-008).

## Controles de seguridad aplicados
- Requiere Bearer válido; usa claims del propio token, nunca un id de sesión del cliente (evita que un usuario cierre la sesión de otro).
- `Cache-Control: no-store`.

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/LogoutServiceTests.cs`.
- Integración: `tests/UniHome.IntegrationTests/LogoutEndpointTests.cs`, `tests/UniHome.IntegrationTests/FullAuthFlowTests.cs`.
- Seguridad: recorrido dinámico de ZAP; esta ruta además está en el set `AUTHENTICATED_OPERATIONS` de `tools/ci/zap_dast.py` como caso explícito de operación autenticada relevante para el DAST.
