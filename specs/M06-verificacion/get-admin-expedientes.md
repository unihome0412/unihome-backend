# `GET /api/v1/admin/expedientes`

| Campo | Valor |
|---|---|
| Módulo | M06 – Verificación documental |
| HU | HU-023 |
| Operación OpenAPI | `GET /api/v1/admin/expedientes` |
| Controlador | `src/UniHome.Api/Controllers/AdminVerificationController.cs` → `List` (EP-M06-04) |

## Autenticación y autorización

`[Authorize(Policy = AdminAccessOptions.PolicyName)]` a nivel de clase: toda
la ruta `/api/v1/admin/expedientes` exige la policy **Admin**, resuelta por
`AdminAccessHandler` comparando el claim `sub` del JWT validado contra
`Admin__AccountIds` configurado en servidor (`_options.Value.IsAdmin(subject)`).
No se acepta ningún rol o claim enviado por el cliente. Sin sesión → `401`;
con sesión pero sin privilegio → `403`.

## Request

Query: `estado` (`EstadoExpediente?`), `prioridadSospechosos` (bool,
default `true`), `page` (default 1), `pageSize` (default
`ListExpedientesService.DefaultPageSize` = 20, máximo 100).

## Response

`200 OK` con `IReadOnlyList<ExpedienteResumenResponse>`. Cabeceras
`X-Total-Count`, `X-Page`, `X-Page-Size` y `X-Pending-Review-Count`
(conteo de `Pendiente` + `EnRevision`).

## Validaciones

- `page` entre 1 y `MaxPage` (100 000); fuera de rango → `400`
  (`ValidationFailed`).
- `pageSize` entre 1 y `MaxPageSize` (100); fuera de rango → `400`.

## Códigos de error

| Código | Causa |
|---|---|
| 400 | `page`/`pageSize` fuera de rango. |
| 401 | Sin sesión válida. |
| 403 | La cuenta no tiene privilegio administrativo. |
| 503 | Preflight de esquema Mongo fallido. |

## Reglas de negocio

- Orden: si `prioridadSospechosos=true` (default), los expedientes
  marcados como sospechosos (`IsSuspicious`) aparecen primero.
- `X-Pending-Review-Count` incluye explícitamente `Pendiente` **y**
  `EnRevision`, no solo el filtro `estado` aplicado a la lista.
- Es la bandeja administrativa de primeras publicaciones (HU-023); no
  incluye detalle de OCR/riesgo (eso es `GetExpedienteDetailService`).

## Controles de seguridad aplicados

- Autorización por función en `/admin/*` vía policy server-side
  (`Admin__AccountIds`), nunca por claim de rol del cliente.
- Paginación acotada (`MaxPage`, `MaxPageSize`) como mitigación de consumo
  excesivo de recursos (OWASP API4:2023).
- No expone nombres de archivo, binario ni señales de riesgo en el
  resumen; esos campos solo aparecen en el detalle (`GET /{id}`).

## Pruebas que la cubren

- Unitarias: no se encontró un archivo `ListExpedientesServiceTests.cs`
  dedicado; la validación de paginación y orden se cubre indirectamente
  en la prueba de integración de bandeja (ver abajo).
- Integración: `tests/UniHome.IntegrationTests/VerificationEndpointTests.cs`
  → `IndicadorSospechoso_OrdenaPrimeroYExigeAdministrador`,
  `Bandeja_DebeValidarPaginacionYOrdenarSospechosos`.
- Seguridad: cubierto por el recorrido dinámico de ZAP sobre
  `/api/v1/*` y por la gramática RESTler del documento OpenAPI; no se
  encontró una prueba específica de autorización por función (403 sin
  privilegio) fuera de las pruebas de integración citadas arriba, que sí
  la ejercitan.
