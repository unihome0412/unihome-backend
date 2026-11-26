# `POST /api/v1/auth/registro`

| Campo | Valor |
|---|---|
| Módulo | M01 – Autenticación y cuenta |
| HU | HU-001 / HU-012 |
| Operación OpenAPI | `POST /api/v1/auth/registro` |
| Controlador | `src/UniHome.Api/Controllers/AuthController.cs` → `Register` (EP-M01-01) |

## Autenticación y autorización
Pública (`[AllowAnonymous]`). No requiere `Authorization: Bearer`. No existe selección de rol por parte del cliente: toda cuenta nace con el mismo tipo.

## Request
Body JSON (`RegistroRequest`, `src/UniHome.Api/Contracts/Auth/RegistroRequest.cs`): `nombreCompleto`, `correo`, `fechaNacimiento`, `genero` (`Gender`), `contrasena`, `confirmarContrasena`, `aceptoAvisoPrivacidad` (bool), `versionAvisoPrivacidad`. Todos los campos son nullable en el DTO para que la validación de negocio devuelva errores por campo en español (`RegisterAccountService`) en vez de un 400 genérico de model binding. Límite de tamaño de cuerpo: 8 KB (`MaxAuthRequestBytes`).

## Response
`201 Created` con `RegistroResponse`: `id`, `correo`, `correoConfirmado` (siempre `false`), `requiereConfirmacionCorreo` (siempre `true`), `correoConfirmacionEnviado`. Nunca incluye rol, hash de contraseña, código de confirmación ni datos de sesión. Cabecera `Cache-Control: no-store`.

## Validaciones
- Validadas por `RegisterAccountService` (`src/UniHome.Application/Identity/RegisterAccount/`): nombre completo no vacío, correo con formato válido, fecha de nacimiento, contraseña entre 8 y 128 caracteres, `contrasena == confirmarContrasena`, `aceptoAvisoPrivacidad == true` con `versionAvisoPrivacidad` presente.
- Cuerpo > 8 KB → `413`.

## Códigos de error
| Código | Causa |
|---|---|
| 400 | Validación de campo fallida (`ValidationProblemDetails`). |
| 409 | Correo ya registrado (DEC-09 pendiente: el detalle no confirma qué dato provocó el conflicto, para no revelar existencia de la cuenta). |
| 413 | Cuerpo de la solicitud mayor a 8 KB. |
| 503 | Dependencia (MongoDB) no disponible; falla cerrada. |

## Reglas de negocio
- Registro único, sin elección de rol (README § "Endpoints disponibles de M01").
- El código de confirmación de correo se guarda como HMAC, nunca en claro.
- `correoConfirmacionEnviado` es `false` si no hay proveedor SMTP configurado (Development); `true` solo indica que el relé aceptó el mensaje, no que llegó al buzón.

## Controles de seguridad aplicados
- Límite de tamaño de cuerpo (8 KB, `RequestSizeLimit`).
- Contraseña nunca se persiste en claro (hash, fuera del alcance de este endpoint — ver `Account`).
- Mensaje de conflicto genérico para no confirmar la existencia de una cuenta por correo (mitigación de enumeración, con DEC-09 pendiente de decisión de producto sobre el detalle exacto).
- Sin rate limiting dedicado en este endpoint específico (a diferencia de `login`/`confirmacion-correo`); ver pendiente de rate limiting en registro en `README.md` y Bloque 4 de esta auditoría.

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/RegisterAccountServiceTests.cs`.
- Integración: `tests/UniHome.IntegrationTests/RegisterEndpointTests.cs`, `tests/UniHome.IntegrationTests/RegisterWithoutStorageTests.cs`, `tests/UniHome.IntegrationTests/FullAuthFlowTests.cs`, `tests/UniHome.IntegrationTests/MongoIdentityPersistenceTests.cs`.
- Seguridad: cubierto dinámicamente por el DAST ZAP (`tools/ci/zap_dast.py`), que recorre todas las rutas `/api/v1/*` del documento OpenAPI real; el fuzzing RESTler (`tools/ci/restler/normalize_openapi.py`, `restler_prepare.py`) construye su gramática a partir del mismo documento. Sin contratos Pact reales todavía (`pacts/` solo contiene `README.md`).
