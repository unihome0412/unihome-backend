# Sprint 3 — Cierre backend M04/M07

**Rama:** `feature/sprint-03-backend-integral`  
**Base:** `main` posterior al merge del Sprint 2 (`ab90bf0ada4d59b0bd87406ff5e5291ddd168528`)  
**Alcance:** HU-007 (búsqueda M03 ya existente), 13 HU de M04 y HU-085 de M07.  
**No incluye:** M06, aprobación de expedientes, M09, chat, rentas ni UI Android.

## Matriz de las 15 HU

| HU | RF | Soporte backend | Endpoint / decisión |
|---|---|---|---|
| HU-007 | RF-03 | Reutiliza M03 | `GET /api/v1/universidades?buscar=` ya existente; solo activas, máximo 10 |
| HU-037 | RF-18 | Implementado | `GET /api/v1/exploracion/mapa` y `/exploracion/lista` |
| HU-038 | RF-18 | Implementado | `GET /api/v1/unidades/{id}/tarjeta` |
| HU-039 | RF-19 | Implementado | `distanciaMaxKm` en mapa/lista |
| HU-040 | RF-19 | Implementado | `precioMaxMxn` en mapa/lista |
| HU-041 | RF-19 | Implementado | `soloIndividual` en mapa/lista |
| HU-042 | RF-19 | Implementado | `paraQuien` en mapa/lista |
| HU-043 | RF-20 | Implementado con límite documentado | Haversine y umbral 1.5 km; sin motor de rutas no se inventan minutos |
| HU-044 | RF-21 | Implementado | `GET /api/v1/unidades/{id}/ficha`; galería y características |
| HU-045 | RF-21 | Implementado | Ficha con servicios incluidos y aparte |
| HU-046 | RF-21 | Parcial por dependencia M06 | Campo `arrendadorVerificado=false` hasta verificación real |
| HU-047 | RF-21 | Implementado | `paraQuien` en tarjeta; `tipoPropiedad` en ficha |
| HU-076 | RF-40 | Solo UI | Sin endpoint propio; el backend no implementa navegación |
| HU-084 | RF-40 | Implementado | Mapa/lista públicos sin Bearer |
| HU-085 | RF-41 | Implementado | `PUT/DELETE /api/v1/favoritos/{unidadId}` y `GET /api/v1/favoritos` |

> HU-086 pertenece al Sprint 6 y no se contabiliza en las 15 HU de este Sprint; el backend ya devuelve 401 en ficha y favoritos para que el cliente conserve la acci?n pendiente. HU-071 tambi?n es Sprint 6 y se aplica como control de no exponer PII.


## Endpoints implementados

| ID | Método | Ruta | Auth | Éxito |
|---|---|---|---|---|
| EP-M04-01 | GET | `/api/v1/exploracion/mapa` | Público | 200 |
| EP-M04-02 | GET | `/api/v1/exploracion/lista` | Público | 200 |
| EP-M04-03 | GET | `/api/v1/unidades/{unitId}/tarjeta` | Público; Bearer opcional para `esFavorito` | 200 / 404 |
| EP-M04-04 | GET | `/api/v1/unidades/{unitId}/ficha` | Bearer | 200 / 401 / 404 |
| EP-M07-01 | PUT | `/api/v1/favoritos/{unitId}` | Bearer | 204 / 404 |
| EP-M07-02 | DELETE | `/api/v1/favoritos/{unitId}` | Bearer | 204 idempotente |
| EP-M07-03 | GET | `/api/v1/favoritos?page=&pageSize=` | Bearer | 200 |

### Query de exploración

`universidadId` obligatorio; `distanciaMaxKm`, `precioMaxMxn`, `soloIndividual` y
`paraQuien` opcionales y combinables con AND. `page`/`pageSize` solo aplican a la
lista. La lista devuelve `X-Total-Count`, `X-Page` y `X-Page-Size`.

### `UnidadMapaResponse`

`id`, `latitud`, `longitud`, `precioMensualMxn`.

### `UnidadTarjetaResponse`

`id`, `fotoPrincipalUrl`, `precioMensualMxn`, `tipoUnidad`, `distanciaKm`,
`tiempoTraslado`, `paraQuien`, `arrendadorVerificado`, `esFavorito`.

`esFavorito` es `null` sin sesión y booleano con Bearer válido. `distanciaKm` y
`tiempoTraslado` pueden ser `null` si la universidad no tiene ubicación
registrada; no se inventan valores. `tiempoTraslado` expresa el modo (`a pie` o
`en transporte`) y no minutos sin motor de rutas.

### `UnidadFichaResponse`

Incluye todos los campos de tarjeta más `galeriaFotos`, `ocupacion`, `lugares`,
`tamanoM2`, `bano`, `mobiliario`, `serviciosIncluidos`, `serviciosAparte[]`
(`nombre`, `montoMensualMxn`) y `tipoPropiedad`.

### `FavoritoResponse`

`unidadId` + `tarjeta` con `id`, `fotoPrincipalUrl`, `precioMensualMxn`,
`tipoUnidad`, `distanciaKm`, `tiempoTraslado`, `paraQuien`,
`arrendadorVerificado`, `esFavorito=true`, `estadoActual`.

`estadoActual` refleja el estado actual: `Disponible`, `Ocupado`,
`Mantenimiento`, `Borrador`, `EnRevision` o `Rechazada`. El favorito permanece
aunque la unidad deje de ser pública.

## Reglas y seguridad

- Solo unidades con `estadoPublicacion=Aprobada` y `estadoOcupacion=Disponible`
  (o campo ausente en documentos previos) aparecen en mapa/lista/tarjeta/ficha.
- `Ocupado`, `Mantenimiento`, `EnRevision` y `Rechazada` no aparecen en M04.
- `M04` nunca serializa la entidad completa: allowlist de DTO; no incluye correo
  ni teléfono del arrendador (HU-071).
- Distancia Haversine en servidor; umbral 1.5 km inclusivo.
- Ficha requiere Bearer; tarjeta es pública y añade favorito solo si hay sesión.
- Favoritos siempre filtrados por `accountId` del JWT; nunca por body/query.
- PUT/DELETE de favoritos son idempotentes; índice único impide duplicados.
- Errores con ProblemDetails: 400, 401, 404, 503.
- Sin mocks en producción; `IUnitPhotoStorage` sigue fail-closed sin DEC-04.

## Persistencia

- Se reutilizan `propiedades`, `unidades` y `universidades`.
- `unidades.estadoOcupacion` se lee con default `Disponible`; M09 lo recalculará.
- Nueva colección `favoritos`:
  - `_id` determinista cuenta:unidad.
  - `accountId`, `unitId`, `createdAt`.
  - `ux_favoritos_cuenta_unidad` único.
  - `ix_favoritos_cuenta_fecha` para listado paginado.
- Migración **M07-001** solo crea `favoritos`; no modifica M01-001, M03-001 ni
  M05-001. Detalle en [`mongodb-m07.md`](mongodb-m07.md).
- No se añadió un índice nuevo a `unidades` para no cambiar el checksum de
  M05-001; M04 usa los índices existentes de M05 y filtra la distancia en
  aplicación. Una migración futura podrá añadir un índice compuesto si el
  volumen lo exige.

## Dependencias y bloqueos

1. **M06 (Sprint 4/5):** no existe todavía el flujo que cambia una unidad de
   `Borrador` a `Aprobada`; en producción las consultas M04 devolverán vacío
   hasta que M06 apruebe publicaciones. Las pruebas siembran unidades aprobadas.
2. **M06:** `arrendadorVerificado` permanece `false`; HU-046 no puede declararse
   completa hasta que exista la verificación real.
3. **DEC-04:** las fotos reales siguen bloqueadas; `fotoPrincipalUrl` y
   `galeriaFotos` pueden ser null/vacíos mientras no haya proveedor. No se
   declara HU-044 fotográfica completa en producción.
4. **Datos de universidad:** el contrato M03 no expone ni permite administrar
   `location`; si falta, la distancia/tiempo son null y un filtro
   `distanciaMaxKm` responde 400. Se requiere carga controlada de ubicaciones
   antes de usar el mapa con distancias reales.
5. **M09:** `Ocupado`/`Mantenimiento` solo se reflejan si el documento tiene
   `estadoOcupacion`; los endpoints de M09 no forman parte de este Sprint.
6. **Android:** pantalla inicial, conservación de destino y navegación son UI.

## Pruebas

- Unitarias: Haversine/umbral, filtros combinados, exclusión de no públicas,
  tarjeta, ficha, servicios, favoritos idempotentes y estado no público.
- Integración: mapa/lista pública, filtros, 401 de ficha, 404 de no pública,
  ausencia de PII, favoritos idempotentes y persistencia tras cambio de estado.
- OpenAPI: 7 rutas nuevas y seguridad de ficha/favoritos.
- Mongo real: `MongoExplorationPersistenceTests`, migración M07-001, favoritos
  reales y cambio de estado.
- Regresión completa de Sprint 1/2.

## Clasificación

**SPRINT 3 BACKEND PARCIAL.** Los endpoints M04/M07 están implementados y
probados, pero la explotación real queda condicionada por M06 (aprobación y
verificación), DEC-04 (fotos) y la carga de ubicaciones de universidades. No se
declara el Sprint operativo hasta resolver esas dependencias.
