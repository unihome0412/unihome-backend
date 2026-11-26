# `POST /api/v1/expedientes/mi-expediente/ine`

| Campo | Valor |
|---|---|
| Módulo | M06 – Verificación documental |
| HU | HU-015/HU-088 |
| Operación OpenAPI | `POST /api/v1/expedientes/mi-expediente/ine` |
| Controlador | `src/UniHome.Api/Controllers/VerificationController.cs` → `UploadIne` (EP-M06-01) |

## Autenticación y autorización

Requiere `Authorization: Bearer` (`[Authorize]`). No hay policy de rol: cualquier
cuenta autenticada puede intentar subir su propia INE, pero
`PublishingEligibilityService.IsEligibleAsync` (invocado desde
`UploadVerificationDocumentService.UploadAsync`) exige que la cuenta sea
elegible para publicar (mayor de 18 años con al menos una unidad propia);
si no lo es, responde `403`. El documento se asocia siempre al
`ownerAccountId` del token (`TryGetAccountId`), nunca a un id recibido del
cliente, por lo que no es posible subir un documento a nombre de otra
cuenta (anti-IDOR).

## Request

- `multipart/form-data` con el campo `Archivo` (binario, `SubirDocumentoRequest`)
  y opcionalmente `NombreArchivoOriginal`.
- Tipos aceptados: JPG, PNG o PDF, determinados por **firma binaria real**,
  no por la extensión o el `Content-Type` declarado.

## Response

`200 OK` con `MiExpedienteResponse` (ver `GetMyExpedienteService`): expone
recepción de documentos, progreso automático, decisión y motivo de rechazo.
**Nunca** expone `storageKey`, el binario, señales de riesgo, OCR ni ELA
(eso es exclusivo del detalle administrativo).

## Validaciones

- `Request.ContentLength` ≤ `VerificationDocumentValidator.MaxRequestBytes`
  (10 MB + 1 MB de margen multipart); si no, `413`.
- Archivo requerido y no vacío; si falta, `400` (`ValidationProblemDetails`).
- Tamaño por documento ≤ `MaxBytesPerDocument` (10 MB); si no, `413`.
- `VerificationDocumentValidator.Validate` (firma binaria real):
  - Detecta el tipo real por bytes mágicos (JPEG `FFD8FF`, PNG, `%PDF-`);
    si no coincide con ninguno, error de validación.
  - La extensión declarada en el nombre de archivo debe coincidir con el
    tipo real detectado (excepto `.jpeg`/`.jpg`).
  - El `Content-Type` declarado por el cliente debe coincidir con el
    real (o ser `application/octet-stream`).
  - Si es PDF: debe tener un `%%EOF` válido y **no** contener
    `/JavaScript`, `/JS`, `/Launch` ni `/EmbeddedFile` (regex con timeout
    de 200 ms) — rechaza PDF con contenido activo.
  - El nombre de archivo se normaliza y rechaza si tiene caracteres de
    control, separadores de ruta o excede la longitud máxima.
- Fallos de validación de contenido devuelven `422` (no `400`), con el
  mensaje real del validador.

## Códigos de error

| Código | Causa |
|---|---|
| 400 | Falta el campo `Archivo` o no se pudo leer el stream. |
| 401 | Sin sesión válida. |
| 403 | La cuenta no es elegible para publicar (no mayor de edad / sin unidad propia). |
| 404 | La cuenta no tiene todavía una primera unidad propia a la que asociar el expediente. |
| 409 | El expediente ya está `EnRevision` o `Aprobado`: no admite cambios de evidencia. |
| 413 | Solicitud o documento exceden el tamaño permitido. |
| 415 | Tipo de contenido no soportado (gestionado por el framework antes del binding). |
| 422 | El archivo no tiene firma binaria válida, contiene PDF activo, o el nombre es inválido. |
| 429 | Límite de subidas de documentos excedido (`AuthRateLimitPolicies.DocumentUpload`, `DocumentUploadPermitLimit`/`DocumentUploadWindowSeconds`, 10/60s por defecto). |
| 503 | Fallo de almacenamiento (`StorageUnavailableException`) o preflight de esquema Mongo fallido. |

## Reglas de negocio

- El expediente (`Expediente`) se crea bajo demanda en la primera subida
  (`GetContextAsync`) asociado a la primera unidad propia del titular;
  reintenta ante una carrera de creación (`ExpedienteAlgunasyaExistsException`).
- Documentos se versionan (`DocumentoVerificacion.Version`); subir el mismo
  contenido (mismo SHA-256) es **idempotente** y no genera una nueva versión
  ni auditoría nueva.
- El contenido se cifra en reposo (AES-256-GCM, ver README "Sprint 4 / M06")
  antes de persistirse; el adaptador de análisis (OCR/ELA/metadatos) es
  orientativo y nunca decide por sí solo (`AnalyzeSafelyAsync` degrada a
  "no disponible" si el analizador falla, sin bloquear la subida).
- El expediente pasa a `EnRevision` únicamente cuando **ambos** documentos
  (INE y comprobante) existen; solo entonces se intenta enviar la unidad a
  revisión (`TrySubmitPublicationAsync`), y solo si además hay fotos y
  términos aceptados.
- Escritura con CAS de un solo documento (`TryReplaceAsync` contra
  `expediente.UpdatedAt`); ante una carrera de concurrencia reintenta hasta
  `MaxConcurrencyAttempts` (3) antes de responder `409`.
- Migración `M06-001` crea `expedientes_verificacion`, `documentos_verificacion`
  y `eventos_expediente` con índices únicos por propietario/folio y por
  `(expedienteId, tipo, version)`; no modifica `M05-001`.

## Controles de seguridad aplicados

- Firma binaria real (no extensión/MIME del cliente) contra suplantación de
  tipo de archivo.
- Rechazo explícito de contenido activo en PDF (`/JavaScript`, `/JS`,
  `/Launch`, `/EmbeddedFile`).
- Límite de tamaño por documento y por solicitud (anti payload-DoS).
- Rate limiting dedicado de subidas de documentos (`DocumentUploadPermitLimit`).
- Cifrado en reposo del binario (AES-256-GCM); el binario en claro nunca se
  persiste ni se devuelve en esta respuesta.
- El documento siempre se asocia al `accountId` del token, nunca a un id
  del body (anti-IDOR).
- Auditoría de cada subida (`VerificationAuditActions.DocumentUploaded`).

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/VerificationServiceTests.cs`
  (`SubirIneYComprobante_DebeCrearExpedienteYEnviarUnidadARevision`,
  `ArchivoInvalido_DebeResponderValidacionSinCrearExpediente`,
  `CuentaNoElegible_DebeResponderForbidden`,
  `MismoHash_DebeSerIdempotenteYSinNuevaAuditoria`,
  `CambiarEvidenciaEnRevision_DebeResponderConflict`,
  `AnalizadorFallido_DebeGuardarDocumentoConNombreNulo`,
  `SubirSinDocumento_DebeRechazarArgumentoNulo`,
  `SubirConExpedienteAprobadoODesvinculado_DebeRechazarse`,
  `SubirSinFotoOSinTerminos_NoDebeEnviarLaUnidadARevision`,
  `FalloDeAlmacenamiento_DebePropagarseComo503`,
  `ConcurrenciaOptimistaAgotada_DebeResponderConflictYLimpiar`).
- Integración: `tests/UniHome.IntegrationTests/VerificationEndpointTests.cs`
  (`FlujoCompleto_DocumentosPasanARevisionYLaUnidadNoEsPublica`,
  `CargasInvalidas_DebenRechazarseYLaRepeticionSerIdempotente`,
  `CargasSinSesionSinUnidadYMenor_DebenFallarConElCodigoCorrecto`) y
  `tests/UniHome.IntegrationTests/RealOcrVerificationEndpointTests.cs`
  (`IneReal_DebeExtraerNombreConTesseractYMostrarloEnElExpediente`, prueba
  real opt-in con Tesseract).
- Seguridad: el DAST (`tools/ci/zap_dast.py`) envía tráfico real contra
  todas las rutas `/api/v1/*` del documento OpenAPI, incluida esta; el
  fuzzing RESTler (`tools/ci/restler/normalize_openapi.py`) construye su
  gramática a partir del mismo documento. No hay una prueba de seguridad
  dedicada a la inyección de PDF activo más allá de la validación unitaria
  del validador; el resultado concreto de un run DAST/RESTler se verifica
  en el Bloque 4.
