# `POST /api/v1/auth/refresh`

| Campo | Valor |
|---|---|
| Módulo | M01 – Autenticación y cuenta |
| HU | No identificada explícitamente con número de HU; implementa EP-M01-08 (renovación de sesión). |
| Operación OpenAPI | `POST /api/v1/auth/refresh` |
| Controlador | `src/UniHome.Api/Controllers/AuthController.cs` → `Refresh` (EP-M01-08) |

## Autenticación y autorización
Pública en el sentido de no requerir `Authorization: Bearer` (usa el refresh token opaco del cuerpo como credencial).

## Request
Body JSON (`RefreshRequest`): `refreshToken`. Límite de 8 KB.

## Response
`200 OK` con `LoginResponse` (nuevo `accessToken`, `refreshToken` rotado, `expiraEn`). Cabecera `Cache-Control: no-store`.

## Validaciones
- `RefreshSessionService` (`src/UniHome.Application/Identity/RefreshSessions/`) valida formato no vacío del token → `400`.
- Token inválido, no encontrado o ya rotado → `401`.

## Códigos de error
| Código | Causa |
|---|---|
| 400 | Formato de campo inválido. |
| 401 | Sesión no válida o expirada. |
| 413 | Cuerpo > 8 KB. |
| 429 | Límite de tasa `AuthRateLimitPolicies.Refresh` (`RefreshPermitLimit`/`RefreshWindowSeconds`, 20/60s). |
| 503 | Dependencia no disponible. |

## Reglas de negocio
- Rotación obligatoria: cada refresh emite un token nuevo con la vigencia configurada (`Jwt__RefreshTokenDays`, 30 días por defecto).
- **Reuso de un token ya rotado revoca la familia completa de sesiones** (mitigación de robo de token de refresh).
- Solo se persiste el SHA-256 del refresh token en MongoDB (`sesiones_refresh`), nunca el valor en claro.

## Controles de seguridad aplicados
- `EnableRateLimiting(AuthRateLimitPolicies.Refresh)`.
- Revocación de familia completa ante reuso detectado.
- `Cache-Control: no-store` (la respuesta contiene tokens).
- Límite de tamaño de cuerpo.

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/RefreshSessionServiceTests.cs`.
- Integración: `tests/UniHome.IntegrationTests/RefreshEndpointTests.cs`, `tests/UniHome.IntegrationTests/FullAuthFlowTests.cs`, `tests/UniHome.IntegrationTests/JwtSecurityTests.cs`.
- Seguridad: recorrido dinámico de ZAP sobre el catálogo OpenAPI real (`tools/ci/zap_dast.py`); gramática RESTler generada del mismo documento.
