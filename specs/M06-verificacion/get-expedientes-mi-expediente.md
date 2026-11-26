# `GET /api/v1/expedientes/mi-expediente`

| Campo | Valor |
|---|---|
| Módulo | M06 – Verificación documental |
| HU | HU-026 |
| Operación OpenAPI | `GET /api/v1/expedientes/mi-expediente` |
| Controlador | `src/UniHome.Api/Controllers/VerificationController.cs` → `GetMyExpediente` (EP-M06-03) |

## Autenticación y autorización

Requiere `Authorization: Bearer` (`[Authorize]`). El expediente se busca
por `FindByOwnerAsync(accountId)` usando el `sub` del token, nunca un id
recibido del cliente: una cuenta solo puede ver su propio expediente
(anti-IDOR por diseño, no hay parámetro de id en la ruta).

## Request

Sin parámetros.

## Response

`200 OK` con `MiExpedienteResponse` (`MiExpedienteView.FromDomain`):
estado de recepción de documentos (INE/comprobante), progreso del análisis
automático, decisión (`Pendiente`/`EnRevision`/`Aprobado`/`Rechazado`) y
motivo de rechazo si aplica. **Nunca** expone riesgo, OCR, ELA ni
identificadores internos de almacenamiento (eso es exclusivo de la vista
administrativa de detalle).

## Validaciones

Ninguna de entrada; es una consulta sin parámetros.

## Códigos de error

| Código | Causa |
|---|---|
| 401 | Sin sesión válida. |
| 404 | La cuenta todavía no tiene un expediente de verificación (no ha subido ningún documento). |
| 503 | Preflight de esquema Mongo fallido para `expedientes_verificacion`. |

## Reglas de negocio

- Es una proyección de solo lectura de `Expediente`; no muta estado.
- La separación de vistas (`MiExpedienteView` vs. `ExpedienteDetalleView`
  del detalle administrativo) es una regla de producto explícita: el
  solicitante nunca ve las señales de riesgo ni el resultado crudo de OCR,
  solo si su documento fue "reconocido"/"en revisión manual" a través de
  los campos que expone `MiExpedienteResponse`.

## Controles de seguridad aplicados

- Minimización de datos expuestos al propio titular (nunca señales de
  riesgo/OCR/ELA ni datos de otros usuarios).
- Resolución del expediente exclusivamente por el `accountId` del token
  (sin parámetro de id en la ruta → no hay superficie de IDOR en este
  endpoint).

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/VerificationServiceTests.cs`
  → `EstadoPropio_IncluyeDecisionYNoExponeIndicadoresInternos`.
- Integración: `tests/UniHome.IntegrationTests/VerificationEndpointTests.cs`
  → `EstadoPropioSinExpediente` cubierto indirectamente en
  `CargasSinSesionSinUnidadYMenor_DebenFallarConElCodigoCorrecto`; no se
  encontró una prueba de integración HTTP dedicada exclusivamente a
  `GET mi-expediente` con 200 fuera del flujo completo
  (`FlujoCompleto_DocumentosPasanARevisionYLaUnidadNoEsPublica`, que lo
  consulta como parte del flujo).
- Seguridad: cubierto por el recorrido dinámico de ZAP sobre todas las
  rutas `/api/v1/*` (`tools/ci/zap_dast.py`) y por la gramática RESTler
  generada del mismo documento OpenAPI (`tools/ci/restler/normalize_openapi.py`).
