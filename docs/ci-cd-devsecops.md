# UniHome Backend — Pipeline DevSecOps

Referencia de diseño: grafo de trabajos GitHub Actions compartido el 6 de noviembre de 2026. Adaptado a **ASP.NET Core 10 y MongoDB Atlas**. Esta entrega reemplaza el **contenido** de `.github/workflows/ci.yml`, conserva `.github/workflows/codeql.yml` y `.github/dependabot.yml`, y no crea otro workflow paralelo que duplicaría builds.

## Qué funciona al integrarlo

| Job | Qué verifica | Cuándo |
|---|---|---|
| Detect changes | Detecta cambios relevantes mediante Git | Todos los PR y push a main, manual |
| Semgrep SAST | Reglas `p/security-audit` de C#/seguridad | Código y CI modificados |
| Build & Test | Restore locked, build Release, formato, unit/integration local y test del gate | Código y CI modificados |
| Dependency scan | NuGet High/Critical, preserva configuración existente | Siempre |
| Secret scan | Gitleaks, preserva el análisis del historial existente | Siempre |
| Contract Test (OpenAPI) | Tests existentes de OpenAPI y Swagger | Código y CI; después de build |
| PR Quality Gate | Analiza realmente los resultados; **skipped no es passed** | Todos los runs, aunque fallen anteriores |
| Hardening Report | Tabla Markdown y artefacto con resultados reales | Todos los runs salvo cancelación |

El análisis **CodeQL existente** sigue ejecutándose de manera independiente en `.github/workflows/codeql.yml`; para proteger `main`, configurar **ambos** estados requeridos (CodeQL y el gate DevSecOps). Las pruebas de integración que necesitan MongoDB están protegidas con un opt-in en el proyecto y se omiten por defecto en las pruebas locales: el pipeline registra dicho `skipped` de forma honesta. El job manual de MongoDB CI no acepta una omisión como aprobación.

## Trabajos visibles pero desactivados por defecto

Estos trabajos aparecen como `skipped` en el grafo hasta configurar las variables indicadas. **No se consideran pruebas superadas**.

| Job | Variable de repositorio | Otro requisito | Evento permitido |
|---|---|---|---|
| SonarCloud SAST | `UNIHOME_ENABLE_SONAR=true` | Secret `SONAR_TOKEN` y vars `SONAR_PROJECT_KEY`, `SONAR_ORGANIZATION`; proyecto Sonar | PR interno a main, push main o manual desde main |
| Database Test (MongoDB efímero) | `UNIHOME_ENABLE_MONGO_CI=true` | MongoDB Community 8.0.34 fijado por digest como servicio temporal del runner; base **exclusiva `unihome_ci_verificacion`**, inicializada con M01-001, M01-002, M03-001, M05-001, M06-001 y M07-001 en el propio job; sin Atlas, secretos ni credenciales | Solo manual |
| Mutation Test (Stryker.NET) | `UNIHOME_ENABLE_MUTATION=true` | Stryker.NET 5.0.0 local desde `dotnet-tools.json`; configuración en `tests/UniHome.UnitTests/stryker-config.json`; reportes HTML/JSON | Solo manual |
| Cloudinary Integration | `UNIHOME_ENABLE_CLOUDINARY_INTEGRATION=true` | Environment protegido `cloudinary-integration` y secretos dedicados `CLOUDINARY_INTEGRATION_CLOUD_NAME`, `CLOUDINARY_INTEGRATION_API_KEY`, `CLOUDINARY_INTEGRATION_API_SECRET`; el test real sube un PNG sintético y limpia el asset en `finally` | Solo manual y con aprobación |
| Docker Build + Dockle + Container Smoke | `UNIHOME_ENABLE_DOCKER=true` | Aprobar uso de Docker en CI, cuotas de acciones/artefactos; **no** publica imagen. El smoke arranca la imagen real con MongoDB efímero y configuración de Production, exige `/health/live` y `/health/ready` en 200, HSTS con `X-Forwarded-Proto: https`, `/openapi/v1.json` no disponible con Swagger desactivado, proceso vivo y ausencia de exit 139/errores fatales | PR, push main, manual |
| DAST ZAP | `UNIHOME_ENABLE_ZAP=true` | API efímera en loopback con MongoDB 8.0.34 y SMTP de captura; descubre OpenAPI, ejecuta rutas reales y flujo autenticado; ZAP **baseline pasivo**; reportes HTML/JSON/Markdown | Manual desde `main` o `feature/*` |
| Pact | `UNIHOME_ENABLE_PACT=true` | Verificador de proveedor PactNet 5.0.1 con API Kestrel aislada; requiere contratos JSON reales del consumidor Android en `pacts/`; sin ellos **falla** y permanece desactivado | Solo manual |
| RESTler | `UNIHOME_ENABLE_RESTLER=true` | RESTler `v9.3.1` fijado por commit; API/MongoDB/SMTP efímeros, Compile + Test + Fuzz-lean con límites; cobertura y bug buckets | Manual desde `main` o `feature/*` |
| OCR documental M06 | No usa variable: `workflow_dispatch` es la autorización | Workflow separado `.github/workflows/ocr-documental.yml`; instala `tesseract-ocr`/`tesseract-ocr-eng`/`tesseract-ocr-spa`/`poppler-utils` desde apt en el runner, ejecuta las pruebas reales de imagen y PDF escaneado y el flujo HTTP real, exige al menos 5 pruebas ejecutadas, 0 omitidas y 0 fallidas; publica TRX | Solo manual |

El Container Smoke levanta un stub HTTPS local de Upstash en `127.0.0.1` para
que `/health/ready` evalúe Redis sin usar el servicio real ni desactivar la
comprobación. El certificado es efímero, se monta como CA de confianza en el
contenedor y el proceso se detiene en el paso de limpieza. No se usan secretos
reales ni red externa.

**Ninguno de estos trabajos usa `unihome_desarrollo`, ejecuta deploy ni escribe en Azure.** El trabajo MongoDB no se conecta a Atlas: usa un contenedor efímero del runner, sin credenciales y con la URI restringida a loopback. Las migraciones `M01-001`, `M01-002`, `M03-001`, `M05-001`, `M06-001` y `M07-001` se aplican únicamente a `unihome_ci_verificacion`; no se ejecuta `promote` ni se escribe en bases compartidas.

### DAST ZAP baseline pasivo

El job `dast-zap` ya no se limita a `/health/live`. Su implementación actual:

- Usa la imagen oficial `ghcr.io/zaproxy/zaproxy:2.17.0` fijada por digest
  `sha256:9624669db72f54d86c4b46a7064518bd9bf79d8c61a4c31befa9923489f328af`.
- Levanta una API efímera en `http://127.0.0.1:5088`, un MongoDB Community
  8.0.34 fijado por digest como servicio del runner y un SMTP de captura local
  con STARTTLS en `127.0.0.1:587`. La base `unihome_dast` se inicializa con
  M01-001, M01-002, M03-001, M05-001, M06-001 y M07-001 y se destruye al final. No usa Atlas, Brevo, Azure,
  puertos públicos ni credenciales reales. La CA efímera del SMTP no publica CRL/OCSP, por lo
  que solo en este job se configura `Smtp__CheckCertificateRevocation=false`;
  el valor predeterminado del código sigue siendo `true` y STARTTLS sigue
  siendo obligatorio.
- Crea dos cuentas sintéticas mediante los endpoints reales: registro,
  confirmación con el código capturado del SMTP, login, refresh, logout y
  eliminación de cuenta. Las claves JWT/HMAC y la contraseña SMTP son valores
  sintéticos del runner; los tokens no se publican en artefactos.
- Inicia sesión con el administrador sintético y descarga `/openapi/v1.json` con
  Bearer. Antes comprueba que `/openapi/v1.json` y `/swagger/index.html`
  responden `401` sin sesión; después visita Swagger UI y `/` con la sesión
  administrativa. La tabla `route-coverage.json` registra método, ruta,
  autenticación requerida, códigos de respuesta y ruta visitada. Una operación
  nueva en OpenAPI que no tenga tráfico hace fallar el job.
- Envía el tráfico a través del proxy de ZAP y ejecuta únicamente análisis
  **pasivo** (`pscan`). No se ejecuta Active Scan ni Full Scan, no se usa `-I`,
  no se aplican listas de exclusión y no se desactivan reglas pasivas.
- Genera `zap-report.html`, `zap-report.json`, `zap-report.md` (si el add-on lo
  permite), `dast-report.md`, `route-coverage.json`, `security-headers.json` y
  las evidencias de arranque/limpieza. High/Critical hacen fallar el job;
  Medium produce `WARN`; un fallo técnico o una ruta no visitada produce
  `ERROR`. El artefacto se publica como `dast-zap-results`.
- Se exige en `workflow_dispatch` desde `main` o `feature/*` cuando
  `UNIHOME_ENABLE_ZAP=true`, hay cambios de código y `build-test` aprobó. En
  PR/push no se exige y el Hardening Report lo muestra como `skipped` no
  aprobado. El gate de Hardening aplica la misma regla de rama.

**Verificación histórica (10 de noviembre de 2026):** ejecución manual
[42772939746](https://github.com/unihome0412/unihome-backend/actions/runs/42772939746),
commit `01893d3`, job `dast-zap` en verde con estado **PASS**, 7/7 operaciones
OpenAPI de ese momento visitadas, 26 mensajes procesados por ZAP, 0 Critical,
0 High, 0 Medium, 0 Low y 2 informativas. Se publicaron `zap-report.html`,
`zap-report.json`, `zap-report.md`, `dast-report.md`, `route-coverage.json` y
evidencias de arranque/limpieza.

**Sprint 4:** el catálogo OpenAPI pasó a **37 operaciones**. El fixture DAST
ejecuta el flujo M06 real: carga INE y comprobante por multipart, envía la
primera publicación, consulta bandeja/detalle administrativo y obtiene y
consume el enlace firmado del documento. Las 7 rutas M04/M07 siguen usando un
fixture público sembrado **solo** en `unihome_dast`. La ejecución #280 detectó
30/37 rutas porque el fixture aún no cubría M06; la corrección quedó preparada y
la ejecución 37/37 permanece **PENDIENTE_CI**. No se declara PASS anticipado.

**Sprint 5:** el catálogo OpenAPI pasa a **41 operaciones**. El fixture DAST
ejecuta el flujo M06 completo: una cuenta carga INE y comprobante, envía la
publicación, consulta su estado propio y su pin privado, y el administrador
aprueba con el endpoint real (HU-024). Una segunda cuenta repite el flujo y el
administrador la rechaza con motivo (HU-025); el solicitante consulta el estado
y el motivo (HU-026), y su pin privado se visita antes de la decisión (HU-028).
La exploración M04 se valida después de la aprobación real, sin escribir
directamente en MongoDB. Las 7 rutas M04/M07 conservan su fixture público en
`unihome_dast`. La ejecución real 41/41 queda **PENDIENTE_CI** y no se declara
PASS anticipado.

**Alcance pendiente y riesgos:** el análisis pasivo no demuestra lógica de
negocio ni autorización a nivel de objeto; la cobertura se limita a las rutas
documentadas y las rutas de soporte enumeradas. El HTTP de loopback es
intencional y no representa el despliegue; HSTS/HTTPS no aplica a este runner.
Las alertas que aparezcan en futuras ejecuciones deben revisarse y aceptarse o
corregirse por separado. RESTler / Pact siguen requiriendo contratos y datos de
prueba; no se activan para aparentar cobertura.

### RESTler fuzzing aislado

El job `restler-fuzzing` usa Microsoft RESTler **v9.3.1**, commit
`eaacd92733771d35e90acfb2040fe37d4e5d09b1`, clonado y compilado desde la fuente
en el runner. No se usa una imagen móvil ni un servicio remoto.

- Reutiliza la API ASP.NET Core en `127.0.0.1:5088`, MongoDB 8.0.34 efímero con
  M01-001, M01-002, M03-001, M05-001, M06-001 y M07-001, y el SMTP de captura local con STARTTLS en
  `127.0.0.1:587`.
- Prepara dos cuentas sintéticas: una confirmada para login/refresh y otra
  pendiente de confirmar para ejercer el endpoint con un código real capturado
  del SMTP. El token provider de RESTler crea una cuenta nueva y hace
  registro, confirmación y login reales en cada refresco.
- El OpenAPI 3.1 original se conserva. Para compatibilidad con RESTler se genera
  una copia OpenAPI 3.0.3 que convierte tipos union `null` a `nullable`;
  `openapi-transformation.json` registra cada cambio y verifica que las rutas y
  métodos sean idénticos. No se elimina ningún endpoint ni se oculta un error
  del contrato. La preparación aborta si el POST de fotos no declara
  `multipart/form-data`, `Fotos` como array de binarios y su codificación
  repetible; no se inventa tráfico ni se omite esa operación. Los 15 enums
  públicos se validan como string.
- Ejecuta **Compile**, **Test/Smoke** y **Fuzz-lean**. No se usa `fuzz` ni
  fuzzing exhaustivo. Límites: `max_combinations=2..3`, `max_sequence_length=10`,
  `max_request_execution_time=20s`, timeout de shell por etapa, sin checkers
  desactivados. En la instancia efímera se elevan los rate limits de
  autenticación para que el fuzzer no quede bloqueado por 429; la autenticación
  y los endpoints siguen siendo los reales.
- Los diccionarios por endpoint aportan valores válidos a registro, confirmación,
  reenvío, login, refresh y eliminación; la autenticación de rutas protegidas se
  realiza con tokens Bearer emitidos por la API real.
- Los artefactos incluyen compilación, gramática/config, cobertura `speccov`,
  `testing_summary`, solicitudes y secuencias, `runSummary`/`errorBuckets`,
  bug buckets, replay logs saneados y resumen Markdown. Se excluyen del
  artefacto los diccionarios privados y los settings con secretos.
- El job falla si Compile/Test/FuzzLean no producen resultados, si falta una
  operación OpenAPI, si un endpoint autenticado no se ejerce o si aparece un
  HTTP 5xx/bug bucket. Un 401/403 no se considera vulnerabilidad por sí mismo,
  pero tampoco se cuenta como prueba funcional válida.

**Verificación histórica (10 de noviembre de 2026):** ejecución manual
[42794026111](https://github.com/unihome0412/unihome-backend/actions/runs/42794026111),
commit `fe22365`, job `restler-fuzzing` en verde con estado **PASS**:
Compile/Test/Fuzz-lean exit `0`, 7/7 operaciones OpenAPI de ese momento
intentadas y con al menos una respuesta 2xx, 2.054 solicitudes y 16 secuencias
observadas, 0 HTTP 5xx y 0 bug buckets.

**Sprint 5:** el catálogo ahora tiene **41 operaciones**. La preparación crea una
universidad, propiedad y unidad sintéticas, publica la unidad solo en la base
efímera `unihome_restler` y añade diccionarios para `universidadId`, `unitId` y
los filtros de exploración. También siembra por HTTP expedientes M06 reales:
uno `EnRevision` para detalle/documento/mapa privado, uno `Borrador` para el
envío, y dos independientes para aprobar y rechazar; los IDs y el token firmado
se pasan como diccionarios privados. Los expedientes del administrador se crean
**antes** de la unidad pública, porque `SubmitFirstPublicationService`
selecciona la unidad más antigua del titular; además, la unidad pública
`Aprobada` pertenece a una cuenta sintética distinta. Crear primero una unidad
pública bajo el administrador hacía que el envío respondiera `409`. Las cargas
multipart que RESTler no puede representar y las operaciones cuyo contrato exige
rechazo se reportan como WARN, nunca como PASS. El reporte ahora cuenta **respuestas 2xx reales**; no confunde
el flag `valid` de RESTler con éxito funcional. La ejecución #280 terminó con
37/37 operaciones intentadas, 28/37 con 2xx, 0 HTTP 5xx y 0 bug buckets; con el
criterio anterior se reportó PASS, pero el criterio corregido la clasifica como
**WARN** porque 9 operaciones no obtuvieron 2xx. La cobertura funcional M06 con
2xx se valida además en DAST y en las pruebas de integración. **Sprint 5** añade cuatro operaciones hasta llegar a 41;
la ejecución real 41/41 queda **PENDIENTE_CI** y no se declara PASS anticipado.

### OCR real M06

El job obligatorio `build-test` instala `tesseract-ocr`, `tesseract-ocr-eng`,
`tesseract-ocr-spa`, `poppler-utils` y `fonts-dejavu-core` en Ubuntu antes de
ejecutar la suite. Así `RealOcrTests`, `RealScannedPdfTests` y
`RealOcrVerificationEndpointTests` ejecutan Tesseract/Poppler reales, incluidos
un documento sintético en español con acentos y eñe y un PDF escaneado sin capa
de texto, en cada PR/push; no dependen de que el workflow separado ya exista en
`main`. El job exige al menos 5 pruebas OCR/PDF ejecutadas y ninguna omitida.

El workflow `.github/workflows/ocr-documental.yml` no forma parte del gate
obligatorio y no se ejecuta en PR/push. Se activa solo con `workflow_dispatch` y
es la evidencia de que el OCR no está simulado:

- Instala `tesseract-ocr`, `tesseract-ocr-eng`, `tesseract-ocr-spa`, `poppler-utils`
  y `fonts-dejavu-core` desde los repositorios de la imagen Ubuntu del runner; no
  usa servicios, API keys ni modelos remotos.
- Ejecuta `RealOcrTests` y `RealScannedPdfTests` (motor real, PDF escaneado con
  Poppler y caso de motor ausente) y `RealOcrVerificationEndpointTests` (multipart
  HTTP → análisis → expediente administrativo) con TRX. La prueba de imagen cubre
  español con acentos y el caso de motor ausente.
- Falla si se ejecutan menos de 5 pruebas reales, si alguna se omite o si alguna
  falla. Un runner sin Tesseract no se considera aprobado para OCR.
- La imagen de la API instala el mismo motor y los modelos español/inglés en el `Dockerfile`; si en un entorno falta el binario, el adaptador marca `NoDisponible` y el nombre queda nulo, sin inventar resultados.

**Estado local (17 de noviembre de 2026):** `RealOcrTests` 3/3,
`RealScannedPdfTests` 2/2 y `RealOcrVerificationEndpointTests` 1/1 aprobadas con
Tesseract 5.4.0/Poppler en Windows, 0 omitidas. La ejecución del workflow en
GitHub Actions queda **PENDIENTE_CI**.

### Contrato OpenAPI de Gender

El contrato HTTP de M01 usa `JsonStringEnumConverter(allowIntegerValues: false)`:
`Gender` se envía como `"Mujer"`, `"Hombre"` o `"PrefiereNoDecir"` y los enteros
se rechazan. El generador nativo de OpenAPI 3.1.1 describía el componente como
`integer`; el documento ahora lo corrige a `type: string` con `enum` de nombres.
`OpenApiGenderContractTests` comprueba el documento generado y contrasta una
solicitud real con string (201) y con entero (400). No cambia el comportamiento
HTTP ni se modificó el DTO para ocultar la discrepancia. Verificación histórica:
[42850339456](https://github.com/unihome0412/unihome-backend/actions/runs/42850339456),
`contract-test` con 10/10 pruebas OpenAPI aprobadas. **Sprint 3** añade pruebas
de las 7 rutas M04/M07, query camelCase, `universidadId` requerido y enums
string; el contrato actual queda **PENDIENTE_CI** hasta la ejecución de GitHub
Actions.

### Pact de proveedor (pendiente de contratos reales)

- El repositorio móvil `unihome-mobile` (commit inspeccionado
  `6355763da7d9659412fd117a1d22aca73be53a84`) no contiene contratos Pact al
  momento de esta entrega; su README no publica pruebas de consumidor ni
  artefactos Pact.
- `tests/UniHome.PactTests` prepara la verificación del proveedor con PactNet
  `5.0.1`. No está incluido en `UniHome.Backend.slnx` para que el build/test
  normal no dependa de una entrega inexistente.
- El verificador usa PactNet contra una API UniHome en ejecución sobre
  Kestrel/TCP (no `WebApplicationFactory`/TestServer) y verifica archivos Pact
  reales del consumidor Android.
- Entradas: `PACT_CONTRACTS_DIR` (por defecto `pacts`) con JSON Pact generados
  por las pruebas del cliente HTTP Android, no derivados de OpenAPI;
  `PACT_PROVIDER_BASE_URL` (por defecto loopback `5088`) y, si se usan
  `providerStates`, `PACT_PROVIDER_STATE_URL`.
- El job `pact-contract` sigue condicionado a `UNIHOME_ENABLE_PACT=true` y
  `workflow_dispatch`. Exige contratos reales y una API aislada disponible; si
  falta cualquiera de los dos, falla explícitamente y no se convierte en
  `success`. Los demás jobs no dependen de Pact. El arranque de la instancia
  aislada del proveedor se incorporará junto con la entrega real de contratos.
- Formato, origen y mecanismo de entrega: [`pacts/README.md`](../pacts/README.md).
  Estado actual: **no hay contratos reales y Pact no está aprobado**.

## SonarQube Cloud

- **PR interno hacia `main`**: se ejecuta análisis real de Pull Request. `SonarScanner for .NET` detecta automáticamente los metadatos desde el evento de GitHub Actions; no se pasan `sonar.branch.name` ni parámetros de rama manuales. El checkout usa `fetch-depth: 0`.
- **Push a `main` y `workflow_dispatch` desde `main`**: se ejecuta análisis de la rama principal.
- **`workflow_dispatch` desde una rama feature**: Sonar no se ejecuta y el gate no lo exige, porque el plan Free no admite análisis de ramas no principales. Stryker puede seguir ejecutándose en su evento manual.
- **PR desde fork**: el job no se ejecuta porque GitHub no expone `SONAR_TOKEN` a esos PR; el gate falla cerrado si Sonar está habilitado y el job quedó `skipped`. No se usa `pull_request_target`.
- **Cobertura para el Quality Gate**: el job de Sonar instala Tesseract `spa+eng` y Poppler antes de ejecutar las pruebas, para que las pruebas OCR/PDF reales no se omitan y su cobertura cuente. Ejecuta las pruebas unitarias y de integración .NET con `XPlat Code Coverage` (Cobertura), además de las pruebas Python de `tools/ci` con `coverage.py`. Los reportes se pasan al scanner; no se desactiva la condición de cobertura nueva.
- Se mantiene `sonar.qualitygate.wait=true`: si el Quality Gate real falla, el job de Sonar falla y bloquea el PR.

## Mutation testing (Stryker.NET)

- Herramienta local fijada en `dotnet-tools.json` (`dotnet-stryker` 5.0.0); no se instala una versión global variable.
- Configuración versionada: `tests/UniHome.UnitTests/stryker-config.json`. Muta
  login, recuperación, refresh, borrado de cuenta, favoritos, fotos, propiedades,
  publicaciones, universidades y verificación; el detalle exacto está en
  `mutate`.
- Ejecución local reproducible:

```bash
dotnet tool restore
cd tests/UniHome.UnitTests
dotnet stryker --config-file stryker-config.json
```

- Reportes: `tests/UniHome.UnitTests/StrykerOutput/<timestamp>/reports/mutation-report.html` y `mutation-report.json`. No se versionan.
- El job manual `mutation-test` restaura la herramienta desde el manifiesto, ejecuta pruebas unitarias, corre Stryker y publica ambos reportes. Falla si no hay mutaciones evaluadas, si hay errores de ejecución o si faltan los reportes.
- Línea base histórica de Login: 57 mutantes en el archivo objetivo, 46 evaluados, 44 detectados, 2 sobrevivientes en `ConfigureAwait(false)` y mutation score de 95.65 %.
- **Sprint 5 + saneamiento:** el alcance de mutación cubre login, recuperación,
  refresh, borrado de cuenta, favoritos, fotos, servicios, zona/reglas,
  propiedades, términos, publicación, mapa privado, perfil y `Verification/**`.
  Ejecución local del 17 de noviembre de 2026: 507 killed, 236 survived, 84
  NoCoverage, 165 CompileError (safe mode), 124 ignorados, 0 timeout, 0
  errores de ejecución y mutation score **61.31 %**. El umbral pasa a
  `break=60` (high 80, low 60 se conservan) y `stryker_summary.py
  --minimum-score 60` falla si el score baja. Los 236 sobrevivientes se reportan
  sin ocultarlos; los `ConfigureAwait` y mutaciones equivalentes no se eliminan
  del reporte para no inflar la métrica. Los servicios nuevos se cubren además
  con pruebas de integración, que Stryker no usa en esta configuración.

## MongoDB CI efímero

- Job manual `mongo-db-test`, condicionado por `UNIHOME_ENABLE_MONGO_CI=true`.
- Servicio de GitHub Actions con MongoDB Community **8.0.34**, fijado por digest
  `sha256:b9924c330a8bbc5800fe9c69f04a84e221eca7b28fd049a0354147d9908fa142`.
  No se usa `latest`, Atlas ni Docker local.
- La URI es `mongodb://127.0.0.1:27017`, directa, sin credenciales. El workflow
  rechaza hosts no loopback, SRV, credenciales y una base distinta de
  `unihome_ci_verificacion`. Las pruebas también aplican esta comprobación cuando
  `UNIHOME_MONGO_CI_LOOPBACK_ONLY=1`.
- No se configura replica set: las pruebas de identidad y bloqueo usan índices y
  operaciones atómicas sobre un documento; no requieren transacciones.
- Se espera la disponibilidad real del contenedor, se aplican `M01-001`, `M01-002`,
  `M03-001` y `M05-001` con `UniHome.MongoMigrator` y se verifican ledger,
  checksum e índices antes de las pruebas.
- `MongoIdentityPersistenceTests`, `MongoPasswordResetPersistenceTests`,
  `MongoUniversityPersistenceTests`, `MongoPublicationPersistenceTests`, `MongoExplorationPersistenceTests` y
  `MongoPromotionLockPersistenceTests` se ejecutan contra MongoDB real; el correo
  se sustituye por un doble en memoria y las fotos usan el doble de DEC-04. No se
  envían correos ni se escribe almacenamiento externo real.
- Un script valida el TRX: exige las ocho pruebas (incluidas las dos de M06), rechaza `NotExecuted`, falla
  si hay cero resultados, si la inicialización no terminó o si el paso de pruebas
  no fue exitoso. El resumen incluye versión, base, conteos, duración,
  inicialización y resultado final.
- Evidencia: `mongodb-ci-results` con el TRX, la salida de migración/verificación
  y el resumen Markdown. El contenedor se destruye al finalizar el job y la base
  temporal se elimina explícitamente.
- Verificación histórica (13 de noviembre de 2026):
  [ejecución manual 43395389178](https://github.com/unihome0412/unihome-backend/actions/runs/43395389178),
  commit de código `f51ac10`, job `mongo-db-test` en verde, TRX con 4 pruebas
  ejecutadas y aprobadas, 0 fallidas, 0 omitidas y limpieza confirmada.
- **Sprint 4/5:** el job aplica también M01-002, M06-001 y M07-001 y exige las
  pruebas M06 de documentos cifrados y decisión administrativa. La siguiente
  ejecución real debe mostrar **8/8 pruebas** (identidad, promoción,
  recuperación, universidades, publicaciones, verificación documental, decisión
  y exploración/favoritos); queda **PENDIENTE_CI**.

## CD Azure preparado (sin despliegue)

El job `deploy-azure` existe pero está desactivado por defecto. Solo puede
ejecutarse en `workflow_dispatch` desde `main`, con el input explícito
`deploy_to_azure=true`, `UNIHOME_ENABLE_AZURE_CD=true`, environment `azure-dev`
y todos los gates previos en verde. Autentica por OIDC, publica la imagen ya
auditada por Dockle con etiqueta SHA y digest registrado, actualiza Container
Apps, exige estado `Running`, verifica `/health/live` y `/health/ready` y
revierte a la imagen anterior si falla. CI y CD usan grupos de concurrencia
separados; un push no cancela un deploy en progreso. ACR no se declara inmutable:
el tag por SHA y el digest son evidencia, no una garantía de bloqueo de tags.

La auditoría del 2026-11-10 no pudo confirmar recursos por falta de sesión de
Azure CLI; la URL pública respondió `200`, el environment tiene branch policy
`main` pero no required reviewers, y no se configuró la variable de activación.
Permisos OIDC propuestos, riesgos, FinOps y los pasos de autorización están en
[`azure-cd-preparacion.md`](azure-cd-preparacion.md). No se creó ni modificó
ningún recurso Azure, no se publicaron imágenes y no se reiniciaron revisiones.
Las ejecuciones manuales `42864581083` (input `false`) y PR `42864050099`
terminaron en verde y mostraron `deploy-azure` como `skipped`; la PR #18 no
tiene ningún check de despliegue ejecutado. El flujo CD todavía **no se probó
en Azure**.

## Flujo

```text
pull_request -> main / push -> main / ejecución manual
                    |
              Detect Changes
             /      |        \
      Semgrep   Build & Test   Escaneos de dependencias / secretos
                    |
            Contract Test OpenAPI
                    |
            (Pact, opcional)

En paralelo, con habilitación explícita:
  SonarCloud / Mongo CI / Stryker / Docker Build -> Dockle -> Container Smoke
  OWASP ZAP local / RESTler aislado

                        |
                  PR Quality Gate
                        |
                 Hardening Report
```

Las pruebas activadas en un contexto admisible pasan a ser **obligatorias** para el gate. Un job opcional que se omite cuando se ha habilitado en ese evento **bloquea la integración**. El workflow usa `if: !cancelled()` en los dos últimos jobs para que no desaparezca el reporte cuando falla una prueba.

## Instalación y verificaciones

1. Guardar el paquete fuera del repositorio. Ejecutar `instalar-ci.ps1` desde PowerShell 7. Hace un preflight del árbol Git limpio y del workflow original y crea `feature/ci-devsecops`, con respaldo externo.
2. Revisar `git diff`, crear un commit de trabajo propio y subir **solo** la rama de CI para un PR hacia `main`.
3. Revisar el primer run. No declarar que ha pasado hasta ver **Build**, **Semgrep**, **OpenAPI**, **Dependency**, **Secret** y **PR Quality Gate** en verde.
4. En GitHub > Settings > Rules > Rulesets, configurar revisión requerida, checks requeridos `PR Quality Gate (controles obligatorios)` y `CodeQL - análisis C#`; validar los **nombres exactos** que GitHub muestre, porque pueden depender del workflow/repo.
5. No activar `UNIHOME_ENABLE_AZURE_CD` ni ejecutar el job `deploy-azure` todavía. Para el primer despliegue deben existir aprobadores en `azure-dev`, identidad federada y roles verificados, presupuesto/alertas revisados y la autorización humana descrita en `azure-cd-preparacion.md`.

## Advertencias

- Esta entrega se validó sintácticamente (YAML), con pruebas de la lógica del quality gate y comprobación de archivos y referencias. La integración de Stryker.NET se validó localmente con .NET 10 y una ejecución real de mutaciones; el primer run de GitHub Actions sigue siendo la validación del pipeline completo.
- Semgrep usa reglas externas y una versión de CLI instalada al ejecutar: para un pipeline reproducible de producción habrá que fijar versiones/digests y comprobar la licencia/costos aplicables.
- Dockle usa un binario oficial descargado de GitHub; fijar y validar su checksum antes de exigirlo en producción.
- Pact tiene soporte de proveedor preparado, pero no hay contratos reales del consumidor Android; el job debe permanecer desactivado y no se declara aprobado. RESTler ya fue verificado con ejecución real. Stryker.NET tiene configuración versionada y línea base local verificada. MongoDB CI fue verificado en la ejecución manual `43395389178`: job `mongo-db-test` en verde, 4/4 pruebas reales, 0 omitidas, migraciones M01-001, M03-001, M05-001 y M07-001 válidas y artefacto `mongodb-ci-results`; la base temporal y el contenedor se destruyeron al finalizar. **Sprint 4** añade `M06-001` y `MongoVerificationPersistenceTests`; esas pruebas y el workflow `ocr-documental` quedan **PENDIENTE_CI** hasta una ejecución autorizada.
- Si el CI que actualmente está en GitHub fue modificado **después** del ZIP del 6 de noviembre, no aplicar el paquete a ciegas: reconciliar las diferencias antes.
- Conservar `.local-sdd/` únicamente en local. `AGENTS.md`, `opencode.json`, `memoria.md` del SDD tampoco deben subir. La documentación técnica de CI en este paquete sí puede versionarse.
