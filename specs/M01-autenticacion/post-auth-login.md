# `POST /api/v1/auth/login`

| Campo | Valor |
|---|---|
| Módulo | M01 – Autenticación y cuenta |
| HU | HU-004 |
| Operación OpenAPI | `POST /api/v1/auth/login` |
| Controlador | `src/UniHome.Api/Controllers/AuthController.cs` → `Login` (EP-M01-04) |

## Autenticación y autorización
Pública (`[AllowAnonymous]`). Login universal por correo y contraseña; no se solicita ni se acepta un rol del cliente.

## Request
Body JSON (`LoginRequest`): `correo`, `contrasena`. Límite de 8 KB.

## Response
`200 OK` con `LoginResponse`: `accessToken` (JWT HS256), `refreshToken` (opaco 256 bits), `expiraEn`. Cabecera `Cache-Control: no-store`.

## Validaciones
- `LoginService` (`src/UniHome.Application/Identity/Login/`) valida formato de correo/contraseña no vacíos → `400` si falla.
- Credenciales incorrectas y correo inexistente devuelven el **mismo** `401` y el mismo mensaje (`Revisa tu correo y contraseña`), para no revelar existencia de cuenta.
- Correo no confirmado → `403`. Cuenta suspendida → `403`. Cuenta eliminada no puede iniciar sesión (tratada como credenciales inválidas → `401`).

## Códigos de error
| Código | Causa |
|---|---|
| 400 | Validación de formato de campos. |
| 401 | Credenciales incorrectas o correo inexistente (mensaje genérico). |
| 403 | Correo no confirmado, o cuenta suspendida. |
| 413 | Cuerpo > 8 KB. |
| 429 | Límite de tasa `AuthRateLimitPolicies.Login` (`LoginPermitLimit`/`LoginWindowSeconds`, 10 intentos/60s por IP). |
| 503 | Dependencia no disponible. |

## Reglas de negocio
- Access token JWT HS256 con `sub`, `sid` de sesión, `issuer`, `audience`, expiración y firma verificados.
- Refresh token opaco de 256 bits; solo se persiste su SHA-256 en MongoDB; se rota en cada refresh.
- El estado de la cuenta se revisa en cada petición autenticada (BE-SEC-008), no solo en login.

## Controles de seguridad aplicados
- `EnableRateLimiting(AuthRateLimitPolicies.Login)` con IP seudonimizada (`Auth:RateLimit:IpHashSalt`) fuera de Development; limitador distribuido Redis/Upstash obligatorio fuera de Development.
- Mensaje de error uniforme para credenciales incorrectas/correo inexistente (anti-enumeración).
- Límite de tamaño de cuerpo (8 KB).
- `Cache-Control: no-store` en la respuesta (contiene tokens).

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/LoginServiceTests.cs`.
- Integración: `tests/UniHome.IntegrationTests/LoginEndpointTests.cs`, `tests/UniHome.IntegrationTests/FullAuthFlowTests.cs`, `tests/UniHome.IntegrationTests/JwtSecurityTests.cs`, `tests/UniHome.IntegrationTests/AuthRateLimitTests.cs`, `tests/UniHome.IntegrationTests/RateLimiting/DistributedRedisIntegrationTests.cs`.
- Seguridad: recorrido dinámico de ZAP (`tools/ci/zap_dast.py`) sobre el catálogo OpenAPI real; gramática RESTler generada del mismo documento (`tools/ci/restler/normalize_openapi.py`).
