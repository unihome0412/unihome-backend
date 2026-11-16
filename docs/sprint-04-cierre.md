# Sprint 4 — Cierre backend M06

**Rama:** `feature/sprint-04-backend-integral`
**Base:** `96edfbab40578d6148c00e9585b7fa9cc16e569e` (Sprint 3)
**Alcance:** 11 HU de verificación documental y antifraude asistido.
**No incluye:** decisiones administrativas de aprobar/rechazar (HU-024/HU-025,
Sprint 5), notificaciones (M12), UI Android, despliegue, promoción a Atlas ni
operaciones Azure.

## 1. Matriz de las 11 HU

| HU | RF | Puntos | Soporte backend | Endpoint / decisión | Pruebas locales |
|---|---|---:|---|---|---|
| HU-013 | RF-01 | 3 | Implementado | `POST /api/v1/publicaciones/primera/enviar` + EP-M06-01/02; la navegación es UI Android | Integración flujo completo |
| HU-015 | RF-06 | 3 | Implementado | `POST /api/v1/expedientes/mi-expediente/ine`; JPG/PNG/PDF, cámara es cliente | Validación de archivos + integración + OCR real |
| HU-016 | RF-06 | 5 | Implementado con límite PDF | `GET /admin/expedientes/{id}` y enlace temporal por documento; miniatura real para imágenes, PDF se sirve como vista previa | Integración admin + enlace + auditoría |
| HU-017 | RF-07 | 3 | Implementado | `POST /api/v1/expedientes/mi-expediente/comprobante` | Integración flujo completo |
| HU-018 | RF-08 | 5 | Implementado | `nombreOcrIne`/`nombreOcrComprobante` en detalle admin; nulo si no se puede leer | OCR real Tesseract + unitarias del extractor |
| HU-020 | RF-09 | 5 | Implementado | `indicadorEdicion` con ELA + metadatos y aviso orientativo; nunca decide | ELA real + metadatos + integración |
| HU-021 | RF-10 | 3 | Implementado en dominio y lectura; decisión humana en Sprint 5 | `Pendiente` → `EnRevision` con ambos documentos; estados y transiciones `Aprobado`/`Rechazado` modelados y probados, sin endpoint de decisión en Sprint 4 | Dominio + integración de estados |
| HU-022 | RF-10 | 3 | Implementado | `esSospechoso` y `senalesRiesgo` separados del estado; lista ordena primero sospechosos | Unitarias + integración |
| HU-023 | RF-11 | 3 | Implementado | `GET /admin/expedientes` con solicitante, folio, fecha, estado, sospechoso y contador | Integración admin |
| HU-027 | RF-13 | 3 | Implementado | La unidad pasa a `EnRevision`; M04 solo muestra `Aprobada` | Integración de visibilidad pública |
| HU-088 | RF-44 | 3 | Implementado | AES-256-GCM en `documentos_verificacion`; aviso `avisoAlmacenamiento`; URL firmada temporal, solo admin, auditada | Cifrado, manipulación, expiración, acceso y auditoría |

> **HU-021 es parcial en sentido funcional:** el backend expone y hace cumplir
> las transiciones permitidas y muestra el mismo estado en lista/detalle, pero
> los botones y endpoints de decisión pertenecen a HU-024/HU-025 (Sprint 5). No
> se marcaron como implementados.

## 2. Endpoints nuevos y modificados

| Método | Ruta | Auth | Éxito | Errores |
|---|---|---|---|---|
| POST | `/api/v1/expedientes/mi-expediente/ine` | Bearer solicitante | 200 | 400, 401, 403, 404, 409, 413, 415, 422, 429, 503 |
| POST | `/api/v1/expedientes/mi-expediente/comprobante` | Bearer solicitante | 200 | 400, 401, 403, 404, 409, 413, 415, 422, 429, 503 |
| POST | `/api/v1/publicaciones/primera/enviar` | Bearer solicitante ≥18 | 200 | 401, 403, 404, 409, 413, 422, 503 |
| GET | `/api/v1/admin/expedientes` | Admin | 200 | 400, 401, 403, 503 |
| GET | `/api/v1/admin/expedientes/{id}` | Admin | 200 | 401, 403, 404, 503 |
| GET | `/api/v1/admin/expedientes/{id}/documentos/{tipo}` | Admin | 200 | 401, 403, 404, 503 |
| GET | `/api/v1/expedientes/documentos/{token}` | Admin + token firmado | 200 | 401, 403, 404, 503 |

Cabeceras de la bandeja: `X-Total-Count`, `X-Page`, `X-Page-Size` y
`X-Pending-Review-Count`. El enlace firmado expira en 300 s por defecto (máximo
900 s), se marca `Cache-Control: no-store`, exige sesión administrativa y nunca
publica una URL permanente de Cloudinary ni una `storageKey`.

## 3. Persistencia MongoDB

Detalle completo en [`mongodb-m06.md`](mongodb-m06.md).

- `expedientes_verificacion`: un documento por titular, folio único, estado e
  indicador de riesgo separados.
- `documentos_verificacion`: ciphertext, nonce y tag AES-256-GCM; huella SHA-256
  del binario en claro; versiones históricas.
- `eventos_expediente`: bitácora append-only de carga, análisis, consulta,
  emisión y lectura.
- Migración `M06-001` con 4 índices de expediente, 2 de documento y 2 de
  auditoría. No modifica `M05-001`; la unidad usa `estadoPublicacion`.

## 4. Seguridad y decisiones

- Sujeto siempre desde el claim `sub`; nunca desde body/query.
- Aislamiento: un solicitante no tiene ruta para leer expedientes ajenos; los
  endpoints administrativos exigen policy `AdminAccess`.
- Validación real de archivos: firma JPEG/PNG/PDF, extensión/MIME, tamaño máximo
  10 MB, nombre saneado, PDF completo y rechazo de `/JavaScript`, `/JS`,
  `/Launch` y `/EmbeddedFile`.
- Cifrado autenticado con datos asociados a expediente/tipo/versión/hash. Una
  manipulación o reasociación falla cerrada con `503`.
- URL firmada HMAC-SHA256 con expiración y comparación de tiempo constante. La
  emisión y la lectura se auditan; si la auditoría no persiste, no se entrega el
  documento.
- Duplicado idéntico = idempotente; cambio de evidencia en `EnRevision` = `409`;
  aprobado = terminal para esa ruta.
- ELA y metadatos son señales orientativas. Si no están disponibles, se informa
  (`elaDisponible=false`, `detalle`) y el nombre OCR queda nulo; nunca se inventa.
- Rate limit provisional de 10 cargas/min por IP con el adaptador distribuido.

## 5. Pruebas y evidencias locales

| Control | Resultado | Evidencia |
|---|---|---|
| Restore locked | PASS | `dotnet restore UniHome.Backend.slnx --locked-mode` |
| Build Release + warnings as errors | PASS | 0 errores, 0 advertencias |
| Formato | PASS | `dotnet format --verify-no-changes` |
| Unitarias | PASS | 625/625 |
| Integración | PASS | 217/217 ejecutadas (8 pruebas opt-in de Mongo/Cloudinary omitidas de forma explícita): flujo M06, admin, aislamiento, archivos inválidos, idempotencia, visibilidad |
| OCR real unitario | PASS | Tesseract 5.4.0 local, 2/2 |
| OCR real end-to-end | PASS | multipart → extracción → expediente admin, 1/1 |
| ELA real | PASS | Imagen JPEG generada, recompresión y diferencia de píxeles |
| PDF capa de texto | PASS | PdfPig extrae nombre de un PDF real; PDF sin texto queda NoDisponible |
| Mongo real M06 | BLOCKED | No hay `mongod` local y Docker está prohibido; prueba opt-in preparada |
| Dependencias vulnerables | PASS | `dotnet list package --vulnerable` sin hallazgos |
| Gitleaks local | PASS | 118 commits/2.77 MB y árbol de trabajo 13.35 MB, 0 leaks |
| Stryker.NET | PASS (informativo) | 48.55 % de mutación; 315 evaluados, 184 killed, 131 survived, 0 timeout/errors; umbral break 0 |
| ZAP / RESTler / Sonar / CodeQL / Semgrep remotos | PENDING_CI | No se ejecutaron workflows remotos |
| Docker build | BLOCKED | Prohibido Docker en esta sesión; Dockerfile actualizado sin ejecutarlo |

## 6. Dependencias y pendientes

1. **Aprobación/rechazo (HU-024/HU-025):** Sprint 5. El dominio ya valida las
   transiciones y el estado `Aprobado`/`Rechazado`, pero no hay endpoint para
   decidir ni para cambiar la etiqueta de cuenta.
2. **HU-019 y HU-026 (Sprint 5):** la comparación de nombres queda en
   `NoDisponible` y no se expone `GET /expedientes/mi-expediente`; las respuestas
   de carga sí informan documentos recibidos y estado. No se inventó el resultado
   de comparación.
3. **MongoDB real:** falta un `mongod` local. La migración `M06-001` y la prueba
   `MongoVerificationPersistenceTests` deben ejecutarse en el job manual
   `mongo-db-test` con `UNIHOME_ENABLE_MONGO_CI=true`.
4. **Atlas:** no se aplicó ni promovió ninguna migración. La autorización de
   Bridged sigue pendiente. expedientes_verificacion ya figura en el modelo
   externo; M06-001 debe reconciliar validador/índices antes de promover.
5. **OCR PDF escaneado:** PdfPig extrae capa de texto; un PDF sin texto requiere
   rasterizador (por ejemplo `pdftoppm`) y no está validado. Se marca
   `NoDisponible`.
6. **Miniatura PDF:** no hay rasterizador local aprobado; el detalle entrega el
   PDF como vista previa, no una miniatura rasterizada.
7. **Retención/rotación:** ADR-005 y la rotación de claves siguen pendientes.
   Cambiar `DocumentSecurity:EncryptionKey` invalida documentos previos.
8. **Imagen Docker:** instala `tesseract-ocr` y `tesseract-ocr-eng`; el build real
   no se ejecutó localmente por la restricción de Docker.
9. **Swagger:** sigue deshabilitado por defecto; el documento OpenAPI solo se
   expone cuando `Api:EnableSwagger=true`.

## 7. DevSecOps preparado

- `ci.yml`: `M06-001` añadida a las secuencias de MongoDB CI, ZAP y RESTler; el
  job MongoDB ejecuta `MongoVerificationPersistenceTests`.
- OpenAPI: pruebas de rutas, seguridad Bearer, enums string y multipart binario
  para ambas cargas.
- Nuevo workflow manual `.github/workflows/ocr-documental.yml`: instala
  Tesseract, ejecuta pruebas reales unitarias/end-to-end y falla si hay omisiones.
- Se conservan Semgrep, Gitleaks, CodeQL, SonarCloud, Stryker, Pact, ZAP,
  RESTler, Docker/Dockle/Container Smoke y los gates existentes.
- No se ejecutó ningún workflow remoto, deploy, push, PR, merge ni operación
  Azure/Atlas/Redis/Cloudinary.

## 8. Cómo verificar localmente

```powershell
dotnet restore UniHome.Backend.slnx --locked-mode
dotnet build UniHome.Backend.slnx --configuration Release -warnaserror
dotnet test UniHome.Backend.slnx --configuration Release
dotnet format UniHome.Backend.slnx --verify-no-changes
```

Prueba real de OCR (requiere Tesseract instalado):

```powershell
$env:UNIHOME_TESSERACT_PATH = "$env:LOCALAPPDATA\Programs\Tesseract-OCR\tesseract.exe"
dotnet test tests/UniHome.UnitTests --configuration Release --filter "FullyQualifiedName~RealOcrTests"
dotnet test tests/UniHome.IntegrationTests --configuration Release --filter "FullyQualifiedName~RealOcrVerificationEndpointTests"
```

Prueba real de MongoDB (requiere una base de verificación autorizada):

```powershell
$env:UNIHOME_RUN_MONGO_INTEGRATION_TESTS = '1'
$env:MongoDb__ConnectionString = '<URI_DE_BASE_DE_VERIFICACION>'
$env:MongoDb__DatabaseName = 'unihome_modelo_pruebas'
dotnet run --project tools/UniHome.MongoMigrator -- apply --database unihome_modelo_pruebas --confirm --version M06-001
dotnet test tests/UniHome.IntegrationTests --configuration Release --filter "FullyQualifiedName~MongoVerificationPersistenceTests"
```

## 9. Commit manual (lo ejecuta el responsable)

No se ejecutó ningún commit. Para revisar y confirmar el Sprint 4 localmente:

```powershell
git status --short
git diff --check
git diff --stat
git add -A
git status --short
git commit -m "feat(sprint-04): implementar verificacion documental M06"
git log -1 --oneline
```

No se debe ejecutar `git push`, abrir PR, hacer merge ni desplegar hasta que el
responsable lo autorice.

## 10. Clasificación

**SPRINT 4 BACKEND IMPLEMENTADO — NO PRODUCTIVO.** Los seis endpoints M06 y el
envío de la primera publicación están implementados, integrados con Sprints 1–3
y cubiertos localmente. La explotación real sigue condicionada por la migración
`M06-001` en la base autorizada, la aprobación administrativa de Sprint 5, la
validación en CI y la autorización de despliegue.

No se hizo commit, push, PR, merge, deploy ni cambio de infraestructura.
