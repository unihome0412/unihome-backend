# Sprint 3 — Cierre backend M04/M07

**Rama:** `feature/sprint-03-backend-integral`
**Base integrada:** `main` posterior al merge de PR #50
(`360838853023deab50cb38418c7f508aa3abcfdf`), que incluye Cloudinary,
Redis readiness/timeouts, M01-002 y los endurecimientos del Sprint 1/2.
**Alcance:** 15 HU, 43 puntos: HU-007, HU-037 a HU-047, HU-076, HU-084 y HU-085.
**No incluye:** M06 (aprobación/verificación), M09 (ocupación), M10, chat,
rentas, UI Android, Swagger público ni despliegue a Azure.

## Matriz de las 15 HU

| HU | RF | Puntos | Soporte backend | Endpoint / decisión | Pruebas |
|---|---|---:|---|---|---|
| HU-007 | RF-03 | 3 | Reutiliza M03 | `GET /api/v1/universidades?buscar=` ya existente; activas, máximo 10 | OpenAPI M03 + regresión |
| HU-037 | RF-18 | 5 | Implementado | `GET /api/v1/exploracion/mapa` y `/lista` | `ExplorationEndpointTests`, `ExplorationAndFavoritesServiceTests` |
| HU-038 | RF-18 | 3 | Implementado | `GET /api/v1/unidades/{unitId}/tarjeta` | Tarjeta pública + OpenAPI |
| HU-039 | RF-19 | 3 | Implementado | `distanciaMaxKm` en mapa/lista | Filtros combinados + Haversine |
| HU-040 | RF-19 | 3 | Implementado | `precioMaxMxn` en mapa/lista | Filtros combinados |
| HU-041 | RF-19 | 3 | Implementado | `soloIndividual` en mapa/lista | Filtros combinados |
| HU-042 | RF-19 | 2 | Implementado | `paraQuien` en mapa/lista | Filtros combinados |
| HU-043 | RF-20 | 2 | Implementado | Haversine y umbral 1.5 km; `tiempoTraslado` = `a pie`/`en transporte`, sin minutos inventados | `UmbralDeTraslado...`, tarjeta |
| HU-044 | RF-21 | 3 | Implementado con Cloudinary | `GET /api/v1/unidades/{unitId}/ficha`; galería y características | Ficha + OpenAPI |
| HU-045 | RF-21 | 3 | Implementado | Servicios incluidos y aparte con monto | Ficha + servicios |
| HU-046 | RF-21 | 3 | **Parcial por dependencia M06** | `arrendadorVerificado` permanece `false`; no se inventa verificación | Tarjeta/ficha/favoritos |
| HU-047 | RF-21 | 2 | Implementado | `paraQuien` en tarjeta y `tipoPropiedad` en ficha | Contrato OpenAPI + ficha |
| HU-076 | RF-40 | 2 | Solo UI | Sin endpoint propio; no se inventó backend de navegación | — |
| HU-084 | RF-40 | 3 | Implementado | Mapa y lista públicos sin Bearer | 200 anónimo + OpenAPI |
| HU-085 | RF-41 | 3 | Implementado | `PUT/DELETE /api/v1/favoritos/{unitId}` y `GET /api/v1/favoritos` | Unitarias, integración, Mongo real, aislamiento |

> HU-086 y HU-071 pertenecen a otros sprints, pero se aplican como controles
> transversales: ficha/favoritos responden 401 sin sesión y M04 nunca serializa
> correo ni teléfono del arrendador.

## Endpoints implementados

| ID | Método | Ruta | Auth | Éxito | Errores cubiertos |
|---|---|---|---|---|---|
| EP-M04-01 | GET | `/api/v1/exploracion/mapa` | Público | 200 | 400, 404, 503 |
| EP-M04-02 | GET | `/api/v1/exploracion/lista` | Público; Bearer opcional para `esFavorito` | 200 | 400, 404, 503 |
| EP-M04-03 | GET | `/api/v1/unidades/{unitId}/tarjeta` | Público; Bearer opcional para `esFavorito` | 200 | 404, 503 |
| EP-M04-04 | GET | `/api/v1/unidades/{unitId}/ficha` | Bearer | 200 | 401, 404, 503 |
| EP-M07-01 | PUT | `/api/v1/favoritos/{unitId}` | Bearer | 204 | 401, 404, 503 |
| EP-M07-02 | DELETE | `/api/v1/favoritos/{unitId}` | Bearer | 204 idempotente | 401, 503 |
| EP-M07-03 | GET | `/api/v1/favoritos?page=&pageSize=` | Bearer | 200 | 400, 401, 503 |

### Query de exploración

`universidadId` es obligatorio y se declara `required: true` en OpenAPI.
`distanciaMaxKm`, `precioMaxMxn`, `soloIndividual` y `paraQuien` son opcionales y
se combinan con AND lógico. `page`/`pageSize` solo aplican a la lista; el máximo
de página es 100. Los nombres del contrato son camelCase.

La lista devuelve `X-Total-Count`, `X-Page` y `X-Page-Size`. Cuando hay Bearer,
la respuesta se marca `Cache-Control: no-store` porque contiene `esFavorito`.

### `UnidadMapaResponse`

`id`, `latitud`, `longitud`, `precioMensualMxn`.

### `UnidadTarjetaResponse`

`id`, `fotoPrincipalUrl`, `precioMensualMxn`, `tipoUnidad`, `distanciaKm`,
`tiempoTraslado`, `paraQuien`, `arrendadorVerificado=false`, `esFavorito`.

`esFavorito` es `null` sin sesión y booleano con Bearer válido. `distanciaKm` y
`tiempoTraslado` son `null` si la universidad no tiene ubicación registrada; no
se inventan valores. `tiempoTraslado` expresa únicamente el modo (`a pie` si la
distancia es `<= 1.5 km`, `en transporte` si es mayor); no promete minutos ni
rutas.

### `UnidadFichaResponse`

Incluye todos los campos de tarjeta más `galeriaFotos`, `ocupacion`, `lugares`,
`tamanoM2`, `bano`, `mobiliario`, `serviciosIncluidos`, `serviciosAparte[]`
(`nombre`, `montoMensualMxn`) y `tipoPropiedad`. La galería usa las fotos
Cloudinary a través del proxy autorizado de la API. La ficha se marca
`Cache-Control: no-store`.

### `FavoritoResponse`

`unidadId` + `tarjeta` con los campos de tarjeta, `esFavorito=true` y
`estadoActual`. `estadoActual` refleja el estado real: `Disponible`, `Ocupado`,
`Mantenimiento`, `Borrador`, `EnRevision` o `Rechazada`. El favorito permanece
aunque la unidad deje de ser pública.

## Reglas y seguridad

- Solo unidades con `estadoPublicacion=Aprobada` y `estadoOcupacion=Disponible`
  (o campo ausente en documentos previos a M09) aparecen en mapa/lista/tarjeta y
  ficha.
- `Ocupado`, `Mantenimiento`, `EnRevision`, `Rechazada` y `Borrador` no aparecen
  en M04; tarjeta y ficha devuelven 404.
- Los filtros se aplican con AND; la distancia se calcula con Haversine en
  servidor y el umbral de traslado es inclusivo en 1.5 km.
- `universidadId` inexistente o inactiva devuelve 404. Si falta la ubicación de
  la universidad, `distanciaMaxKm` devuelve 400 y nunca se inventa distancia.
- M04 nunca serializa la entidad completa: allowlist de DTO; no incluye correo
  ni teléfono del arrendador (HU-071).
- Ficha y favoritos exigen Bearer. El `accountId` sale exclusivamente del claim
  `sub` del JWT validado; nunca de body/query.
- Favoritos se filtran siempre por cuenta. PUT/DELETE son idempotentes y el
  índice único impide duplicados. Ningún usuario puede listar ni quitar
  favoritos de otro. Solo se puede crear un favorito sobre una unidad pública o
  propia; una unidad ajena no pública responde 404 para no revelar su existencia.
  Si una unidad ya guardada deja de ser pública, el favorito se conserva y
  muestra su estado actual.
- Las respuestas autenticadas de M04/M07 se marcan `Cache-Control: no-store`.
- Errores homogéneos con ProblemDetails: 400, 401, 403 (regresión de plataforma
  en rutas administrativas), 404 y 503. El 403 no aplica a M04/M07 porque no
  tienen reglas de rol; se conserva el mapeo probado en M03/M05.
- Sin mocks en producción: MongoDB y Cloudinary fallan cerrados con 503 cuando
  falta configuración.

## Persistencia

- Se reutilizan `propiedades`, `unidades` y `universidades`.
- `unidades.estadoOcupacion` se lee con default `Disponible`; M09 lo
  recalculará.
- Nueva colección `favoritos`:
  - `_id` determinista `cuenta:unidad`.
  - `accountId`, `unitId`, `createdAt`.
  - `ux_favoritos_cuenta_unidad` único.
  - `ix_favoritos_cuenta_fecha` para listado paginado.
- Migración **M07-001** solo crea `favoritos`; no modifica M01-001, M01-002,
  M03-001 ni M05-001. La integración conserva M01-002 y añade M07-001 al
  registro. Detalle en [`mongodb-m07.md`](mongodb-m07.md).
- No se añadió un índice nuevo a `unidades` para no cambiar el checksum de
  M05-001; M04 usa los índices existentes y filtra distancia en aplicación. Una
  migración futura podrá añadir un índice compuesto si el volumen lo exige.
- Fotografías: Cloudinary autenticado + proxy `/api/v1/fotos/{photoRef}`; MongoDB
  guarda solo metadatos. Ver [`dec-04-cloudinary.md`](dec-04-cloudinary.md).

## Dependencias y bloqueos honestos

1. **M06 (Sprint 4/5):** no existe todavía el flujo que cambia una unidad de
   `Borrador` a `Aprobada`; en producción M04 devolverá vacío hasta que M06
   apruebe publicaciones. Las pruebas siembran unidades aprobadas.
2. **M06:** `arrendadorVerificado` permanece `false`; HU-046 no puede declararse
   completa hasta que exista verificación real.
3. **Cloudinary:** el adaptador está implementado y probado con dobles; la prueba
   real opt-in sigue **PENDIENTE** de credenciales seguras. No se declara la
   fotografía productiva como validada.
4. **Ubicación de universidad:** el contrato M03 no expone ni permite administrar
   `location`; sin ella la distancia/tiempo son null y `distanciaMaxKm` responde
   400. Se requiere carga controlada antes de usar el mapa con distancias reales.
5. **M09:** `Ocupado`/`Mantenimiento` solo se reflejan si el documento tiene
   `estadoOcupacion`; los endpoints de M09 no forman parte de este Sprint.
6. **Borrado de cuenta:** la retención/anonimización de favoritos y demás datos
   sigue sujeta a ADR-005; no se implementó en Sprint 3.
7. **Android:** pantalla inicial, conservación de destino y navegación son UI.
   HU-076 no tiene endpoint backend propio.

## Pruebas y DevSecOps

- Unitarias: Haversine/umbral, filtros combinados, exclusión de no públicas,
  tarjeta, ficha, servicios, favoritos idempotentes, aislamiento y estado no
  público.
- Integración: mapa/lista pública, filtros, paginación, 400/401/404/503,
  ausencia de PII, favoritos idempotentes, aislamiento entre cuentas y
  persistencia tras cambio de estado.
- OpenAPI: 7 rutas, seguridad de ficha/favoritos, enums string, query camelCase,
  `universidadId` required y multipart M05 intacto.
- Mongo real: `MongoExplorationPersistenceTests`, migración M07-001, favoritos
  reales y cambio de estado; se ejecuta en el job manual de MongoDB CI con una
  base aislada.
- ZAP visita las 30 operaciones OpenAPI, incluidas las 7 de M04/M07, con fixture
  público sembrado solo en la base efímera. RESTler compila y ejerce las 7 rutas
  con diccionarios de `universidadId`/`unitId`; su reporte exige intentarlas.
- El Quality Gate conserva build/test .NET 10, SonarCloud, Semgrep, Gitleaks,
  CodeQL, Stryker.NET, MongoDB CI, OpenAPI Contract Test, RESTler, ZAP, Docker
  Build, Dockle, Container Smoke, Hardening Report y PR Quality Gate.

## Clasificación

**SPRINT 3 BACKEND IMPLEMENTADO — NO PRODUCTIVO.** Los siete endpoints M04/M07
están implementados, integrados con Sprints 1/2 y cubiertos por pruebas locales.
La explotación real sigue condicionada por M06 (aprobación/verificación),
la carga de ubicaciones de universidades y la prueba real opt-in de Cloudinary.
No se hizo merge, no se desplegó y no se modificó Azure.
