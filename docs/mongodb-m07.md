# M07 — Favoritos y lectura de exploración

## Colección `favoritos`

| Campo | Tipo BSON | Uso |
|---|---|---|
| `_id` | string | `accountId:unitId` determinista |
| `accountId` | string Guid N | Dueño del favorito |
| `unitId` | string Guid N | Unidad guardada |
| `createdAt` | date UTC | Orden descendente del listado |

Índices:

- `ux_favoritos_cuenta_unidad`: único de `accountId + unitId`.
- `ix_favoritos_cuenta_fecha`: `accountId + createdAt` descendente.

El alta usa `updateOne` con `$setOnInsert` y `upsert`, por lo que dos `PUT`
concurrentes no duplican ni fallan. La baja es `deleteOne`; si no existía, sigue
siendo `204`.

## Migración

**`M07-001`** crea únicamente `favoritos` con sus dos índices. **No modifica**
M01-001, M03-001 ni M05-001; por tanto no cambia sus checksums históricos.

```powershell
dotnet run --project tools/UniHome.MongoMigrator -- apply `
  --database unihome_modelo_pruebas --confirm --version M07-001
dotnet run --project tools/UniHome.MongoMigrator -- verify `
  --database unihome_modelo_pruebas --version M07-001
```

## Campo de disponibilidad

M04 exige excluir `Ocupado` y `Mantenimiento`. Se lee
`unidades.estadoOcupacion` con default `Disponible` para documentos anteriores a
M09. M09 será la fuente de verdad y recalculará el campo; este Sprint no expone
endpoints para cambiarlo.

## Consultas M04

- M04 filtra en MongoDB por `universidadId`, `estadoPublicacion=Aprobada`,
  disponibilidad y filtros de precio/ocupación/paraQuién.
- La distancia se calcula en Application con Haversine; después se filtra por
  `distanciaMaxKm` y se pagina.
- No se añadió un índice nuevo a `unidades` para no alterar la migración M05-001.
  Se reutilizan `ix_unidades_busqueda` e `ix_unidades_propiedad`; si el volumen
  crece, una migración nueva podrá añadir un índice compuesto.

## Datos y bloqueos

- M06 debe aprobar publicaciones para que existan unidades `Aprobada` reales.
- DEC-04 debe proveer el almacenamiento de fotos; M07 y M04 devuelven URLs
  null/vacías si el puerto falla cerrado.
- La ubicación de universidad debe cargarse por un proceso controlado; el
  contrato M03 actual no la administra.
