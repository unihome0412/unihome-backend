# `GET /api/v1/unidades/{unitId}/renta/mis-pagos`

| Campo | Valor |
|---|---|
| Módulo | M10 – Renta y pagos informativos |
| HU | HU-058 |
| Operación OpenAPI | `GET /api/v1/unidades/{unitId}/renta/mis-pagos` |
| Controlador | `src/UniHome.Api/Controllers/RentController.cs` → `MyPaymentHistory` (`GetMyPaymentHistoryService.GetAsync`) |

## Autenticación y autorización

`[Authorize]`. Devuelve únicamente los pagos del llamador; no exige vínculo activo (a diferencia de `mi-renta`, HU-057): el inquilino sigue viendo lo que se le registró aunque ya haya dejado la unidad (`GetAsync_InquilinoYaDejoLaUnidad_SigueViendoSuHistorial`).

## Request

Parámetro de ruta: `unitId` (`Guid`). Sin cuerpo.

## Response

`200 OK` con `MisPagosResponse`: historial propio de pagos informativos en la unidad, a través de todos los periodos, más reciente primero (`GetAsync_ConPagosEnVariosPeriodos_DevuelveSoloLosPropiosOrdenadosDescendente`). `Cache-Control: no-store`.

## Validaciones

Ninguna de entrada (solo parámetro de ruta).

## Códigos de error

| Código | Causa |
|---|---|
| 401 | Sesión inválida o ausente. |
| 404 | La unidad no existe (`GetAsync_UnidadInexistente_DebeDevolverNotFound`). |
| 503 | Falla de infraestructura. |

## Reglas de negocio

- Recorre todos los periodos de la unidad y filtra solo los pagos del llamador; una cuenta sin pagos recibe una lista vacía, nunca un error (`GetAsync_InquilinoSinPagos_DevuelveListaVacia`).
- A diferencia de `GET .../mi-renta` (HU-057), **no exige vínculo activo**: el historial es propiedad del inquilino, independiente de si sigue ocupando la unidad.

## Controles de seguridad aplicados

- El filtrado es siempre por el `accountId` de la sesión, nunca por un parámetro del cliente (mitiga BOLA/IDOR: imposible pedir el historial de otro inquilino).
- `Cache-Control: no-store`.

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/GetMyPaymentHistoryServiceTests.cs` (4 casos).
- Integración: `tests/UniHome.IntegrationTests/RentEndpointTests.cs`.
- Seguridad: cubierta dinámicamente por el DAST (`tools/ci/zap_dast.py`) y por RESTler sobre el documento OpenAPI real; sin prueba de seguridad dedicada adicional a las unitarias citadas (el diseño mismo, filtrar siempre por el `accountId` de sesión, elimina la superficie de IDOR en este endpoint).
