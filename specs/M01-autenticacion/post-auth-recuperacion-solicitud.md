# `POST /api/v1/auth/recuperacion/solicitud`

| Campo | Valor |
|---|---|
| Módulo | M01 – Autenticación y cuenta |
| HU | HU-006 |
| Operación OpenAPI | `POST /api/v1/auth/recuperacion/solicitud` |
| Controlador | `src/UniHome.Api/Controllers/AuthController.cs` → `RequestPasswordReset` (EP-M01-06) |

## Autenticación y autorización
Pública (`[AllowAnonymous]`).

## Request
Body JSON (`RecuperacionSolicitudRequest`): `correo`. Límite de 8 KB.

## Response
`200 OK` con `RecuperacionSolicitudResponse { mensaje: "Si el correo está registrado, recibirás instrucciones para restablecer tu contraseña" }`, siempre, exista o no la cuenta.

## Validaciones
- `RequestPasswordResetService` valida formato de correo → `400` si falla.
- Solo envía el enlace a cuentas **activas** y con **correo confirmado**; para cualquier otro caso responde el mismo `200` genérico sin enviar nada (anti-enumeración).

## Códigos de error
| Código | Causa |
|---|---|
| 400 | Formato de correo inválido. |
| 413 | Cuerpo > 8 KB. |
| 429 | Límite de tasa `AuthRateLimitPolicies.PasswordResetRequest` (`PasswordResetRequestPermitLimit`/`WindowSeconds`, 5/60s). |
| 503 | Dependencia no disponible. |

## Reglas de negocio
- El enlace contiene un token opaco de 256 bits, vence a los 30 minutos, es de un solo uso; en MongoDB solo se guarda su SHA-256 (`restablecimientos_contrasena`).
- Respuesta idéntica exista o no la cuenta, para no revelar cuentas registradas.

## Controles de seguridad aplicados
- `EnableRateLimiting(AuthRateLimitPolicies.PasswordResetRequest)`.
- Respuesta uniforme sin importar el resultado real.
- Token de un solo uso, opaco, solo se persiste su hash.

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/RequestPasswordResetServiceTests.cs`.
- Integración: `tests/UniHome.IntegrationTests/PasswordRecoveryEndpointTests.cs`, `tests/UniHome.IntegrationTests/MongoPasswordResetPersistenceTests.cs`, `tests/UniHome.IntegrationTests/AuthRateLimitTests.cs`.
- Seguridad: recorrido dinámico de ZAP sobre el catálogo OpenAPI real; gramática RESTler generada del mismo documento.
