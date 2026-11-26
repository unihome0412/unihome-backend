# `PUT /api/v1/admin/cuentas/{accountId}/estado`

| Campo | Valor |
|---|---|
| Módulo | M13 – Administración de cuentas |
| HU | HU-072 |
| Operación OpenAPI | `PUT /api/v1/admin/cuentas/{accountId}/estado` |
| Controlador | `src/UniHome.Api/Controllers/AdminCuentasController.cs` → `SetStatus` (EP-M13-01) |

## Autenticación y autorización

Policy **Admin** a nivel de clase (ver [`get-admin-cuentas.md`](get-admin-cuentas.md)).
**Hallazgo de seguridad real (evidencia de código, no una suposición):**
`SetAccountStatusService.SetStatusAsync(targetAccountId, suspender, ...)`
no recibe el id del administrador que llama ni lo compara contra
`targetAccountId`. **Hoy no existe en el código ninguna restricción que
impida que un administrador se autosuspenda** a través de este endpoint
(el README confirma esto como pendiente: "Pendiente" explícito está
documentado para la autoeliminación de HU-096, pero la autosuspensión de
M13 no tiene ni siquiera esa mención — este spec la deja constatada con
evidencia de código para que el Bloque 4 la cierre).

## Request

Parámetro de ruta `accountId` (`Guid`). Body JSON
`CambiarEstadoCuentaRequest` con `Suspendida` (bool).
`RequestSizeLimit(8 * 1024)`.

## Response

`200 OK` con `AdminCuentaResponse.FromDomain(value)`.

## Validaciones

- `accountId` debe existir; si no, `404`.
- `account.Suspend()` / `account.Reactivate()` (reglas de dominio): si la
  cuenta ya está en el estado destino, o está `Deleted`, lanzan
  `InvalidOperationException` → `400` sobre el campo `estado`.

## Códigos de error

| Código | Causa |
|---|---|
| 400 | La cuenta ya está suspendida/activa, o está eliminada (no admite cambio de estado). |
| 401 | Sin sesión válida. |
| 403 | La cuenta no tiene privilegio administrativo. |
| 404 | La cuenta objetivo no existe. |
| 413 | Solicitud excede 8 KB. |
| 503 | Preflight de esquema Mongo fallido. |

## Reglas de negocio

- **Suspender revoca todas las sesiones activas** de la cuenta objetivo
  (`IRefreshSessionRepository.RevokeAllForAccountAsync`, razón
  `"account_suspended_by_admin"`), igual que la eliminación de cuenta
  (HU-096): una cuenta suspendida no debe seguir operando con un token ya
  emitido (consistente con BE-SEC-008, revisión de estado en cada
  petición autenticada).
- Reactivar **no** revoca sesiones (no las había activas tras la
  suspensión) ni las restaura.
- Reutiliza `cuentas` (`M01-001`); sin migración nueva.

## Controles de seguridad aplicados

- Autorización por función en `/admin/*`.
- Revocación inmediata de sesiones al suspender, cerrando la ventana de
  uso de un token ya emitido.
- **Gap de seguridad real detectado (a cerrar en Bloque 4):** no existe
  restricción de autosuspensión administrativa; un administrador puede
  suspender su propia cuenta a través de este endpoint sin ninguna
  verificación adicional en el código actual.

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/SetAccountStatusServiceTests.cs`
  (`SetStatusAsync_Suspender_CambiaEstadoYRevocaTodasLasSesiones`,
  `SetStatusAsync_Reactivar_CambiaEstadoSinRevocarSesiones`,
  `SetStatusAsync_CuentaInexistente_DevuelveNotFound`,
  `SetStatusAsync_SuspenderUnaCuentaYaSuspendida_DevuelveValidationFailed`,
  `SetStatusAsync_ReactivarUnaCuentaEliminada_DevuelveValidationFailed`).
- Integración: `tests/UniHome.IntegrationTests/AdminCuentasEndpointTests.cs`
  (`Suspender_ComoAdmin_DebeResponder200YRevocarSesionesDelUsuario`,
  `Reactivar_SobreCuentaSuspendida_DebeResponder200YVolverActiva`,
  `CambiarEstado_CuentaInexistente_DebeResponder404`,
  `CambiarEstado_SinPrivilegioAdministrativo_DebeResponder403`).
- Seguridad: cubierto por el recorrido dinámico de ZAP y la gramática
  RESTler sobre el documento OpenAPI. **No se encontró ninguna prueba que
  verifique el bloqueo de autosuspensión administrativa**, porque esa
  restricción no existe todavía en el código (ver "Controles de
  seguridad aplicados"); queda como hallazgo abierto para el Bloque 4.
