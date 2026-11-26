# `GET /api/v1/mis-propiedades`

| Campo | Valor |
|---|---|
| Módulo | M05 – Propiedades y unidades |
| HU | HU-033 |
| Operación OpenAPI | `GET /api/v1/mis-propiedades` |
| Controlador | `src/UniHome.Api/Controllers/MyPropertiesController.cs` → `List` (EP-M05-11) |

## Autenticación y autorización
Requiere Bearer. Sin sesión responde `401`. Solo devuelve propiedades del titular autenticado.

## Request
Query (`MisPropiedadesQuery`): `tipoPropiedad` (enum `PropertyType` por nombre, opcional), `estadoHabitacion` (enum `UnitAvailability` por nombre, opcional). Ambos usan `EnumQueryModelBinder<T>`, que rechaza valores fuera del enum (incluidos enteros).

## Response
`200 OK` con `MiPropiedadResponse[]`: `id`, `nombre`, `tipoPropiedad`, `totalHabitaciones`, `habitacionesDisponibles`, `habitaciones[]` (`id`, `tipoUnidad`, `estado`).

## Validaciones
- Un valor de `tipoPropiedad`/`estadoHabitacion` fuera del enum (incluido un entero) → `400` (`EnumQueryModelBinder`).

## Códigos de error
| Código | Causa |
|---|---|
| 400 | Valor de filtro fuera del enum. |
| 401 | Sin sesión válida. |
| 503 | Falla de dependencia. |

## Reglas de negocio
- `GetMyPropertiesService.ListAsync`: `tipoPropiedad` filtra por el tipo de la propiedad; `estadoHabitacion` incluye una propiedad si **al menos una** de sus habitaciones está en ese estado. Ambos filtros son opcionales y se combinan con `AND`.
- Una propiedad sin habitaciones aparece con `totalHabitaciones: 0`, `habitacionesDisponibles: 0`, `habitaciones: []`.
- No se agregó migración ni índice nuevo: reutiliza `ix_propiedades_propietario` de `M05-001`.

## Controles de seguridad aplicados
- Aislamiento estricto por `accountId` del token (`ListByOwnerAsync`); nunca por un id de cuenta recibido del cliente.

## Pruebas que la cubren
- Unitarias: `tests/UniHome.UnitTests/Application/GetMyPropertiesServiceTests.cs::ListAsync_AgrupaPorPropiedadYCuentaDisponibles`, `::ListAsync_SinHabitaciones_DevuelveCerosYListaVacia`, `::ListAsync_FiltraPorTipoPropiedad`, `::ListAsync_FiltraPorEstadoHabitacion`, `::ListAsync_SoloDevuelvePropiedadesDelTitular`.
- Integración: `tests/UniHome.IntegrationTests/MyPropertiesEndpointTests.cs::MisPropiedades_AgrupaHabitacionesCuentaDisponiblesYFiltra`, `::MisPropiedades_SinSesion_DebeResponder401`, `::MisPropiedades_EstaAisladoPorCuenta`, `::MisPropiedades_FiltroInvalido_DebeResponder400`.
- Seguridad: cobertura general de ZAP/RESTler sobre el documento OpenAPI real.
