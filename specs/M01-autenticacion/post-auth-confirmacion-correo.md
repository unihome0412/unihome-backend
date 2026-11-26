# `POST /api/v1/auth/confirmacion-correo`

| Campo | Valor |
|---|---|
| Módulo | M01 – Autenticación y cuenta |
| HU | HU-003 |
| Operación OpenAPI | `POST /api/v1/auth/confirmacion-correo` |
| Controlador | `src/UniHome.Api/Controllers/AuthController.cs` → `ConfirmEmail` (EP-M01-02) |

## Autenticación y autorización
Pública (`[AllowAnonymous]`). Decisión aprobada: recibe correo y código en el cuerpo, sin token de sesión de registro (ese contrato no está definido en la spec).

## Request
Body JSON (`ConfirmarCorreoRequest`): `correo`, `codigo` (6 dígitos). Límite de 8 KB.

## Response
`200 OK` con `ConfirmarCorreoResponse { correoConfirmado: true }`. Respuesta idéntica si la cuenta ya estaba confirmada (idempotencia), sin revelar más datos. Cabecera `Cache-Control: no-store`.

## Validaciones
- `ConfirmEmailService` (`src/UniHome.Application/Identity/ConfirmEmail/`) valida formato de correo y código.
- Correo inexistente, código incorrecto, sin emitir o ya usado → responden igual (`400`), para no revelar si la cuenta existe.
- Código vencido o con intentos agotados (`VerificationCode.MaxConfirmationAttempts = 5`, provisional) → `410`.

## Códigos de error
| Código | Causa |
|---|---|
| 400 | Correo inexistente, código incorrecto, sin emitir o ya usado (mismo mensaje para los cuatro casos). |
| 410 | Código vencido o intentos agotados. |
| 413 | Cuerpo > 8 KB. |
| 429 | Límite de tasa `AuthRateLimitPolicies.ConfirmEmail` (`ConfirmEmailPermitLimit`/`ConfirmEmailWindowSeconds`, 10/60s por defecto). |
| 503 | Dependencia no disponible. |

## Reglas de negocio
- El código de confirmación se guarda como HMAC (`VerificationCode__HmacSecret`), nunca en claro.
- Límite "por cuenta" de 5 intentos incorrectos por verificador; al agotarse se trata como vencido para forzar un reenvío.

## Controles de seguridad aplicados
- `EnableRateLimiting(AuthRateLimitPolicies.ConfirmEmail)` vía el limitador distribuido (Redis/Upstash fuera de Development).
- Mismo código de error para correo inexistente/código incorrecto (anti-enumeración).
- Límite de tamaño de cuerpo (8 KB).

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/ConfirmEmailServiceTests.cs`.
- Integración: `tests/UniHome.IntegrationTests/ConfirmEmailEndpointTests.cs`, `tests/UniHome.IntegrationTests/FullAuthFlowTests.cs`, `tests/UniHome.IntegrationTests/AuthRateLimitTests.cs`.
- Seguridad: DAST ZAP recorre esta ruta dentro del catálogo OpenAPI real (`tools/ci/zap_dast.py`); el código real se obtiene en CI mediante el SMTP de captura local (`tools/ci/dast_smtp_capture.py`). Fuzzing RESTler construido desde el mismo documento OpenAPI.
