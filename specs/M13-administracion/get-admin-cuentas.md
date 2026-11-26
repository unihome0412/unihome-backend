# `GET /api/v1/admin/cuentas`

| Campo | Valor |
|---|---|
| Módulo | M13 – Administración de cuentas |
| HU | HU-073 |
| Operación OpenAPI | `GET /api/v1/admin/cuentas` |
| Controlador | `src/UniHome.Api/Controllers/AdminCuentasController.cs` → `Search` (EP-M13-02) |

## Autenticación y autorización

`[Authorize(Policy = AdminAccessOptions.PolicyName)]` a nivel de clase:
toda la ruta `/api/v1/admin/cuentas` exige la policy **Admin**
(`AdminAccessHandler`, claim `sub` contra `Admin__AccountIds` server-side).
Sin sesión → `401`; con sesión sin privilegio → `403`.

## Request

Query (`BuscarCuentasQuery`): `buscar` (texto libre, nombre o correo),
`filtro` (`AccountSearchFilter`: `Todos` | `BuscanAlojamiento` |
`ConPublicaciones` | `EnRevision`), `page`, `pageSize`.

## Response

`200 OK` con `IReadOnlyList<AdminCuentaResponse>`. Cabeceras
`X-Total-Count`, `X-Page`, `X-Page-Size`.

## Validaciones

- Si `ModelState` no es válido (p. ej. `filtro` fuera del enum), `400`
  sobre el campo `filtro`.
- `page` entre 1 y `SearchAccountsService.MaxPage` (100 000); fuera de
  rango → `400`.
- `pageSize` entre 1 y `MaxPageSize` (100); fuera de rango → `400`.

## Códigos de error

| Código | Causa |
|---|---|
| 400 | `filtro` inválido o `page`/`pageSize` fuera de rango. |
| 401 | Sin sesión válida. |
| 403 | La cuenta no tiene privilegio administrativo. |
| 503 | Preflight de esquema Mongo fallido. |

## Reglas de negocio

- La categoría (`AccountLabel`) **reutiliza exactamente** la misma
  etiqueta que ve el propio titular en su perfil (HU-002/008/009):
  `UsuarioUniHome` (sin unidades propias / "Buscan alojamiento"),
  `ConEspaciosPublicados` (al menos una unidad `Aprobada` / "Con
  publicaciones") o `PublicacionEnRevision` (al menos una `EnRevision` y
  ninguna `Aprobada` / "En revisión").
- Como la categoría depende de las unidades de cada cuenta
  (`IUnitRepository.ListByOwnerAsync` por candidato), la paginación se
  aplica **después** de calcular la etiqueta de cada candidato, no en la
  consulta a Mongo — coste proporcional al número de candidatos del texto
  buscado, no de la base completa.
- No se agregó una migración nueva: reutiliza `cuentas` (`M01-001`); es
  una búsqueda de texto sin índice dedicado, mismo criterio que
  `lugaresOcupados` (Sprint 6).

## Controles de seguridad aplicados

- Autorización por función en `/admin/*` server-side, nunca por claim del
  cliente.
- `AdminCuentaResponse` nunca expone el hash de contraseña ni tokens de
  sesión (verificado en pruebas, ver abajo).
- Paginación acotada (`MaxPage`, `MaxPageSize`) como mitigación de
  consumo excesivo de recursos.

## Pruebas que la cubren

- Unitarias: `tests/UniHome.UnitTests/Application/SearchAccountsServiceTests.cs`
  (`SearchAsync_SinFiltro_DevuelveTodasLasCoincidencias`,
  `SearchAsync_FiltradoPorConPublicaciones_ExcluyeCuentasSinUnidadAprobada`,
  `SearchAsync_FiltradoPorEnRevision_SoloIncluyeCuentasConUnidadEnRevisionSinAprobadas`,
  `SearchAsync_PorTexto_SoloCoincideConNombreOCorreo`,
  `SearchAsync_Pagina_RespetaPageSize`,
  `SearchAsync_ConPaginaInvalida_DevuelveValidationFailed`,
  `SearchAsync_ConTamanoDePaginaInvalido_DevuelveValidationFailed`).
- Integración: `tests/UniHome.IntegrationTests/AdminCuentasEndpointTests.cs`
  (`Buscar_ComoAdmin_DebeEncontrarPorNombreOCorreoYPaginar`,
  `Buscar_FiltradoPorBuscanAlojamiento_DebeExcluirCuentasConPublicaciones`,
  `Buscar_ConFiltroInvalido_DebeResponder400`,
  `Buscar_SinPrivilegioAdministrativo_DebeResponder403`,
  `Buscar_SinSesion_DebeResponder401`,
  `Respuestas_NuncaExponenHashDeContrasena`).
- Seguridad: cubierto por el recorrido dinámico de ZAP sobre `/api/v1/*`
  y por la gramática RESTler del documento OpenAPI; la autorización por
  función (`403`/`401`) ya está cubierta explícitamente por las pruebas
  de integración citadas.
