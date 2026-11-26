# `GET /api/v1/admin/expedientes/{id}`

| Campo | Valor |
|---|---|
| Módulo | M06 – Verificación documental |
| HU | HU-016/HU-018/HU-020/HU-021/HU-022 |
| Operación OpenAPI | `GET /api/v1/admin/expedientes/{id}` |
| Controlador | `src/UniHome.Api/Controllers/AdminVerificationController.cs` → `Detail` (EP-M06-05) |

## Autenticación y autorización

Policy **Admin** a nivel de clase (ver [`get-admin-expedientes.md`](get-admin-expedientes.md)).
`id` es un `Guid` de ruta; cualquier administrador autorizado puede
consultar cualquier expediente (es el propósito de la bandeja), pero el
acceso queda auditado (`VerificationAuditActions.ExpedienteViewed`) antes
de devolver datos personales — trazabilidad en vez de restricción de
pertenencia, ya que el recurso es intrínsecamente administrativo.

## Request

Parámetro de ruta `id` (`Guid`).

## Response

`200 OK` con `ExpedienteDetalleResponse` (`GetExpedienteDetailService.GetAsync`):
nombre y correo del solicitante, fecha de creación, estado, URLs de
miniatura firmadas (no permanentes) para INE y comprobante, nombre
detectado por OCR de cada documento, resultado de comparación de nombres,
indicador de edición/ELA/metadatos, flag `esSospechoso`, señales de riesgo,
si el análisis automático se completó, y motivo de rechazo si aplica.
**Nunca** devuelve `storageKey` ni una URL permanente al binario.

## Validaciones

Ninguna adicional de entrada; `id:guid` se valida por el constraint de
ruta de ASP.NET (un valor no-GUID no enruta a la acción).

## Códigos de error

| Código | Causa |
|---|---|
| 401 | Sin sesión válida. |
| 403 | La cuenta no tiene privilegio administrativo. |
| 404 | El expediente no existe. |
| 503 | Preflight de esquema Mongo fallido. |

## Reglas de negocio

- Las URLs de documento son **miniaturas firmadas** (`DocumentUrlVariant.Thumbnail`)
  de corta vida (300 s por defecto, máximo 900 s según `UrlLifetimeSeconds`
  documentado en README); el acceso al documento completo exige el
  endpoint separado `GET /{id}/documentos/{tipo}`.
- Si la comparación de nombres (`EffectiveNameComparison`) detecta una
  diferencia, se añade una señal de riesgo orientativa adicional
  ("La comparación orientativa de nombres detectó una diferencia.") sin
  que esto decida la aprobación/rechazo.
- Cada consulta de detalle se audita (`ExpedienteAuditActions.ExpedienteViewed`)
  con el id del administrador, incluso si solo está revisando sin decidir.

## Controles de seguridad aplicados

- Autorización por función en `/admin/*` (policy Admin server-side).
- Auditoría de cada acceso a datos personales/sensibles (OCR, correo,
  nombre) antes de entregarlos.
- Nunca se expone la clave de almacenamiento (`storageKey`) ni una URL
  permanente al binario: solo enlaces firmados de corta vida.
- El análisis automático (OCR/ELA/comparación de nombres) se marca
  explícitamente como orientativo y no decide la aprobación.

## Pruebas que la cubren

- Unitarias: no se encontró `GetExpedienteDetailServiceTests.cs` dedicado;
  cobertura indirecta vía la prueba de integración de detalle (ver abajo).
- Integración: `tests/UniHome.IntegrationTests/VerificationEndpointTests.cs`
  → `DetalleYEnlace_DebenAuditarYNoExponerLlaves`,
  `ComparacionOcr_DebeMarcarDiferenciaSinDecidirNiFiltrarAlSolicitante`.
- Seguridad: cubierto por el recorrido dinámico de ZAP y la gramática
  RESTler sobre el documento OpenAPI; no se encontró una prueba de
  seguridad dedicada a IDOR cruzado entre administradores (el recurso es
  administrativo global, no por-propietario, por lo que no aplica el
  mismo patrón de IDOR que en endpoints de titular/inquilino).
