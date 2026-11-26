# `POST /api/v1/expedientes/mi-expediente/comprobante`

| Campo | Valor |
|---|---|
| Módulo | M06 – Verificación documental |
| HU | HU-017/HU-088 |
| Operación OpenAPI | `POST /api/v1/expedientes/mi-expediente/comprobante` |
| Controlador | `src/UniHome.Api/Controllers/VerificationController.cs` → `UploadProof` (EP-M06-02) |

## Autenticación y autorización

Idéntico a `POST /api/v1/expedientes/mi-expediente/ine`
([ver spec](post-expedientes-mi-expediente-ine.md)): `[Authorize]`, elegibilidad
verificada por `PublishingEligibilityService.IsEligibleAsync`, y el documento
se asocia siempre al `accountId` del token (anti-IDOR).

## Request

`multipart/form-data` con el campo `Archivo` (escritura, boleta predial o
constancia de propiedad) en JPG, PNG o PDF, y opcionalmente
`NombreArchivoOriginal`. Misma validación de contenido real que la INE.

## Response

`200 OK` con `MiExpedienteResponse`. Igual que la INE, nunca expone
`storageKey`, binario, riesgo, OCR ni ELA.

## Validaciones

Comparte exactamente el mismo validador
(`VerificationDocumentValidator.Validate`) y el mismo flujo
(`UploadVerificationDocumentService.UploadAsync` con
`documentType = TipoDocumentoVerificacion.Comprobante`) que la INE: firma
binaria real, coincidencia de extensión/Content-Type, PDF sin contenido
activo (`/JavaScript`, `/JS`, `/Launch`, `/EmbeddedFile`), límite de 10 MB
por documento y nombre de archivo normalizado. Ver detalle completo en
[`post-expedientes-mi-expediente-ine.md`](post-expedientes-mi-expediente-ine.md).

## Códigos de error

| Código | Causa |
|---|---|
| 400 | Falta el campo `Archivo` o no se pudo leer el stream. |
| 401 | Sin sesión válida. |
| 403 | La cuenta no es elegible para publicar. |
| 404 | La cuenta no tiene todavía una primera unidad propia. |
| 409 | El expediente ya está `EnRevision` o `Aprobado`. |
| 413 | Solicitud o documento exceden el tamaño permitido. |
| 415 | Tipo de contenido no soportado. |
| 422 | El archivo no tiene firma binaria válida o contenido activo en PDF. |
| 429 | Límite de subidas de documentos excedido. |
| 503 | Fallo de almacenamiento o preflight de esquema Mongo fallido. |

## Reglas de negocio

Mismas reglas que la INE: versionado por hash (idempotente ante el mismo
SHA-256), cifrado en reposo, análisis orientativo sin decidir, CAS con
reintentos y transición a `EnRevision` solo cuando **ambos** documentos
existen y hay foto y términos aceptados. Migración `M06-001`.

## Controles de seguridad aplicados

Idénticos a la INE: firma binaria real, rechazo de PDF con contenido
activo, límites de tamaño, rate limiting de subidas, cifrado en reposo,
asociación exclusiva al `accountId` del token y auditoría de la subida.

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/VerificationServiceTests.cs`
  (mismo conjunto que la INE: `SubirIneYComprobante_DebeCrearExpedienteYEnviarUnidadARevision`,
  `ArchivoInvalido_DebeResponderValidacionSinCrearExpediente`,
  `CuentaNoElegible_DebeResponderForbidden`,
  `MismoHash_DebeSerIdempotenteYSinNuevaAuditoria`,
  `CambiarEvidenciaEnRevision_DebeResponderConflict`,
  `SubirSinFotoOSinTerminos_NoDebeEnviarLaUnidadARevision`,
  `FalloDeAlmacenamiento_DebePropagarseComo503`,
  `ConcurrenciaOptimistaAgotada_DebeResponderConflictYLimpiar`) — el handler
  es compartido entre ambos tipos de documento.
- Integración: `tests/UniHome.IntegrationTests/VerificationEndpointTests.cs`
  (`FlujoCompleto_DocumentosPasanARevisionYLaUnidadNoEsPublica`,
  `CargasInvalidas_DebenRechazarseYLaRepeticionSerIdempotente`,
  `CargasSinSesionSinUnidadYMenor_DebenFallarConElCodigoCorrecto`).
- Seguridad: cubierto por el recorrido dinámico de ZAP
  (`tools/ci/zap_dast.py`) sobre todas las rutas `/api/v1/*` del documento
  OpenAPI y por la gramática RESTler generada del mismo documento
  (`tools/ci/restler/normalize_openapi.py`); no hay prueba de seguridad
  dedicada adicional a la validación unitaria del validador.
