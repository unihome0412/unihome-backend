# `GET /api/v1/expedientes/documentos/{token}`

| Campo | Valor |
|---|---|
| Módulo | M06 – Verificación documental |
| HU | HU-016/HU-088 |
| Operación OpenAPI | `GET /api/v1/expedientes/documentos/{token}` |
| Controlador | `src/UniHome.Api/Controllers/VerificationDocumentsController.cs` → `Read` |

## Autenticación y autorización

`[Authorize(Policy = AdminAccessOptions.PolicyName)]` a nivel de clase:
**nunca es público**, aunque la ruta no esté bajo `/admin/`. Consumir el
enlace exige sesión administrativa además de la validez del token firmado
en sí; un token válido sin sesión administrativa responde `401`/`403`, no
`200`.

## Request

Parámetro de ruta `token` (string, enlace firmado emitido por
`POST .../documentos/{tipo}` del controlador administrativo).

## Response

`200 OK` con el binario del documento (`File(...)`), `Content-Type` real
detectado en la carga original, y para la variante completa
`Content-Disposition` con el nombre original del archivo (para miniaturas,
sin `fileDownloadName`). Cabeceras de respuesta explícitas:
`Cache-Control: no-store`, `Content-Security-Policy: default-src 'none'`,
`X-Content-Type-Options: nosniff`.

## Validaciones

- El token debe ser válido y no haber expirado (`IDocumentUrlSigner.Validate`);
  si no, `404` (no se distingue "no existe" de "expiró", para no dar pistas).
- El expediente y el documento referenciados deben existir y la versión
  del documento debe coincidir con la versión firmada en el token (evita
  que un enlace viejo sirva una versión reemplazada); si no, `404`.
- El hash SHA-256 y el tamaño almacenados deben coincidir con los
  registrados en el expediente; una discrepancia lanza
  `StorageUnavailableException` (falla cerrado, nunca sirve contenido no
  verificado) → `503`.

## Códigos de error

| Código | Causa |
|---|---|
| 401 | Sin sesión válida. |
| 403 | La cuenta no tiene privilegio administrativo. |
| 404 | El token es inválido/expiró, o el documento/expediente/versión no coinciden. |
| 503 | Discrepancia de integridad (hash/tamaño) o preflight de esquema Mongo fallido. |

## Reglas de negocio

- Cada lectura exitosa se audita (`VerificationAuditActions.DocumentViewed`),
  distinguiendo miniatura de documento completo en el detalle de
  auditoría.
- El contenido se descifra en el momento de servirlo; nunca se persiste
  en claro en disco.
- No usa `enableRangeProcessing` para el documento completo (evita fugas
  por rangos parciales en contenido sensible).

## Controles de seguridad aplicados

- Autorización por función (`Admin`) incluso para "consumir un enlace
  firmado": el token no es capacidad suficiente por sí sola.
- Verificación de integridad (SHA-256 + tamaño) antes de servir contenido:
  falla cerrado ante discrepancia en vez de servir un binario no
  verificado.
- Cabeceras anti-cacheo y anti-sniffing explícitas en la respuesta
  (`no-store`, `default-src 'none'`, `nosniff`).
- Auditoría de cada acceso a contenido sensible.

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/VerificationServiceTests.cs`
  (`LecturaDeDocumento_DebeValidarTokenYAuditar`,
  `LecturaFirmada_RechazaTokenExpedienteDocumentoVersionOAlmacenamiento`) —
  ejercitan directamente `ReadVerificationDocumentService.ReadAsync`.
- Integración: `tests/UniHome.IntegrationTests/VerificationEndpointTests.cs::EnlaceDeDocumento_EsTemporalExigeAdminYAuditaElAcceso`
  (emite el enlace con el endpoint administrativo, lo consume como admin —
  `200` —, como anónimo — `401` —, como solicitante sin privilegio — `403` —,
  con el token alterado — `404` — y verifica el registro de auditoría).
- Seguridad: cubierto por el recorrido dinámico de ZAP y la gramática
  RESTler sobre el documento OpenAPI. La prueba unitaria
  `LecturaFirmada_RechazaTokenExpedienteDocumentoVersionOAlmacenamiento`
  ejercita directamente el control anti-BOLA/IDOR (token/versión/documento
  no coincidentes) de este endpoint sensible.
