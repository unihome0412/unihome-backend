# M06 — Persistencia de verificación documental

**Fecha:** 16 de noviembre de 2026
**Migración:** `M06-001`
**Estado:** implementada y probada localmente; **no aplicada ni promovida a Atlas**.

## Colecciones

### `expedientes_verificacion`

Un expediente por titular (`ownerAccountId`). Vincula la primera publicación
(`publicationId` = unidad, `propertyId`) y conserva el estado separado del
indicador de riesgo.

| Campo | Tipo | Notas |
|---|---|---|
| `_id` | string Guid N | Identificador del expediente |
| `folio` | string | Único, `UH-AAAA-XXXXXXXXXXXX` |
| `ownerAccountId` | string Guid N | Único; un expediente por cuenta |
| `publicationId` | string Guid N | Unidad de la primera publicación |
| `propertyId` | string Guid N | Propiedad asociada |
| `estado` | string | `Pendiente`, `EnRevision`, `Aprobado`, `Rechazado` |
| `ine`, `comprobante` | objeto o null | Metadatos del documento vigente |
| `esSospechoso` | bool | Derivado de señales; nunca es un estado |
| `senalesRiesgo` | string[] | Señales orientativas sin contenido documental |
| `resultadoComparacionNombres` | string | `NoDisponible` en Sprint 4 (HU-019 es Sprint 5) |
| `motivoRechazo` | string o null | Reservado para Sprint 5 |
| `createdAt`, `updatedAt`, `submittedAt` | fecha UTC | Trazabilidad |
| `decision` | objeto o null | Reservado para Sprint 5; actor, fecha y motivo |

Cada documento incluye `tipo`, `storageKey`, `originalFileName`, `contentType`,
`sizeBytes`, `sha256`, `version`, `uploadedAt`, `nombreOcr`, `estadoOcr`,
`estadoEdicion`, `edicionDetectada`, `elaDisponible`, `metadatosDisponibles`,
`senalesEdicion` y `detalleAnalisis`.

### `documentos_verificacion`

Guarda el binario **cifrado** con AES-256-GCM. Cada versión es un documento
independiente; el expediente apunta a la versión vigente.

| Campo | Tipo | Notas |
|---|---|---|
| `_id` | string | `doc-<guid>`; no contiene PII |
| `expedienteId` | string Guid N | Índice junto con tipo/versión |
| `tipo` | string | `Ine` o `Comprobante` |
| `version` | int | Creciente por tipo |
| `contentType` | string | `image/jpeg`, `image/png` o `application/pdf` |
| `originalFileName` | string | Saneado; solo metadato |
| `sizeBytes`, `sha256` | long/string | Huella del binario en claro |
| `nonce`, `tag`, `ciphertext` | binario | Contenedor AES-GCM |
| `createdAt` | fecha UTC | |

El `associated data` de AES-GCM autentica `expedienteId`, `tipo`, `version` y
`sha256`: un ciphertext no puede reasociarse a otro expediente o tipo. El hash se
recalcula al leer y se compara con el metadato; una divergencia falla cerrada.

### `eventos_expediente`

Bitácora append-only de carga, análisis, consulta de detalle, emisión de enlace y
lectura de documento. Registra actor, rol, acción, tipo/versión, timestamp y
`traceId`. No guarda contenido, nombres OCR ni tokens.

## Índices

| Colección | Índice | Definición |
|---|---|---|
| `expedientes_verificacion` | `ux_expedientes_propietario` | único `ownerAccountId` |
| | `ux_expedientes_folio` | único `folio` |
| | `ix_expedientes_propiedad` | `propertyId`, `createdAt` desc |
| | `ix_expedientes_revision` | `esSospechoso` desc, `estado`, `createdAt` asc |
| `documentos_verificacion` | `ux_documentos_expediente_tipo_version` | único `expedienteId`, `tipo`, `version` |
| | `ix_documentos_expediente_tipo_fecha` | `expedienteId`, `tipo`, `createdAt` desc |
| `eventos_expediente` | `ix_eventos_expediente_fecha` | `expedienteId`, `timestamp` desc |
| | `ix_eventos_actor_fecha` | `actorAccountId`, `timestamp` desc |

## Migración y fail-closed

`M06-001` crea las tres colecciones y sus índices. No modifica `M05-001`: el
estado de la primera publicación se actualiza en `unidades.estadoPublicacion`.
Los repositorios comprueban los índices en modo lectura antes de operar y
responden `503` si falta la migración o diverge.

```powershell
dotnet run --project tools/UniHome.MongoMigrator -- plan --database unihome_modelo_pruebas --version M06-001
dotnet run --project tools/UniHome.MongoMigrator -- apply --database unihome_modelo_pruebas --confirm --version M06-001
dotnet run --project tools/UniHome.MongoMigrator -- verify --database unihome_modelo_pruebas --version M06-001
```

`apply` solo escribe en bases de verificación autorizadas. La promoción a
`unihome_desarrollo` sigue siendo un comando separado con autorización explícita.
**No se ejecutó ninguna escritura en Atlas.**

## Límites y riesgos

- Cada documento acepta hasta 10 MB. El ciphertext se guarda en un solo documento
  BSON, por debajo del límite de 16 MB de MongoDB; no se usa GridFS.
- El tamaño del expediente crece con cada versión. La política de retención y
  purga de versiones/expedientes pertenece a ADR-005 y no está implementada.
- La clave `DocumentSecurity:EncryptionKey` debe tener 32 bytes en Base64 y
  provenir de un almacén de secretos. En Development se genera una clave efímera
  y los documentos dejan de abrirse al reiniciar el proceso.
- La rotación de clave no está implementada; cambiarla invalida los documentos
  existentes hasta migrarlos.
- La consulta de la primera unidad (`unidades.ownerAccountId` ordenada por
  `createdAt`) no tiene un índice dedicado porque `M05-001` no se modifica. Con
  el volumen actual el filtro es aceptable; una migración posterior puede añadir
  el índice compuesto sin tocar el checksum histórico.
- La colección externa expedientes_verificacion puede existir en Atlas con
  validador o índices previos. Antes de promover M06-001 hay que reconciliar
  nombres/definiciones; el migrador falla cerrado si divergen. No se modificó
  Atlas.
- La prueba real contra MongoDB (`MongoVerificationPersistenceTests`) está
  preparada pero **BLOCKED** en este entorno local porque no hay `mongod` ni se
  permite Docker. Se ejecutará en el job manual `mongo-db-test` cuando se autorice.
