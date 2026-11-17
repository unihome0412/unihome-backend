# Matriz de aceptación HTTPS — Sprints 1 a 5

**Alcance:** 41 operaciones OpenAPI de los Sprints 1–5 y los controles
transversales de sesión, autorización por recurso, documentos y Swagger.
**Datos:** únicamente sintéticos. No se usan JWT productivos, credenciales
reales, cuentas administrativas reales ni escrituras en Atlas.

## 1. Regla de evidencia

- `PASS local` significa que la suite automatizada del repositorio ejerce el
  contrato con datos sintéticos y dobles en memoria; no sustituye una prueba
  HTTPS contra un entorno desplegado.
- `PENDIENTE_AUTORIZACIÓN` significa que la prueba requiere una sesión legítima,
  una cuenta administrativa autorizada, correo de captura o una ventana
  aprobada. No se declara aprobada sin ejecutarla.
- Las pruebas negativas esperadas (`400`, `401`, `403`, `404`, `409`, `410`,
  `413`, `415`, `422`, `429`, `503`) no se convierten artificialmente en `2xx`.
- La colección Postman/Newman
  [`tests/acceptance/unihome-sprints-1-5.postman_collection.json`](../tests/acceptance/unihome-sprints-1-5.postman_collection.json)
  está parametrizada y no contiene secretos.

## 2. Variables de la colección

| Variable | Uso | No debe contener |
|---|---|---|
| `baseUrl` | URL HTTPS del entorno autorizado | Una URL productiva hardcodeada |
| `userEmail` / `userPassword` | Cuenta sintética confirmada | Credenciales reales |
| `confirmationCode` | Código capturado del buzón de pruebas | Código productivo |
| `resetToken` | Token del enlace capturado | Token productivo |
| `accessToken` / `refreshToken` | Sesión sintética; se guardan al ejecutar login | Tokens productivos |
| `adminAccessToken` | Sesión de la cuenta administrativa autorizada | JWT productivo en Git |
| `universityId` / `propertyId` / `unitId` / `photoRef` | Recursos sintéticos | IDs de datos reales |
| `expedienteId` / `documentToken` | Expediente y enlace sintéticos | Documentos reales |
| `photoPath` / `inePath` / `proofPath` | Rutas locales a archivos de prueba | Archivos de personas reales |

## 3. Matriz de las 41 operaciones

`Auth` = Bearer obligatorio; `Admin` = policy administrativa existente;
`Público` = sin Bearer. La columna `Evidencia local` remite a la suite que
ejecuta el contrato; `HTTPS` queda pendiente hasta la ventana autorizada.

| # | Método | Ruta | Auth | Éxito | Negativos esperados | Evidencia local | HTTPS |
|---:|---|---|---|---|---|---|---|
| 1 | POST | `/api/v1/auth/registro` | Público | 201 | 400, 409, 413, 503 | `RegisterEndpointTests`, `OpenApiGenderContractTests` | PENDIENTE_AUTORIZACIÓN |
| 2 | POST | `/api/v1/auth/confirmacion-correo` | Público | 200 | 400, 410, 413, 429, 503 | `ConfirmEmailEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 3 | POST | `/api/v1/auth/confirmacion-correo/reenvio` | Público | 202 | 400, 413, 429, 503 | `ResendConfirmationEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 4 | POST | `/api/v1/auth/login` | Público | 200 | 400, 401, 403, 413, 429, 503 | `LoginEndpointTests`, `AuthRateLimitTests` | PENDIENTE_AUTORIZACIÓN |
| 5 | POST | `/api/v1/auth/refresh` | Público | 200 | 400, 401, 413, 429, 503 | `RefreshEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 6 | POST | `/api/v1/auth/logout` | Auth | 204 | 401, 413, 503 | `LogoutEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 7 | POST | `/api/v1/auth/recuperacion/solicitud` | Público | 200 | 400, 413, 429, 503 | `PasswordRecoveryEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 8 | POST | `/api/v1/auth/recuperacion/confirmacion` | Público | 200 | 400, 410, 413, 429, 503 | `PasswordRecoveryEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 9 | DELETE | `/api/v1/cuentas/mi-cuenta` | Auth | 204 | 400, 401, 413, 503 | `DeleteAccountEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 10 | GET | `/api/v1/perfil/mi-perfil` | Auth | 200 | 401, 503 | `ProfileEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 11 | POST | `/api/v1/propiedades` | Auth | 201 | 400, 401, 403, 409, 413, 503 | `PublicationEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 12 | POST | `/api/v1/propiedades/{propertyId}/unidades` | Auth | 201 | 400, 401, 403, 404, 413, 503 | `PublicationEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 13 | POST | `/api/v1/unidades/{unitId}/fotos` | Auth | 200 | 400, 401, 403, 404, 409, 413, 415, 503 | `UnitPhotoEndpointTests`, `OpenApiMultipartContractTests` | PENDIENTE_AUTORIZACIÓN |
| 14 | GET | `/api/v1/fotos/{photoRef}` | Público/condicional | 200 | 401, 404, 503 | `UnitPhotoEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 15 | PUT | `/api/v1/unidades/{unitId}/servicios` | Auth | 200 | 400, 401, 403, 404, 413, 503 | `PublicationEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 16 | PUT | `/api/v1/unidades/{unitId}/zona-reglas` | Auth | 200 | 400, 401, 403, 404, 413, 503 | `PublicationEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 17 | GET | `/api/v1/unidades/{unitId}/vista-previa` | Auth | 200 | 401, 403, 404, 503 | `PublicationEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 18 | POST | `/api/v1/publicaciones/primera/terminos` | Auth | 200 | 400, 401, 403, 413, 422, 503 | `PublicationEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 19 | POST | `/api/v1/publicaciones/primera/enviar` | Auth | 200 | 401, 403, 404, 409, 413, 422, 503 | `PublicationEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 20 | GET | `/api/v1/mi-publicacion/estado-mapa` | Auth | 200 | 401, 404, 422, 503 | `MyPublicationMapServiceTests`, integración M06 | PENDIENTE_AUTORIZACIÓN |
| 21 | GET | `/api/v1/exploracion/mapa` | Público | 200 | 400, 404, 503 | `ExplorationEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 22 | GET | `/api/v1/exploracion/lista` | Público/Bearer opcional | 200 | 400, 404, 503 | `ExplorationEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 23 | GET | `/api/v1/unidades/{unitId}/tarjeta` | Público/Bearer opcional | 200 | 404, 503 | `ExplorationEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 24 | GET | `/api/v1/unidades/{unitId}/ficha` | Auth | 200 | 401, 404, 503 | `ExplorationEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 25 | PUT | `/api/v1/favoritos/{unitId}` | Auth | 204 | 401, 404, 503 | `ExplorationEndpointTests`, `MongoExplorationPersistenceTests` | PENDIENTE_AUTORIZACIÓN |
| 26 | DELETE | `/api/v1/favoritos/{unitId}` | Auth | 204 | 401, 503 | `ExplorationEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 27 | GET | `/api/v1/favoritos` | Auth | 200 | 400, 401, 503 | `ExplorationEndpointTests`, `MongoExplorationPersistenceTests` | PENDIENTE_AUTORIZACIÓN |
| 28 | GET | `/api/v1/universidades` | Público | 200 | 400, 503 | `AdminUniversitiesEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 29 | POST | `/api/v1/admin/universidades` | Admin | 201 | 400, 401, 403, 409, 413, 503 | `AdminUniversitiesEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 30 | GET | `/api/v1/admin/universidades` | Admin | 200 | 400, 401, 403, 503 | `AdminUniversitiesEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 31 | PUT | `/api/v1/admin/universidades/{id}` | Admin | 200 | 400, 401, 403, 404, 409, 413, 503 | `AdminUniversitiesEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 32 | PUT | `/api/v1/admin/universidades/{id}/estado` | Admin | 200 | 401, 403, 404, 413, 503 | `AdminUniversitiesEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 33 | GET | `/api/v1/expedientes/mi-expediente` | Auth | 200 | 401, 404, 503 | `VerificationEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 34 | POST | `/api/v1/expedientes/mi-expediente/ine` | Auth | 200 | 400, 401, 403, 404, 409, 413, 415, 422, 429, 503 | `VerificationEndpointTests`, OCR real | PENDIENTE_AUTORIZACIÓN |
| 35 | POST | `/api/v1/expedientes/mi-expediente/comprobante` | Auth | 200 | 400, 401, 403, 404, 409, 413, 415, 422, 429, 503 | `VerificationEndpointTests`, OCR real | PENDIENTE_AUTORIZACIÓN |
| 36 | GET | `/api/v1/admin/expedientes` | Admin | 200 | 400, 401, 403, 503 | `VerificationEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 37 | GET | `/api/v1/admin/expedientes/{id}` | Admin | 200 | 401, 403, 404, 503 | `VerificationEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 38 | GET | `/api/v1/admin/expedientes/{id}/documentos/{tipo}` | Admin | 200 | 401, 403, 404, 503 | `VerificationEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 39 | GET | `/api/v1/expedientes/documentos/{token}` | Admin | 200 | 401, 403, 404, 503 | `VerificationEndpointTests` | PENDIENTE_AUTORIZACIÓN |
| 40 | POST | `/api/v1/admin/expedientes/{id}/aprobar` | Admin | 200 | 401, 403, 404, 409, 413, 503 | `VerificationEndpointTests`, `DecideExpedienteServiceTests` | PENDIENTE_AUTORIZACIÓN |
| 41 | POST | `/api/v1/admin/expedientes/{id}/rechazar` | Admin | 200 | 400, 401, 403, 404, 409, 413, 503 | `VerificationEndpointTests`, `DecideExpedienteServiceTests` | PENDIENTE_AUTORIZACIÓN |

## 4. Controles transversales

| Control | Condición esperada | Evidencia |
|---|---|---|
| Swagger/OpenAPI deshabilitado | `/swagger`, `/swagger/login`, `/openapi/v1.json` y `/` responden 404 | `SwaggerUiTests`, `SwaggerAccessTests` |
| Swagger/OpenAPI habilitado sin sesión | `/swagger` redirige a `/swagger/login`; OpenAPI y assets no se sirven sin sesión | `SwaggerAccessTests`, `SwaggerBrowserLoginTests` |
| Login documental en navegador | Formulario público, CSRF, credenciales existentes y cookie HttpOnly; sin JWT en HTML/JS | `SwaggerBrowserLoginTests` |
| Login documental sobre HTTP directo | `400` sin cookie ni token; solo HTTPS lógico del ingress confiable emite CSRF | `SwaggerBrowserLoginTests`, `SwaggerForwardedHeadersTests` |
| Usuario normal | 403 en Swagger/OpenAPI; login documental rechazado con error genérico | `SwaggerAccessTests`, `SwaggerBrowserLoginTests` |
| Lector documental | 200 en Swagger/OpenAPI; 403 en `/api/v1/admin/*` | `SwaggerAccessTests`, `SwaggerBrowserLoginTests` |
| Administrador con Bearer | 200 en JSON/UI y `Set-Cookie` `__Host-...` con `Secure`, `HttpOnly`, `SameSite=Strict` | `SwaggerAccessTests` |
| Cookie de documentación | 200 en Swagger/OpenAPI; 401 en `/api/*` | `SwaggerAccessTests`, `SwaggerBrowserLoginTests` |
| Logout y CSRF | `POST /swagger/logout` con token; sin token 400; cookie revocada | `SwaggerBrowserLoginTests` |
| Cookie manipulada, expirada o cuenta suspendida | 401 | `SwaggerAccessTests`, `SwaggerBrowserLoginTests` |
| Cache de documentación | `Cache-Control: no-store`, `X-Robots-Tag: noindex` | `SwaggerAccessTests` |
| HSTS | `max-age=31536000; includeSubDomains`, sin preload, con HTTPS real o `X-Forwarded-Proto: https` | `ForwardedHeadersTests` |
| Cabeceras de seguridad | `nosniff`, `DENY`, `no-referrer`, `Permissions-Policy`, CSP | `ProblemDetailsTests`, `ForwardedHeadersTests` |
| Aislamiento por recurso | 403/404 sin filtrar existencia; favoritos por cuenta | `ExplorationEndpointTests`, `VerificationEndpointTests` |
| Rate limiting y Redis | 429 por ventana; 503 si Redis falla; readiness 503 | `AuthRateLimitTests`, `HealthReadinessTests`, `DistributedRedisIntegrationTests` |
| Documentos cifrados | AES-256-GCM, rotación, enlace HMAC temporal y auditoría | `DocumentCryptographyTests`, `VerificationEndpointTests` |
| OCR español/PDF | Tesseract `spa+eng`, Poppler y fallback `NoDisponible` | `RealOcrTests`, `RealScannedPdfTests`, workflow OCR |
| M07/favoritos | Colección e índices presentes; si faltan, 503 | `MongoExplorationPersistenceTests`, `mongo-db-test` |

## 5. Ejecución HTTPS autorizada

1. Crear una cuenta sintética confirmada y una cuenta administrativa
   autorizada en el entorno de aceptación. No reutilizar cuentas productivas.
2. Configurar la colección Postman con `baseUrl` HTTPS y las variables
   anteriores. No guardar tokens en Git ni en capturas.
3. Ejecutar en este orden: salud y cabeceras, registro/confirmación/login,
   perfil, universidades, propiedad/unidad/fotos, términos y expediente,
   aprobación/rechazo, exploración/favoritos y Swagger.
4. Capturar el `traceId` de cualquier `5xx`; no se considera aceptado.
5. Ejecutar `newman run` contra el entorno autorizado:

```powershell
newman run tests/acceptance/unihome-sprints-1-5.postman_collection.json `
  --environment <ruta-al-environment-local.json> `
  --reporters cli,json --reporter-json-export TestResults/acceptance.json
```

6. Limpiar los datos sintéticos con los endpoints de borrado y los
   procedimientos autorizados del entorno. No borrar datos de terceros.

## 6. Estado de esta intervención

| Evidencia | Estado |
|---|---|
| Restore locked, build Release, formato | PASS local |
| Unitarias .NET | PASS local |
| Integración .NET (dobles en memoria) | PASS local |
| Contratos OpenAPI, Swagger y autorización | PASS local |
| OCR/PDF real en Linux | PASS local / CI pendiente de ejecución |
| MongoDB real, M07 y favoritos | Preparado; `mongo-db-test` PENDIENTE_CI |
| DAST ZAP, RESTler, Sonar, CodeQL, Semgrep, Stryker, Docker/Dockle | Preparado; ejecución remota PENDIENTE_AUTORIZACIÓN |
| Aceptación HTTPS autenticada | PENDIENTE_AUTORIZACIÓN: no se usaron credenciales reales |
| Cloudinary real | PENDIENTE de credenciales dedicadas |
| Pact | PENDIENTE de contratos reales del consumidor Android |

No se declara aprobada la aceptación autenticada remota. La matriz local y la
colección permiten ejecutarla cuando exista una sesión autorizada y una ventana
aprobada.

## 7. Evidencia de solo lectura antes del despliegue

El 17 de noviembre de 2026 se consultó la URL HTTPS desplegada sin credenciales
ni escrituras:

| Ruta | Resultado observado | Interpretación |
|---|---|---|
| `/health/live` | `200` | Proceso vivo. |
| `/health/ready` | `200` | MongoDB/Redis reportados disponibles. |
| `/api/v1/universidades?buscar=Uni` | `200` | Catálogo público responde. |
| `/openapi/v1.json` | `404` | Swagger desactivado en la revisión. |
| `/swagger/index.html` | `404` | Swagger desactivado en la revisión. |
| Cabecera HSTS | Ausente | Hallazgo confirmado; el código ya emite HSTS con HTTPS o `X-Forwarded-Proto: https`, pendiente de desplegar y verificar. |

No se ejecutaron login, registro, recuperación, cargas ni decisiones
administrativas contra producción. Esas pruebas requieren una cuenta sintética
autorizada y una ventana aprobada; la colección de esta carpeta queda preparada
para ese momento.

## 8. Aceptación en navegador (Brave/Chrome)

Con `Api__EnableSwagger=true` y al menos una cuenta en `Admin__AccountIds` o
`DocumentationAccess__AccountIds`:

1. Abrir `https://<fqdn>/swagger`. Sin cookie, el navegador debe redirigir a
   `/swagger/login`.
2. Confirmar que `/openapi/v1.json` no se sirve antes del login (`401`) y que
   `/swagger/index.html` redirige al formulario.
3. Introducir correo y contraseña de una cuenta autorizada. El formulario usa
   CSRF y el limitador de login; no devuelve JWT ni lo guarda en el navegador.
4. Tras el login, comprobar que Swagger UI carga y que `/openapi/v1.json`
   responde `200`. La cookie `__Host-unihome-swagger` debe ser HttpOnly, Secure,
   SameSite=Strict y no debe servir en `/api/*`.
5. Un usuario normal debe recibir un error genérico de login; un lector
   documental no debe poder abrir `/api/v1/admin/*`.
6. Abrir el enlace "Cerrar sesión" de Swagger, confirmar el formulario y
   comprobar que `/swagger` vuelve a redirigir al login.
7. Repetir con ventana privada para verificar que no hay sesión residual.
