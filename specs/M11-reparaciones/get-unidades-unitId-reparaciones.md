# `GET /api/v1/unidades/{unitId}/reparaciones`

| Campo | Valor |
|---|---|
| Módulo | M11 – Reparaciones |
| HU | HU-064 |
| Operación OpenAPI | `GET /api/v1/unidades/{unitId}/reparaciones` |
| Controlador | `src/UniHome.Api/Controllers/ReparacionesController.cs` → `List` (`ListReparacionesService.ListAsync`) |

## Autenticación y autorización

`[Authorize]`. Acceso compartido: titular de la unidad o inquilino con vínculo activo (`ListAsync_ComoInquilinoActivo_DebePermitirAcceso`); cualquier otra cuenta recibe `403` (`ListAsync_CuentaSinAccesoALaUnidad_DevuelveForbidden`).

## Request

Parámetro de ruta: `unitId` (`Guid`). Sin cuerpo.

## Response

`200 OK` con `IReadOnlyList<ReparacionResponse>`: reportes de la unidad con su estado, quién lo levantó y su detalle. `Cache-Control: no-store`.

## Validaciones

Ninguna de entrada (solo parámetro de ruta).

## Códigos de error

| Código | Causa |
|---|---|
| 401 | Sesión inválida o ausente. |
| 403 | La cuenta no tiene acceso a la unidad. |
| 404 | La unidad no existe. |
| 503 | Falla de infraestructura. |

## Reglas de negocio

- Mismo criterio de acceso compartido que la creación de reportes (titular o inquilino con vínculo activo).
- Si la cuenta que levantó un reporte ya no se puede resolver, el nombre mostrado usa un texto por defecto en vez de fallar (`ListAsync_CuentaReportanteDesconocida_UsaTextoPorDefecto`).

## Controles de seguridad aplicados

- Autorización por pertenencia a la unidad (mitiga BOLA/IDOR sobre `unitId`).
- `Cache-Control: no-store`.

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/ListReparacionesServiceTests.cs` (5 casos, incluido `ListAsync_CuentaSinAccesoALaUnidad_DevuelveForbidden`).
- Integración: `tests/UniHome.IntegrationTests/ReparacionesEndpointTests.cs`.
- Seguridad: la autorización por pertenencia (BOLA/IDOR) está cubierta por `ListAsync_CuentaSinAccesoALaUnidad_DevuelveForbidden`. Cubierta además dinámicamente por el DAST (`tools/ci/zap_dast.py`) y por RESTler sobre el documento OpenAPI real.
