# `POST /api/v1/auth/recuperacion/confirmacion`

| Campo | Valor |
|---|---|
| Módulo | M01 – Autenticación y cuenta |
| HU | HU-006 |
| Operación OpenAPI | `POST /api/v1/auth/recuperacion/confirmacion` |
| Controlador | `src/UniHome.Api/Controllers/AuthController.cs` → `ConfirmPasswordReset` (EP-M01-07) |

## Autenticación y autorización
Pública (`[AllowAnonymous]`); la credencial es el token opaco de un solo uso del cuerpo, no un Bearer.

## Request
Body JSON (`RecuperacionConfirmacionRequest`): `token`, `nuevaContrasena`. Límite de 8 KB.

## Response
`200 OK` sin cuerpo adicional (`Ok()`).

## Validaciones
- `ConfirmPasswordResetService` valida la nueva contraseña con la misma política del registro (8 a 128 caracteres) → `400` si falla.
- Token inválido o ya usado → `400`. Token vencido o con intentos agotados → `410`.

## Códigos de error
| Código | Causa |
|---|---|
| 400 | Validación de contraseña fallida, o token inválido/ya usado. |
| 410 | Token vencido o intentos agotados (`PasswordResetToken.MaxAttempts = 5`, provisional). |
| 413 | Cuerpo > 8 KB. |
| 429 | Límite de tasa `AuthRateLimitPolicies.PasswordResetConfirm` (`PasswordResetConfirmPermitLimit`/`WindowSeconds`, 10/60s). |
| 503 | Dependencia no disponible. |

## Reglas de negocio
- Al confirmar se **revocan todas las sesiones de refresh de la cuenta**.
- El cambio de hash usa un CAS sobre el hash anterior: dos confirmaciones concurrentes de la misma cuenta no pueden ganar ambas (una sola solicitud consume el token).
- No usa transacciones multi-documento: un fallo posterior al consumo del token deja el token consumido y las sesiones revocadas; exige solicitar un enlace nuevo.

## Controles de seguridad aplicados
- `EnableRateLimiting(AuthRateLimitPolicies.PasswordResetConfirm)`.
- Token de un solo uso con límite de intentos; CAS de concurrencia sobre el hash de contraseña.
- Revocación total de sesiones de refresh tras el cambio (invalida tokens robados previos al reseteo).

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/ConfirmPasswordResetServiceTests.cs`.
- Integración: `tests/UniHome.IntegrationTests/PasswordRecoveryEndpointTests.cs`, `tests/UniHome.IntegrationTests/MongoPasswordResetPersistenceTests.cs`, `tests/UniHome.IntegrationTests/AuthRateLimitTests.cs`.
- Seguridad: recorrido dinámico de ZAP sobre el catálogo OpenAPI real; gramática RESTler generada del mismo documento.
