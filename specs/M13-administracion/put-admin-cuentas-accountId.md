# `PUT /api/v1/admin/cuentas/{accountId}`

| Campo | Valor |
|---|---|
| Módulo | M13 – Administración de cuentas |
| HU | HU-074 |
| Operación OpenAPI | `PUT /api/v1/admin/cuentas/{accountId}` |
| Controlador | `src/UniHome.Api/Controllers/AdminCuentasController.cs` → `Edit` (EP-M13-03) |

## Autenticación y autorización

Policy **Admin** a nivel de clase (ver [`get-admin-cuentas.md`](get-admin-cuentas.md)).
**Hallazgo de seguridad real (evidencia de código, no una suposición):**
`EditAccountService.EditAsync` no recibe ni valida el id del administrador
que realiza la llamada contra el `accountId` objetivo; **no existe hoy en
el código ninguna restricción que impida que un administrador edite su
propia cuenta** (no hay un caso análogo a "autoeliminación" aquí, pero
tampoco hay registro de quién hizo el cambio en `Account`). Esto es
relevante para el Bloque 4 (seguridad): a diferencia de
`PUT .../estado` (ver spec siguiente), aquí ni siquiera se discute la
restricción en el README actual.

## Request

Parámetro de ruta `accountId` (`Guid`). Body JSON `EditarCuentaRequest`
con `NombreCompleto` y `Correo`. `RequestSizeLimit(8 * 1024)`.

## Response

`200 OK` con `AdminCuentaResponse.FromDomain(value)`.

## Validaciones

- `accountId` debe existir; si no, `404`.
- `Correo` se normaliza y valida con `EmailAddress.Create`; si es
  inválido, `400` sobre el campo `correo`.
- `NombreCompleto`/`Correo` se validan también en `Account.EditProfile`
  (reglas de dominio); error de argumento → `400` sobre el campo
  correspondiente (`correo` o `nombreCompleto`).
- El nuevo correo no puede coincidir con el de **otra** cuenta
  (`DuplicateEmailException` → `400` sobre `correo`); el propio correo
  sin cambios se acepta explícitamente
  (`Editar_ConElMismoCorreoPropio_DebePermitirlo`... — nombre de prueba
  real: `EditAsync_ConElMismoCorreoPropio_DebePermitirlo`).

## Códigos de error

| Código | Causa |
|---|---|
| 400 | Correo inválido, nombre inválido, o correo ya usado por otra cuenta. |
| 401 | Sin sesión válida. |
| 403 | La cuenta no tiene privilegio administrativo. |
| 404 | La cuenta objetivo no existe. |
| 413 | Solicitud excede 8 KB. |
| 503 | Preflight de esquema Mongo fallido. |

## Reglas de negocio

- Reutiliza `cuentas` (`M01-001`); sin migración nueva.
- No dispara revocación de sesiones (a diferencia de suspender): editar
  nombre/correo no invalida el token de acceso ya emitido en esta
  implementación actual.

## Controles de seguridad aplicados

- Autorización por función en `/admin/*`.
- Unicidad de correo verificada en escritura (`DuplicateEmailException`),
  no solo en el documento inicial.
- **Gap de seguridad real detectado (a cerrar en Bloque 4):** no hay
  restricción de que un administrador no pueda editarse a sí mismo ni
  registro de auditoría (actor) del cambio; tampoco se registra qué
  administrador hizo la edición.

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/EditAccountServiceTests.cs`
  (`EditAsync_ConDatosValidos_ActualizaNombreYCorreo`,
  `EditAsync_CuentaInexistente_DevuelveNotFound`,
  `EditAsync_ConCorreoInvalido_DevuelveValidationFailed`,
  `EditAsync_ConNombreInvalido_DevuelveValidationFailed`,
  `EditAsync_ConCorreoYaUsadoPorOtraCuenta_DevuelveValidationFailed`,
  `EditAsync_ConElMismoCorreoPropio_DebePermitirlo`).
- Integración: `tests/UniHome.IntegrationTests/AdminCuentasEndpointTests.cs`
  (`Editar_ComoAdmin_DebeActualizarNombreYCorreo`,
  `Editar_ConCorreoYaUsado_DebeResponder400`,
  `Editar_CuentaInexistente_DebeResponder404`,
  `Editar_SinPrivilegioAdministrativo_DebeResponder403`).
- Seguridad: cubierto por el recorrido dinámico de ZAP y la gramática
  RESTler sobre el documento OpenAPI. **No se encontró ninguna prueba que
  verifique que un administrador no puede editarse a sí mismo**, porque
  esa restricción no existe todavía en el código (ver "Controles de
  seguridad aplicados").
