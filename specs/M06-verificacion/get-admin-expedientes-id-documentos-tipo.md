# `GET /api/v1/admin/expedientes/{id}/documentos/{tipo}`

| Campo | Valor |
|---|---|
| Módulo | M06 – Verificación documental |
| HU | HU-016/HU-088 |
| Operación OpenAPI | `GET /api/v1/admin/expedientes/{id}/documentos/{tipo}` |
| Controlador | `src/UniHome.Api/Controllers/AdminVerificationController.cs` → `DocumentUrl` (EP-M06-06) |

## Autenticación y autorización

Policy **Admin** a nivel de clase (ver [`get-admin-expedientes.md`](get-admin-expedientes.md)).
Emite el enlace, pero el enlace resultante exige **de nuevo** sesión
administrativa al consumirse (`GET /api/v1/expedientes/documentos/{token}`
también tiene `[Authorize(Policy = AdminAccessOptions.PolicyName)]`): doble
verificación, el token firmado por sí solo no basta.

## Request

Parámetros de ruta `id` (`Guid`, expediente) y `tipo`
(`TipoDocumentoVerificacion`: `Ine` o `Comprobante`).

## Response

`200 OK` con `DocumentoUrlResponse` (`DocumentAccessUrlView`): la URL
firmada (variante completa, no miniatura), su fecha de expiración y el
tipo de documento.

## Validaciones

Ninguna adicional de entrada; `tipo` se valida por el binding del enum de
ruta (un valor fuera del enum no enruta / responde `400` del framework).

## Códigos de error

| Código | Causa |
|---|---|
| 401 | Sin sesión válida. |
| 403 | La cuenta no tiene privilegio administrativo. |
| 404 | El expediente o el documento del tipo pedido no existen. |
| 503 | Preflight de esquema Mongo fallido. |

## Reglas de negocio

- El enlace firmado (`IDocumentUrlSigner.Create`) vence a los 300 s por
  defecto (máximo 900 s, `UrlLifetimeSeconds`); es de variante "Full"
  (documento completo), distinto de las miniaturas usadas en el detalle.
- La emisión queda auditada (`VerificationAuditActions.DocumentUrlIssued`)
  antes de entregar la URL, no después de que se consuma.

## Controles de seguridad aplicados

- Autorización por función en `/admin/*` para emitir **y** para consumir
  el enlace (doble capa, el token firmado no sustituye la sesión).
- Enlaces de corta vida en vez de URLs permanentes al binario.
- Auditoría de la emisión del enlace, independiente de si luego se
  consume.

## Pruebas que la cubren

- Unitarias: no se encontró `IssueDocumentAccessUrlServiceTests.cs`
  dedicado; cobertura indirecta en la prueba de integración de enlace
  temporal (ver abajo).
- Integración: `tests/UniHome.IntegrationTests/VerificationEndpointTests.cs`
  → `EnlaceDeDocumento_EsTemporalExigeAdminYAuditaElAcceso`.
- Seguridad: cubierto por el recorrido dinámico de ZAP y la gramática
  RESTler sobre el documento OpenAPI. La prueba de integración citada ya
  ejercita el control anti-acceso-no-autorizado (BOLA/IDOR sobre
  documentos sensibles) al verificar que el enlace exige sesión
  administrativa.
