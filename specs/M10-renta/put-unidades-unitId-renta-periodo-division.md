# `PUT /api/v1/unidades/{unitId}/renta/{periodo}/division`

| Campo | Valor |
|---|---|
| Módulo | M10 – Renta y pagos informativos |
| HU | HU-053 / HU-054 |
| Operación OpenAPI | `PUT /api/v1/unidades/{unitId}/renta/{periodo}/division` |
| Controlador | `src/UniHome.Api/Controllers/RentController.cs` → `Split` (`SplitRentPeriodService.SplitAsync`) |

## Autenticación y autorización

`[Authorize]`. Solo el **titular de la unidad** divide la renta; otra cuenta recibe `403`.

## Request

- Parámetros de ruta: `unitId`, `periodo` (`"yyyy-MM"`).
- Body JSON `DividirRentaRequest`: `metodo` (igual/personalizada), `cuotas[]` (`inquilinoAccountId`, `montoMxn`).
- `[RequestSizeLimit(8 * 1024)]`.

## Response

`200 OK` con `RentaPeriodoResponse`. `Cache-Control: no-store`.

## Validaciones

- División personalizada: la suma de las cuotas debe ser igual a la renta total, si no, `400` (`SplitAsync_Personalizada_SumaDistinta_DebeRechazarse`).
- Cada cuota debe corresponder a un inquilino con vínculo activo en la unidad; si no, `400` (`SplitAsync_Personalizada_InquilinoSinVinculoActivo_DebeRechazarse`).
- Si el periodo no existe y no hay uno anterior que heredar: `404` (`SplitAsync_SinPeriodoNiAnterior_DebeDevolverNotFound`).
- Tamaño de solicitud ≤ 8 KB (`413`).

## Códigos de error

| Código | Causa |
|---|---|
| 400 | Suma de cuotas distinta a la renta total, o inquilino sin vínculo activo. |
| 401 | Sesión inválida o ausente. |
| 403 | La cuenta no es titular de la unidad. |
| 404 | El periodo no existe y no hay un periodo anterior que heredar. |
| 413 | El cuerpo de la solicitud excede 8 KB. |
| 503 | Falla de infraestructura. |

## Reglas de negocio

- **Herencia entre periodos:** si el periodo no existe, se crea copiando el periodo anterior más reciente de la misma unidad (renta total, conceptos, día de corte); la división y cuotas solo se heredan si el conjunto de inquilinos activos no cambió (`SplitAsync_HeredaPeriodoAnterior_MismosInquilinos_DivideSobreElHeredado`, README.md).
- **Redondeo en partes iguales:** se trabaja en centavos (renta × 100, entero) y el residuo se reparte de un centavo en centavo a los inquilinos ordenados por `accountId` (formato `"N"`, orden ordinal), para que la suma cierre exacto contra la renta total (`SplitAsync_Igual_DivideEntreInquilinosActivos`). Ejemplo documentado: $1000.00 entre 3 inquilinos → $333.34 + $333.33 + $333.33.
- División personalizada: la suma de cuotas debe ser igual a la renta total y cada cuota debe corresponder a un inquilino con vínculo activo (`SplitAsync_Personalizada_SumaCorrectaYInquilinosActivos_Aplica`).

## Controles de seguridad aplicados

- Autorización por titularidad (mitiga BOLA/IDOR).
- Validación estricta de que cada cuota pertenezca a un inquilino con vínculo activo real (evita asignar renta a cuentas sin relación con la unidad — mitigación de asignación masiva).

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/SplitRentPeriodServiceTests.cs` (7 casos, incluido `SplitAsync_UnidadAjena_DebeDevolverForbidden`).
- Integración: `tests/UniHome.IntegrationTests/RentEndpointTests.cs`.
- Seguridad: la autorización por titularidad está cubierta por `SplitAsync_UnidadAjena_DebeDevolverForbidden`; la validación de pertenencia de cuotas (asignación masiva) está cubierta por `SplitAsync_Personalizada_InquilinoSinVinculoActivo_DebeRechazarse`. Cubierta además dinámicamente por el DAST (`tools/ci/zap_dast.py`) y por RESTler sobre el documento OpenAPI real.
