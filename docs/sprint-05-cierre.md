# Sprint 5 — Cierre backend M02/M05/M06

**Rama:** `feature/sprint-05-backend-integral`
**Base:** `33d5d5e0d116829942c4cc0a3b8ec51adc718e99` (Sprint 4 en `main`)
**Alcance:** 6 HU nuevas + cierre de HU-021; 56 HU de Sprints 1–5.
**No incluye:** Sprint 6 (chat, ocupación, nuevas unidades, rentas/reparaciones,
M13 Usuarios), notificaciones push, Swagger público, Azure, Atlas ni Docker
local. Los workflows remotos se adaptaron pero no se ejecutaron en GitHub.

## 1. Estado de las 6 HU

| HU | Estado | Entrega backend | Pruebas locales |
|---|---|---|---|
| HU-009 | Implementada en API / navegación UI | Perfil deriva `etiquetaCuenta`, `estadoPrimeraPublicacion` y `puedePublicar=false` con publicación; no se agregó `/mis-propiedades` (HU-033, Sprint 6) | Unitarias de etiqueta + integración perfil en revisión/aprobado/rechazado |
| HU-019 | Implementada | Comparación normalizada de nombres OCR, `CoincidenciaOrientativa`/`DiferenciaDetectada`/`NoDisponible`; diferencia marca `Sospechoso`, nunca decide | Normalización, compuestos, acentos, orden, integración admin |
| HU-024 | Implementada | `POST /admin/expedientes/{id}/aprobar`; CAS, auditoría, unidad `Aprobada`, M04 visible, perfil actualizado | Unitarias + integración + concurrencia |
| HU-025 | Implementada | `POST /admin/expedientes/{id}/rechazar` con motivo; unidad `Rechazada`, M04 oculta, motivo consultable | Unitarias + integración |
| HU-026 | Implementada | `GET /expedientes/mi-expediente`: documentos, revisión automática, decisión y motivo; sin riesgo/OCR | Unitarias + integración + aislamiento |
| HU-028 | Implementada | `GET /mi-publicacion/estado-mapa`: pin privado con etiqueta «Tu espacio · En revisión»; solo propietario | Unitarias + integración de visibilidad |

## 2. Cierre de HU-021

HU-021 queda **completa** en Sprint 5:

- `Pendiente`: expediente preliminar sin ambos documentos.
- `EnRevision`: ambos documentos recibidos; la unidad también pasa a `EnRevision`.
- `Aprobado`: decisión humana favorable, actor/fecha/motivo y auditoría; unidad `Aprobada`.
- `Rechazado`: decisión humana con motivo obligatorio, auditoría y unidad `Rechazada`.
- Lista y detalle leen el mismo estado persistido.
- Una decisión duplicada o una carrera entre administradores devuelve `409`.
- Las señales automáticas (ELA, OCR, nombres, Sospechoso) no cambian el estado.

La API no implementa una reapertura administrativa: un cambio posterior requiere
un proceso distinto, conforme a `spec.md`.

## 3. Endpoints nuevos/modificados

| Método | Ruta | Autorización | Éxito | Errores |
|---|---|---|---|---|
| GET | `/api/v1/expedientes/mi-expediente` | Bearer solicitante | 200 `MiExpedienteResponse` | 401, 404, 503 |
| POST | `/api/v1/admin/expedientes/{id}/aprobar` | Admin | 200 `ExpedienteDetalleResponse` | 401, 403, 404, 409, 413, 503 |
| POST | `/api/v1/admin/expedientes/{id}/rechazar` | Admin | 200 `ExpedienteDetalleResponse` | 400, 401, 403, 404, 409, 413, 503 |
| GET | `/api/v1/mi-publicacion/estado-mapa` | Bearer propietario | 200 `MiPublicacionMapaResponse` | 401, 404, 422, 503 |

Respuestas existentes modificadas de forma compatible:

- `MiExpedienteResponse` añade `decision` (`estado`, `fecha`, `motivo`) y conserva
  `avisoAlmacenamiento`.
- `PerfilResponse` añade `estadoPrimeraPublicacion` (`Borrador`, `EnRevision`,
  `Aprobada`, `Rechazada` o `null`).
- Tarjeta, ficha, vista previa y favoritos ahora calculan `arrendadorVerificado`
  desde un expediente aprobado.
- `resultadoComparacionNombres` deja de ser siempre `NoDisponible`: se calcula en
  carga/actualización de OCR y se expone solo en el detalle administrativo.

## 4. Cambios MongoDB

**No se añadió una migración nueva.** `M06-001` conserva su checksum. Se usan
campos flexibles ya existentes o nuevos sin índice:

- `expedientes_verificacion.resultadoComparacionNombres` y
  `comparacionNombresActualizadaAt`.
- `expedientes_verificacion.decision` y `motivoRechazo` (ya existentes en Sprint 4).
- `documentos_verificacion.keyVersion` para rotación AES.
- Índices existentes: `ux_expedientes_propietario`, `ux_expedientes_folio`,
  `ix_expedientes_revision`, `ux_documentos_expediente_tipo_version`, etc.
- `unidades.estadoPublicacion` se actualiza con CAS; no cambia su esquema ni
  `M05-001`.

No se aplicó `M06-001` a Atlas. La colección externa `expedientes_verificacion`
debe reconciliar validador/índices antes de promover.

## 5. Archivos principales

**Nuevos**
- `src/UniHome.Domain/Verification/PersonNameComparison.cs`
- `src/UniHome.Application/Verification/Admin/DecideExpedienteService.cs`
- `src/UniHome.Application/Verification/Documents/GetMyExpedienteService.cs`
- `src/UniHome.Application/Publications/MyPublicationMap/GetMyPublicationMapService.cs`
- `src/UniHome.Api/Controllers/...` cambios en `AdminVerificationController`,
  `VerificationController`, `PublicationsController`
- `src/UniHome.Api/Contracts/Verification/RechazarExpedienteRequest.cs`
- `src/UniHome.Api/Contracts/Publications/MiPublicacionMapaResponse.cs`
- `src/UniHome.Infrastructure/Analysis/PdfRasterizer.cs`
- Tests de comparación, decisiones, estado propio, mapa, rotación de clave,
  PDF escaneado real y OCR real.
- `docs/sprint-05-cierre.md` (este documento).

**Modificados**
- `Expediente`, `Unit`, `GetProfileService`, `ProfileView`,
  `PerfilResponse`, `GetUnitCardService`, `GetUnitDetailService`,
  `GetUnitPreviewService`, `ExploreUnitsService`, `ListFavoritesService`
- `MongoVerificationRepository`, `MongoVerificationDocumentStore`,
  `MongoUnitRepository`, `MongoVerificationSchema`/documentos BSON
- `DocumentSecurityOptions`, `DocumentAnalysisOptions`, `AesGcmDocumentCipher`,
  `LocalDocumentAnalyzer`, `SkiaDocumentPreviewGenerator`, DI
- `Dockerfile` (Tesseract + Poppler), `appsettings.json`
- Workflows, README y documentación.

## 6. Pruebas locales

| Control | Resultado |
|---|---|
| `dotnet restore --locked-mode` | PASS |
| `dotnet build -c Release -warnaserror` | PASS, 0 warnings/errores |
| `dotnet format --verify-no-changes` | PASS |
| Unitarias | **744/744 PASS** |
| Integración | **227/227 ejecutadas PASS**; 9 opt-in Mongo/Cloudinary omitidas explícitamente |
| OpenAPI | PASS: 41 operaciones, rutas nuevas, Bearer, enums string |
| Python CI | **163/163 PASS** |
| HU-019 | PASS: acentos, mayúsculas, compuestos, orden, diferencia, no disponible |
| HU-024 | PASS: aprobación, visibilidad M04, perfil, auditoría, duplicado, concurrencia |
| HU-025 | PASS: motivo, rechazo, ocultamiento, motivo al solicitante |
| HU-026 | PASS: estado propio sin indicadores internos, aislamiento |
| HU-028 | PASS: pin privado, etiqueta, otro usuario no lo ve, aprobación lo retira |
| OCR real imágenes | PASS: Tesseract 5.4.0 local, `spa+eng` (español instalado) |
| OCR real PDF escaneado | PASS: Poppler `pdftoppm` + Tesseract `spa+eng` |
| Miniatura PDF real | PASS: rasterización Poppler → JPEG |
| Rotación AES | PASS: lectura v1 con clave v2, rechazo de versión incorrecta |
| Gitleaks local | PASS: historial completo (125 commits) y árbol de trabajo, 0 leaks |
| Dependencias vulnerables | PASS local (`dotnet list package --vulnerable --include-transitive`: 0 paquetes vulnerables) |
| Stryker.NET | **PASS informativo**: 59.63 %; 493 evaluados, 294 killed, 158 survived, 41 NoCoverage, 28 CompileError (safe mode), 0 timeout/errores de ejecución |
| Mongo real | **BLOCKED**: no hay `mongod` local; Docker prohibido |
| Docker build | **BLOCKED**: no se ejecutó Docker |
| ZAP/RESTler/Sonar/CodeQL/Semgrep remotos | **PENDING_CI** |

### Deuda de Sprint 4 atendida

1. **HU-021:** cerrada con las transiciones Pendiente→EnRevision→Aprobado/Rechazado y las pruebas de decisión.
2. **Stryker:** se revisaron los sobrevivientes, se añadieron pruebas de decisión,
   comparación, estado propio, mapa y perfil; el score subió de 48.55 % a 59.63 %.
   La diferencia entre el porcentaje y el conteo se explica en la sección 7.
3. **OCR de PDF escaneado:** se integró Poppler `pdftoppm` local, sin pago ni
   servicio externo; la prueba real rasteriza un PDF generado sin capa de texto y
   Tesseract `spa+eng` extrae el nombre.
4. **Vista previa PDF:** la miniatura administrativa ahora es un JPEG de la primera
   página rasterizada; si Poppler falta, se devuelve el PDF original y se informa
   la limitación.
5. **Criptografía:** AES-256-GCM mantiene nonce/tag/AD; la autorización documental
   sigue exigiendo admin + enlace HMAC. Se añadió rotación con `keyVersion`,
   `CurrentKeyVersion` y `PreviousEncryptionKeys`; la rotación operativa no se
   ejecutó sobre datos reales.
6. **Idioma Tesseract:** configuración `spa+eng`, con `tesseract-ocr-spa` en Docker
   y CI; las pruebas reales locales se ejecutaron con español instalado.
7. **MongoDB aislado:** no hay `mongod` local y Docker está prohibido; la prueba
   real y la nueva decisión persistida quedan **BLOCKED** hasta el job autorizado.
8. **Controles remotos:** ZAP, RESTler, Sonar, CodeQL, Semgrep y Pact siguen
   **PENDING_CI/PENDING_CONTRACTS**; no se simularon como PASS.

## 7. Stryker.NET

Se amplió el alcance a `Verification/**`, `SubmitFirstPublication`,
`MyPublicationMap` y `GetProfileService`. Ejecución local final (17-nov-2026):
**59.63 %**, 493 mutantes evaluados, 294 killed, 158 survived, 41 NoCoverage, 73
Ignored y 28 CompileError tratados por el safe mode de Stryker; 0 timeout y 0
errores de ejecución. El reporte JSON contiene 594 mutantes. El score incluye
`NoCoverage` en el denominador y excluye `CompileError`/`Ignored`; no se bajó el
umbral (`break=0`). Los 28 CompileError corresponden a dos métodos no cubiertos
(`AddUnitPhotosService.AddAsync` y `ListFavoritesService.ListAsync`) y se
reportan sin ocultarlos; el job no los declara como mutaciones evaluadas.

## 8. DevSecOps preparado

- OpenAPI: rutas de decisión, estado propio y mapa privado.
- ZAP: catálogo de 41 operaciones; el flujo real usa dos cuentas sintéticas para
  aprobar (HU-024) y rechazar con motivo (HU-025), consulta el estado propio
  (HU-026), el pin privado (HU-028) y la exploración pública tras la aprobación.
- RESTler: catálogo de 41 operaciones; los endpoints administrativos usan el
  token administrativo real y las rutas sin fixture pueden quedar `WARN`, no PASS.
- CI MongoDB: `M06-001` + pruebas M06; sin nueva migración.
- OCR workflow: instala Tesseract y `poppler-utils`, ejecuta OCR de imagen y PDF
  escaneado; falla si hay omisiones.
- Stryker: alcance ampliado; reporte manual.
- Semgrep, Gitleaks, CodeQL, SonarCloud, Pact, Docker/Dockle/Container Smoke se
  conservan. No se ejecutaron workflows remotos.
- Sin cambios en Azure, ACR, Container Apps, Atlas, Redis o Cloudinary.

## 9. Pendientes por infraestructura/autorización

1. `M06-001` no aplicado a Atlas; reconciliar validador/índices antes de promover.
2. Pruebas Mongo reales requieren `mongod` aislado o el job `mongo-db-test`.
3. Docker build/Container Smoke no ejecutados localmente.
4. ZAP, RESTler, Sonar, CodeQL, Semgrep, Pact y Stryker remoto: PENDING_CI.
5. Notificaciones push, M12 y M13 Usuarios no forman parte de Sprint 5.
6. UI Android, navegación de cuatro accesos y menú Rentas son cliente.
7. Retención/anonimización ADR-005 y rotación operativa de claves siguen sujetas a
   autorización.
8. No declarar producción preparada: persisten BLOCKED/PENDING_CI.

## 10. Matriz de las 56 HU de Sprints 1–5

`PASS local` significa que la suite local cubre el contrato backend; no implica
validación CI ni despliegue. `Parcial` indica dependencia de M09/M13/UI.

### Sprint 1 (11)

| HU | Módulo | Implementación | Validación local |
|---|---|---|---|
| HU-001 | M01 | Implementada | PASS local |
| HU-002 | M02 | Implementada | PASS local |
| HU-003 | M01 | Implementada | PASS local |
| HU-004 | M01 | Implementada | PASS local |
| HU-005 | M01 | Implementada | PASS local |
| HU-006 | M01 | Implementada | PASS local |
| HU-008 | M02 | Soporte API; `vinculadoAUnidad` depende de M09 | Parcial |
| HU-011 | M13 | Panel admin parcial; Usuarios es M13 Sprint 8 | Parcial |
| HU-012 | M01 | Implementada | PASS local |
| HU-075 | M03 | Implementada | PASS local |
| HU-077 | M03 | Implementada | PASS local |

### Sprint 2 (13)

| HU | Módulo | Implementación | Validación local |
|---|---|---|---|
| HU-010 | M02 | Soporte API/UI con `puedePublicar` | PASS local |
| HU-014 | M05 | Implementada | PASS local |
| HU-029 | M05 | Implementada | PASS local |
| HU-030 | M05 | Implementada | PASS local |
| HU-031 | M05 | Implementada | PASS local |
| HU-032 | M05 | Implementada (Cloudinary free) | PASS local / opt-in pendiente |
| HU-078 | M05 | Implementada | PASS local |
| HU-079 | M05 | Implementada | PASS local |
| HU-080 | M05 | Implementada | PASS local |
| HU-081 | M05 | Implementada | PASS local |
| HU-082 | M05 | Implementada | PASS local |
| HU-083 | M05 | Implementada | PASS local |
| HU-091 | M05 | Implementada | PASS local |

### Sprint 3 (15)

| HU | Módulo | Implementación | Validación local |
|---|---|---|---|
| HU-007 | M03 | Implementada | PASS local |
| HU-037 | M04 | Implementada | PASS local |
| HU-038 | M04 | Implementada | PASS local |
| HU-039 | M04 | Implementada | PASS local |
| HU-040 | M04 | Implementada | PASS local |
| HU-041 | M04 | Implementada | PASS local |
| HU-042 | M04 | Implementada | PASS local |
| HU-043 | M04 | Implementada | PASS local |
| HU-044 | M04 | Implementada | PASS local |
| HU-045 | M04 | Implementada | PASS local |
| HU-046 | M04 | Implementada: `arrendadorVerificado` desde M06 aprobado | PASS local |
| HU-047 | M04 | Implementada | PASS local |
| HU-076 | M04 | Solo UI; sin endpoint propio | UI Android |
| HU-084 | M04 | Implementada | PASS local |
| HU-085 | M07 | Implementada | PASS local |

### Sprint 4 (11)

| HU | Módulo | Implementación | Validación local |
|---|---|---|---|
| HU-013 | M05/M06 | Implementada | PASS local |
| HU-015 | M06 | Implementada (imagen/PDF, OCR real) | PASS local |
| HU-016 | M06 | Implementada (imagen y PDF) | PASS local |
| HU-017 | M06 | Implementada | PASS local |
| HU-018 | M06 | Implementada (OCR imagen/PDF escaneado) | PASS local |
| HU-020 | M06 | Implementada (ELA/metadatos) | PASS local |
| HU-021 | M06 | **Completa en Sprint 5** | PASS local |
| HU-022 | M06 | Implementada | PASS local |
| HU-023 | M06 | Implementada | PASS local |
| HU-027 | M06/M04 | Implementada | PASS local |
| HU-088 | M06 | Implementada (AES, aviso, URL temporal) | PASS local |

### Sprint 5 (6)

| HU | Módulo | Implementación | Validación local |
|---|---|---|---|
| HU-009 | M02 | Implementada para navegación; `/mis-propiedades` es Sprint 6 | PASS local |
| HU-019 | M06 | Implementada | PASS local |
| HU-024 | M06 | Implementada | PASS local |
| HU-025 | M06 | Implementada | PASS local |
| HU-026 | M06 | Implementada | PASS local |
| HU-028 | M05/M04 | Implementada | PASS local |

Total: **56 HU** (11 + 13 + 15 + 11 + 6). No se incluyen HU de Sprint 6+.

## 11. Commit manual

No se ejecutó ningún commit. Para revisar y confirmar:

```powershell
cd C:\Users\Admin\Proyectos\UniHome\unihome-backend-sprint05
git status --short
git diff --check
git diff --stat
git add -A
git commit -m "feat(sprint-05): implementar dictamen y navegacion M02-M06"
git log -1 --oneline

# Publicar únicamente la rama de Sprint 5 y crear un solo PR.
git push -u origin feature/sprint-05-backend-integral
gh pr create `
  --base main `
  --head feature/sprint-05-backend-integral `
  --title "Sprint 5: dictamen M06, navegación M02/M05 y DevSecOps" `
  --body "Integra Sprint 4 de main con las seis HU de Sprint 5. Incluye OpenAPI 41, DAST 41/41 preparado, Mongo CI 8/8 preparado, OCR real spa+eng/Poppler y pruebas locales documentadas en docs/sprint-05-cierre.md."
```

No ejecutar `git push`, PR, merge ni despliegue hasta autorización. El
`stash@{0}: sprint5-wip-before-main-sprint4` debe conservarse hasta que el PR
esté revisado; no usar `git stash drop` ni `git stash pop` en esta tarea.

## 12. Recomendación de publicación

1. Publicar primero Sprint 4 (`33d5d5e`, ya en `main`) en un entorno autorizado
   y aplicar `M06-001`.
2. Ejecutar `mongo-db-test`, ZAP, RESTler, OpenAPI y OCR workflow sobre ese
   incremento.
3. Publicar después Sprint 5, con `M06-001` ya presente y sin migración nueva.
4. Validar en Android las pantallas de Mis espacios, decisión y pin privado.
5. No promover a producción mientras queden BLOCKED/PENDING_CI críticos.

## 13. Preparación Azure/MongoDB para Sprint 5 (sin ejecutar)

Esta preparación es documental. No se autenticó Azure, no se escribió Atlas, no
se creó infraestructura, no se publicaron imágenes y no se modificaron secretos.

### Migraciones nuevas

- **No hay migración nueva en Sprint 5.** `M06-001` conserva su checksum y sigue
  siendo la única migración de M06.
- Los campos `resultadoComparacionNombres`, `comparacionNombresActualizadaAt` y
  `documentos_verificacion.keyVersion` usan el esquema flexible de colecciones
  existentes. Los campos `decision` y `motivoRechazo` ya existían en Sprint 4.
- Antes de promover, ejecutar `plan`/`verify` de `M06-001` contra la base
  autorizada y reconciliar el validador/índices de la colección externa
  `expedientes_verificacion`. El migrador falla cerrado ante divergencias.
- Si Sprint 4 aún no está aplicado, el orden es `M01-001`, `M01-002`,
  `M03-001`, `M05-001`, `M06-001`, `M07-001`. No saltar `M06-001`.

### Respaldo y promoción autorizada

1. Confirmar ventana, responsable y autorización humana; no ejecutar en horario
   de producción sin aprobación.
2. Tomar un backup coherente de Atlas (snapshot o `mongodump` autorizado) antes
   de la ventana. Verificar que el backup termina y registrar su identificador,
   sin copiar cadenas de conexión ni credenciales al repositorio.
3. Ejecutar `verify` de todas las migraciones previas y de `M06-001`.
4. Aplicar/promover solo a la base de verificación autorizada; `promote` no se
   ejecuta en este repositorio ni en CI. No se promueve `unihome_desarrollo`
   desde una sesión sin autorización.
5. Repetir `verify` y comprobar `escrituras pendientes: 0`, checksum e índices.
6. Si la verificación falla, detener la ventana y restaurar el backup según el
   procedimiento autorizado; no improvisar escrituras correctivas.

### Configuración y secretos requeridos

- `DocumentSecurity__EncryptionKey`: Base64 de 32 bytes, obligatoria fuera de
  Development. No rotar en la misma ventana del primer despliegue salvo
  autorización expresa.
- `DocumentSecurity__CurrentKeyVersion=1` y
  `DocumentSecurity__PreviousEncryptionKeys=[]` en el primer despliegue. Para
  rotar, conservar la clave anterior y subir la versión de forma coordinada.
- `DocumentSecurity__UrlSigningKey` y `DocumentSecurity__PublicBaseUrl` son
  opcionales; la URL pública debe ser HTTPS absoluta si se configura.
- `Admin__AccountIds` debe contener la cuenta administrativa autorizada; una
  lista vacía deniega `/admin/*`.
- Revisar sin exponer valores: `Jwt__SigningKey`, `Jwt__Issuer`,
  `Jwt__Audience`, `VerificationCode__HmacSecret`, `Auth__RateLimit__IpHashSalt`,
  `Redis__ConnectionString`/`Redis__RestUrl` + `Redis__RestToken`,
  `Cloudinary__CloudName`/`ApiKey`/`ApiSecret`, SMTP y
  `PasswordRecovery__ResetLinkBaseUrl`.
- `Api__EnableSwagger` debe permanecer en `false` en producción hasta
  autorización explícita.

### Compatibilidad con Sprint 4 desplegado

- Los endpoints nuevos son aditivos: un cliente Sprint 4 sigue funcionando sin
  cambios.
- Los campos nuevos de `expedientes_verificacion` son opcionales; un documento
  anterior sin `keyVersion` se interpreta como versión 1 y sin comparación como
  `NoDisponible`.
- `PerfilResponse` añade `estadoPrimeraPublicacion`; los clientes que no lo leen
  lo ignoran.
- La decisión no cambia el contrato de carga de documentos ni el de exploración;
  solo añade transiciones y campos de respuesta.
- No se requiere reescribir documentos cifrados ni ejecutar una migración de
  datos para Sprint 5.

### Procedimiento CD preparado

1. Ejecutar CI completo sobre el commit candidato: build, pruebas, OpenAPI,
   Mongo CI, OCR, DAST 41/41, RESTler, Docker Build, Dockle, Container Smoke,
   Quality Gate y Hardening Report.
2. Construir la imagen con etiqueta por SHA y registrar el digest; no usar
   `latest`.
3. Ejecutar `deploy-azure` solo por `workflow_dispatch` desde `main`, con
   `deploy_to_azure=true`, `UNIHOME_ENABLE_AZURE_CD=true`, environment
   `azure-dev` y todos los gates en verde.
4. Verificar `/health/live` y `/health/ready`, la revisión `Running`/`Healthy` y
   que el FQDN responde por HTTPS.
5. No crear recursos nuevos ni aumentar SKU, réplicas o costos. El
   despliegue usa la infraestructura existente.

### Pruebas HTTPS de aceptación (entorno autorizado, no producción)

- `GET https://<fqdn>/health/live` y `/health/ready` deben devolver `200` y
  `Healthy`.
- Una petición sin token a un endpoint protegido debe devolver `401`; una cuenta
  no administrativa a `/api/v1/admin/expedientes` debe devolver `403`.
- Con cuentas sintéticas autorizadas: cargar documentos, enviar la publicación,
  consultar `GET /api/v1/expedientes/mi-expediente` y
  `GET /api/v1/mi-publicacion/estado-mapa`; aprobar y rechazar desde una cuenta
  administrativa sintética; comprobar que la unidad aprobada aparece en M04 y
  que la rechazada no.
- Verificar que el enlace documental expira y que no se puede reutilizar sin
  sesión administrativa. No usar documentos personales reales.
- Comprobar que Swagger sigue deshabilitado salvo autorización.

### Rollback seguro

- El rollback de aplicación consiste en volver a la revisión/imagen anterior ya
  auditada y verificar salud y FQDN. No se elimina la imagen anterior durante la
  ventana.
- Los campos nuevos son aditivos: volver a Sprint 4 no requiere revertir
  `M06-001` ni borrar datos. Los expedientes decididos y la auditoría se
  conservan.
- No ejecutar un rollback de base de datos como rutina. Solo ante corrupción o
  una migración fallida, restaurar el backup autorizado y documentar el punto de
  recuperación.
- Si la aprobación de una unidad queda inconsistente, usar el procedimiento de
  compensación del servicio y revisar la auditoría; no editar documentos
  cifrados ni el ledger a mano.
