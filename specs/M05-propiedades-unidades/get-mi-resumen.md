# `GET /api/v1/mi-resumen`

| Campo | Valor |
|---|---|
| Módulo | M05 – Propiedades y unidades (cruza con M10 renta y M11 reparaciones) |
| HU | HU-034 |
| Operación OpenAPI | `GET /api/v1/mi-resumen` |
| Controlador | `src/UniHome.Api/Controllers/SummaryController.cs` → `Get` (EP-M05/M10/M11) |

## Autenticación y autorización
Requiere Bearer. Sin sesión responde `401`. Solo agrega datos del titular autenticado (sin parámetro de cuenta).

## Request
Sin parámetros.

## Response
`200 OK` con `ResumenResponse`: `periodo` (yyyy-MM del servidor), `rentasRegistradasDelMesMxn`, `numeroPropiedades`, `reparacionesAbiertas`. `Cache-Control: no-store`.

## Validaciones
Ninguna de entrada (no recibe parámetros del cliente).

## Códigos de error
| Código | Causa |
|---|---|
| 401 | Sin sesión válida. |
| 503 | Falla de dependencia. |

## Reglas de negocio
- `GetMySummaryService.GetAsync` (`src/UniHome.Application/Summary/GetMySummaryService.cs`): el periodo se calcula con la fecha del servidor, nunca recibido del cliente.
- Rentas del mes: suma de pagos informativos (`IInformativePaymentRepository`) del periodo resuelto con herencia (`RentPeriodResolver`, mismo criterio que HU-060/061).
- Reparaciones abiertas: cuenta reportes con estado distinto de `Atendido` (`Pendiente` o `EnProceso`) en unidades propias.
- Una cuenta sin propiedades recibe el resumen en ceros, nunca un error.

## Controles de seguridad aplicados
- `Cache-Control: no-store` (datos propios del arrendador).
- Agregación estrictamente por `accountId` del token (`ListByOwnerAsync`), sin aceptar un id de cuenta del cliente.

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/GetMySummaryServiceTests.cs::GetAsync_SinPropiedades_DevuelveCeros`, `::GetAsync_ConPropiedadRentaYReparaciones_AgregaLosTresIndicadores`, `::GetAsync_SinRentaConfigurada_NoSumaRenta`.
- Integración: `tests/UniHome.IntegrationTests/SummaryEndpointTests.cs::MiResumen_SinPropiedades_DebeResponderEnCeros`, `::MiResumen_ConPropiedadRentaYReparaciones_DebeAgregarLosTresIndicadores`, `::MiResumen_SinSesion_DebeResponder401`.
- Seguridad: cobertura general de ZAP/RESTler sobre el documento OpenAPI real.
