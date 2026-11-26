# `PUT /api/v1/unidades/{unitId}/reparaciones/{reparacionId}/estado`

| Campo | Valor |
|---|---|
| Módulo | M11 – Reparaciones |
| HU | HU-065 / HU-066 |
| Operación OpenAPI | `PUT /api/v1/unidades/{unitId}/reparaciones/{reparacionId}/estado` |
| Controlador | `src/UniHome.Api/Controllers/ReparacionesController.cs` → `ChangeStatus` (`ChangeReparacionStatusService.ChangeStatusAsync`) |

## Autenticación y autorización

`[Authorize]`. **Cambio de estado exclusivo del titular**: un inquilino con vínculo activo puede crear/leer reportes, pero no cambiar su estado — recibe `403` (`ChangeStatusAsync_ComoInquilino_DevuelveForbidden`).

## Request

- Parámetros de ruta: `unitId`, `reparacionId` (`Guid`).
- Body JSON `CambiarEstadoReparacionRequest`: `estado` (`Pendiente`/`EnProceso`/`Atendido`).
- `[RequestSizeLimit(8 * 1024)]`.

## Response

`200 OK` con `ReparacionResponse`. `Cache-Control: no-store`.

## Validaciones

- `reparacionId` debe pertenecer a `unitId`; un reporte de otra unidad responde `404`, no `403` (`ChangeStatusAsync_ReporteDeOtraUnidad_DevuelveNotFound`) — evita confirmar por canal lateral que el reporte existe en otra unidad.
- Tamaño de solicitud ≤ 8 KB (`413`).

## Códigos de error

| Código | Causa |
|---|---|
| 400 | Valor de `estado` fuera del enum. |
| 401 | Sesión inválida o ausente. |
| 403 | La cuenta es inquilino (no titular) o no tiene acceso a la unidad. |
| 404 | La unidad no existe, el reporte no existe, o el reporte pertenece a otra unidad. |
| 413 | El cuerpo de la solicitud excede 8 KB. |
| 503 | Falla de infraestructura. |

## Reglas de negocio

- **El estado se puede revertir:** `Reparacion.ChangeStatus` no impone un flujo lineal; el titular puede volver de `Atendido` a `EnProceso` (README.md, "Sprint 8 ... M11"). Fijar el mismo estado que ya tenía es un no-op: no genera notificación ni actualiza `updatedAt` (`ChangeStatusAsync_ConElMismoEstado_NoGeneraNotificacion`).
- **HU-066:** al cambiar el estado se notifica de inmediato a todos los que tienen acceso a la unidad (titular e inquilinos con vínculo activo), **excepto a quien hizo el cambio** (`ChangeStatusAsync_ComoPropietario_ActualizaYNotificaAInquilinosActivos`, `ChangeStatusAsync_ElPropioReportanteCambiaElEstado_NoSeAutonotifica`): a diferencia del recordatorio de renta (HU-059, generado al consultar), esta notificación se genera de inmediato al cambiar el estado.

## Controles de seguridad aplicados

- Autorización por función: solo el titular cambia el estado, verificado explícitamente más allá de la pertenencia general a la unidad (mitiga escalamiento de privilegios de inquilino a titular).
- Un reporte de otra unidad responde `404` en vez de `403`, evitando confirmar su existencia a una cuenta sin acceso a esa unidad.

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/ChangeReparacionStatusServiceTests.cs` (7 casos: notificación a inquilinos activos, no autonotificación, no-op con mismo estado, `ComoInquilino_DevuelveForbidden`, unidad inexistente, reporte inexistente, reporte de otra unidad).
- Integración: `tests/UniHome.IntegrationTests/ReparacionesEndpointTests.cs`.
- Seguridad: la autorización por función (titular vs. inquilino) está cubierta por `ChangeStatusAsync_ComoInquilino_DevuelveForbidden`; el aislamiento entre unidades por `ChangeStatusAsync_ReporteDeOtraUnidad_DevuelveNotFound`. Cubierta además dinámicamente por el DAST (`tools/ci/zap_dast.py`) y por RESTler sobre el documento OpenAPI real.
