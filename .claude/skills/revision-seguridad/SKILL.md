---
name: revision-seguridad
description: "Checklist real usado antes de pedir merge de un endpoint sensible (admin, datos personales, dinero, documentos) o de un cambio de seguridad: OWASP API Top 10 por operación, secretos, cabeceras, CORS, rate limiting y evidencia en specs/. Usar antes de abrir o aprobar un PR que toque autenticación, autorización, datos personales o infraestructura."
---

## Cuándo usar esto

Antes de pedir el merge de cualquier PR que toque: autenticación/sesión,
`/admin/*`, datos personales, pagos informativos, documentos de verificación,
CORS, cabeceras de seguridad, rate limiting, o infraestructura (Bicep,
Container Apps, secretos). Para el flujo de construir el endpoint en sí, ver
la skill `nuevo-endpoint`; esta skill es la revisión posterior, no sustituye
las pruebas que ya exige esa skill.

## Paso 1 — Revisión automática (siempre, sin excepción)

No sustituye la lectura manual, pero se corre primero:

- `/security-review` (skill integrada de Claude Code) sobre el diff.
- CI en verde: CodeQL ×2 (C#/Python), Semgrep, Gitleaks, SonarCloud (0 avisos
  nuevos, 0 *security hotspots* sin revisar, cobertura de línea nueva
  &gt;80 %) y el PR Quality Gate — ver `docs/ci-cd-devsecops.md`.
- Si el PR toca una ruta `/api/v1/*`: confirmar que sigue apareciendo en
  `/openapi/v1.json` con el método y la seguridad esperados
  (`OpenApiTests`) y que `SpecsCoverageTests` sigue en verde.

## Paso 2 — OWASP API Top 10, por operación tocada

Para cada operación nueva o modificada, revisar y dejar evidencia en su
archivo de `specs/<modulo>/*.md` (sección "Controles de seguridad
aplicados"):

1. **API1 BOLA/IDOR:** ¿el handler usa el `accountId`/`unitId` del claim de
   sesión y del recurso cargado, o confía en un id que manda el cliente para
   decidir pertenencia? Agregar una prueba que intente acceder/editar un
   recurso de otra cuenta y espere `403`/`404`, nunca `200`.
2. **API2 autenticación rota:** ¿la ruta nueva realmente exige `[Authorize]`
   si debía? Revisar el documento OpenAPI real (`security: [Bearer]`), no
   solo el atributo en el código — el precedente de `Memoria.md` ("El 503
   del schema guard") muestra que el orden de evaluación puede enmascarar un
   `401` faltante.
3. **API3 asignación masiva de propiedades:** ¿el DTO de request expone
   campos que el dominio no debería aceptar del cliente (`activa`, `rol`,
   `estado`, ids internos)? Los contratos de este proyecto son `record`
   explícitos por diseño (nunca bind directo a la entidad de dominio);
   confirmar que sigue así.
4. **API5 autorización por función:** para cualquier ruta `/admin/*`,
   confirmar que usa la policy `Admin` (`AdminAccessOptions`) revalidada en
   servidor contra `Admin__AccountIds`, nunca un claim/rol que mande el
   cliente. Agregar o confirmar una prueba "con claim admin falso → 403"
   (precedente: `Crear_ConClaimAdminFalso_DebeResponder403` en
   `AdminUniversitiesEndpointTests`).
5. **API4 consumo de recursos:** límite de tamaño de cuerpo
   (`RequestSizeLimit`), paginación acotada (`pageSize` con máximo), y rate
   limiting si la ruta es de alto volumen o sensible a fuerza bruta
   (`AuthRateLimitPolicies`, `Auth:RateLimit:*`).
6. **API8 mala configuración de seguridad:** cabeceras (`X-Content-Type-Options`,
   `X-Frame-Options`, `Content-Security-Policy`, `Cache-Control: no-store` en
   respuestas con datos propios/personales — ver el middleware global en
   `Program.cs` y confirmar que el prefijo de la ruta nueva está en la lista,
   o que el controlador llama `SetNoStore()` explícitamente) y CORS
   (`Cors:AllowedOrigins`, nunca `*` con credenciales).

## Paso 3 — Secretos y configuración

- `git diff` contra secretos antes de cualquier commit: ninguna cadena de
  conexión, contraseña, clave HMAC/JWT/AES o token real en código, logs,
  `appsettings*.json`, commits o este chat. Precedente de lo que pasa si se
  omite: "La credencial expuesta en `scratch/`" en `Memoria.md`.
- Confirmar que `.gitleaks.toml`/`.gitleaksignore` siguen vigentes para el
  patrón del incidente anterior (URIs `mongodb(+srv)://usuario:contraseña@`,
  etiquetas de imagen ACR) antes de ampliarlos con una regla nueva.
- Nunca correr `dotnet user-secrets` ni imprimir su contenido desde un
  asistente de IA (candado de `.claude/settings.local.json`).

## Paso 4 — Pendientes conocidos (no repetir la pregunta, solo verificar si ya se cerraron)

Antes de dar por cerrada la revisión, confirmar en `README.md` y
`docs/auditoria-backend.md` (cuando exista) el estado de: `DEC-09`
(detalle del `409` de registro duplicado), `DEC-10`, `ADR-005` (retención/
anonimización al eliminar cuenta), autosuspensión/autoeliminación de un
administrador, rate limiting en registro, y `ForwardedHeaders` para que el
rate limit por IP funcione detrás del ingress. Si alguno sigue abierto, no
inventar que se resolvió: documentarlo como pendiente con su riesgo.

## Paso 5 — Registrar el resultado

Si la revisión encuentra un hallazgo, corregirlo en la misma rama (o abrir
uno específico si no es del alcance del PR actual) y dejar constancia en
`docs/auditoria-backend.md` o en el cierre de sprint correspondiente — nunca
silenciarlo ni marcarlo resuelto sin la prueba que lo demuestre.
