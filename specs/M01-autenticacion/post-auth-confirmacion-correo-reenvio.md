# `POST /api/v1/auth/confirmacion-correo/reenvio`

| Campo | Valor |
|---|---|
| Módulo | M01 – Autenticación y cuenta |
| HU | HU-003 |
| Operación OpenAPI | `POST /api/v1/auth/confirmacion-correo/reenvio` |
| Controlador | `src/UniHome.Api/Controllers/AuthController.cs` → `ResendConfirmation` (EP-M01-03) |

## Autenticación y autorización
Pública (`[AllowAnonymous]`).

## Request
Body JSON (`ReenviarCodigoRequest`): `correo`. Límite de 8 KB.

## Response
`202 Accepted`, sin cuerpo, siempre — exista o no el correo — para no revelar cuentas registradas. Cabecera `Cache-Control: no-store`.

## Validaciones
- `ResendConfirmationService` valida formato de correo; un correo con formato inválido devuelve `400` (`ValidationProblemDetails`).

## Códigos de error
| Código | Causa |
|---|---|
| 400 | Formato de correo inválido. |
| 413 | Cuerpo > 8 KB. |
| 429 | Límite de tasa `AuthRateLimitPolicies.Resend` (`ResendPermitLimit`/`ResendWindowSeconds`, 5/60s por defecto). |
| 503 | Dependencia no disponible. |

## Reglas de negocio
- Siempre `202` sin cuerpo, exista o no el correo (decisión aprobada, anti-enumeración).
- Genera un nuevo código HMAC y reinicia el contador de intentos del verificador.

## Controles de seguridad aplicados
- `EnableRateLimiting(AuthRateLimitPolicies.Resend)`.
- Respuesta uniforme (no distingue correo existente/inexistente).
- Límite de tamaño de cuerpo.

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/ResendConfirmationServiceTests.cs`.
- Integración: `tests/UniHome.IntegrationTests/ResendConfirmationEndpointTests.cs`, `tests/UniHome.IntegrationTests/AuthRateLimitTests.cs`.
- Seguridad: cubierto por el recorrido dinámico de ZAP sobre el catálogo OpenAPI real (`tools/ci/zap_dast.py`) y por la gramática RESTler generada del mismo documento.
