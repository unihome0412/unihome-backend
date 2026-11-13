# M05 — Propiedades, unidades y términos

## Colecciones

### `propiedades`

| Campo | Tipo BSON | Uso |
|---|---|---|
| `_id` | string Guid N | Identificador |
| `ownerAccountId` | string Guid N | Titular |
| `nombre`, `nombreNormalizado` | string | Nombre visible y clave de duplicado |
| `direccion`, `direccionNormalizada` | string | Dirección privada |
| `colonia`, `coloniaNormalizada` | string | Colonia |
| `tipoPropiedad` | string | Casa, Departamento o Edificio |
| `universidadId` | string Guid N | Universidad cercana |
| `location` | GeoJSON Point opcional | `[longitud, latitud]` |
| `piso`, `numeroUnidad` | string opcional | Solo Edificio |
| `estadoPublicacion` | string | Borrador en Sprint 2 |
| `firstApprovalAt` | date opcional | Reservado para M06 |
| `createdAt` | date UTC | Alta |

Índices:

- `ux_propiedades_propietario_direccion`: único de propietario + dirección + colonia + nombre normalizados.
- `ix_propiedades_propietario`: propietario + fecha.

### `unidades`

Campos principales: `propertyId`, `ownerAccountId`, `universidadId`, `tipoUnidad`,
`ocupacion`, `lugares`, `precioMensualPorLugarMxn` (Decimal128),
`tamanoM2` (Decimal128), `paraQuien`, `bano`, `mobiliario`, `muebles`,
`plantillaBase`, `informacionZona`, `reglasConvivencia`, `servicios[]`,
`fotos[]`, `estadoPublicacion` y `createdAt`.

Cada foto guarda solo metadatos: `storageKey`, `contentType`, `sizeBytes`,
`sha256`, `orden`, `esPrincipal` y `createdAt`. El binario no se almacena en
MongoDB.

Índices:

- `ix_unidades_propiedad`: propiedad + fecha.
- `ix_unidades_busqueda`: estado + precio.

### `aceptaciones_terminos`

`_id`, `accountId`, `versionTerminos`, `acceptedAt`. Índice único
`ux_aceptaciones_cuenta_version` por cuenta + versión.

## Migración

**`M05-001`** crea las tres colecciones y sus índices. No modifica M01-001 ni
M03-001 y no declara validadores JSON no ratificados.

```powershell
dotnet run --project tools/UniHome.MongoMigrator -- apply `
  --database unihome_modelo_pruebas --confirm --version M05-001
dotnet run --project tools/UniHome.MongoMigrator -- verify `
  --database unihome_modelo_pruebas --version M05-001
```

El job `mongo-db-test` aplica M01-001, M01-002, M03-001 y M05-001 en
`unihome_ci_verificacion` y ejecuta la prueba real
`MongoPublicationPersistenceTests`.

## DEC-04 ? almacenamiento de fotos

La decisi?n vigente es **Cloudinary Free** (ver
[`dec-04-cloudinary.md`](dec-04-cloudinary.md)). El puerto `IUnitPhotoStorage`
mantiene la validaci?n de tama?o, firma real, hash y ownership. El binario se
sube como `authenticated`; MongoDB solo guarda el `public_id` de Cloudinary en
`storageKey` y los metadatos de `unidades.fotos[]`.

La entrega no expone la URL firmada de Cloudinary: `GetReadUrl` devuelve el proxy
`GET /api/v1/fotos/{photoRef}`, que verifica la visibilidad/ownership antes de
descargar el binario. Los borradores no son p?blicamente accesibles.

Si faltan `Cloudinary__CloudName`, `Cloudinary__ApiKey` o
`Cloudinary__ApiSecret`, se conserva el adaptador fail-closed (`503`); no se
simula almacenamiento. La prueba real de Cloudinary permanece **PENDIENTE**
hasta disponer de credenciales seguras y ejecutar el test opt-in.

## Gate Atlas

No se modificó Atlas ni se aplicó M05-001 fuera del MongoDB efímero de CI. La
promoción requiere autorización humana, respaldo/restauración verificado y
conciliación con las colecciones/índices existentes. Es un Gate independiente
del merge del PR.
