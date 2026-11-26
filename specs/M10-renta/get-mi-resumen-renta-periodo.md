# `GET /api/v1/mi-resumen-renta/{periodo}`

| Campo | Valor |
|---|---|
| Módulo | M10 – Renta y pagos informativos |
| HU | HU-060 / HU-061 |
| Operación OpenAPI | `GET /api/v1/mi-resumen-renta/{periodo}` |
| Controlador | `src/UniHome.Api/Controllers/RentController.cs` → `MySummary` (`GetMyRentSummaryService.GetAsync`) |

## Autenticación y autorización

`[Authorize]`. Combina ambos roles de la cuenta (titular e inquilino) en una sola consulta; no hay restricción adicional de rol.

## Request

Parámetro de ruta: `periodo` (string, formato `"yyyy-MM"`). Sin cuerpo.

## Response

`200 OK` con `MiResumenRentaResponse`: como titular, por cada unidad propia con renta configurada en el periodo (renta total, recaudado, pendiente, si la división sigue pendiente); como inquilino, por cada unidad con vínculo activo donde ya tiene cuota asignada (cuota, pagado, saldo, estado). `Cache-Control: no-store`.

## Validaciones

- `periodo` con formato distinto a `"yyyy-MM"`: `400` (`ValidationProblemDetails`, `GetAsync_PeriodoConFormatoInvalido_DebeDevolverValidationFailed`).

## Códigos de error

| Código | Causa |
|---|---|
| 400 | `periodo` no tiene el formato `"yyyy-MM"`. |
| 401 | Sesión inválida o ausente. |
| 503 | Falla de infraestructura. |

## Reglas de negocio

- Combina ambos roles en una sola consulta para no duplicar HU-056/HU-057 (README.md, "Sprint 7 / M10-M12"): como titular resume recaudado y pendiente (`GetAsync_ComoPropietario_ResumeRecaudadoYPendiente`); como inquilino resume la propia cuota (`GetAsync_ComoInquilino_ResumePropiaCuota`).
- Una unidad propia sin renta configurada en el periodo se omite del resumen de titular (`GetAsync_UnidadPropiaSinRentaConfiguradaEnElPeriodo_SeOmite`); un vínculo activo con división pendiente se omite del resumen de inquilino (`GetAsync_VinculoActivoConDivisionPendiente_SeOmiteDeComoInquilino`).
- Una cuenta sin unidades propias ni vínculos activos recibe ambas listas vacías, nunca un error (`GetAsync_SinUnidadesNiVinculos_DevuelveListasVacias`): es "mi resumen", no el recurso de una unidad.

## Controles de seguridad aplicados

- El alcance de ambos roles se calcula siempre a partir del `accountId` de la sesión, nunca de un parámetro del cliente (sin superficie de IDOR en este endpoint).
- `Cache-Control: no-store`.

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/GetMyRentSummaryServiceTests.cs` (6 casos, incluido `GetAsync_PeriodoConFormatoInvalido_DebeDevolverValidationFailed`).
- Integración: `tests/UniHome.IntegrationTests/RentEndpointTests.cs`.
- Seguridad: cubierta dinámicamente por el DAST (`tools/ci/zap_dast.py`) y por RESTler sobre el documento OpenAPI real; sin prueba de seguridad dedicada adicional a las unitarias citadas.
