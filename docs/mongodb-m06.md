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
| `resultadoComparacionNombres` | string | `CoincidenciaOrientativa`, `DiferenciaDetectada` o `NoDisponible` |
| `comparacionNombresActualizadaAt` | fecha UTC o null | Evidencia de la última comparación (sin guardar los nombres) |
| `motivoRechazo` | string o null | Motivo visible al solicitante |
| `createdAt`, `updatedAt`, `submittedAt` | fecha UTC | Trazabilidad |
| `decision` | objeto o null | Actor, fecha, estado y motivo de la decisión humana |

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
| `sizeBytes` | long | Tamaño del binario en claro |
| `sha256`, `keyVersion` | string/int | Huella del binario en claro y versión de la clave AES (0/ausente en documentos previos se interpreta como versión 1) |
| `nonce`, `tag`, `ciphertext` | binario | Contenedor AES-GCM |
| `createdAt` | fecha UTC | |

El `associated data` de AES-GCM autentica `expedienteId`, `tipo`, `version` y
`sha256`: un ciphertext no puede reasociarse a otro expediente o tipo. El hash se
recalcula al leer y se compara con el metadato; una divergencia falla cerrada.

### Rotación de clave

`DocumentSecurity:CurrentKeyVersion` identifica la clave actual. Las claves
anteriores se declaran en `DocumentSecurity:PreviousEncryptionKeys`, en orden
desde la versión 1 hasta la N-1. Cada documento guarda `keyVersion`; al leer se
elige la clave correspondiente. Para rotar:

1. Añadir la clave actual al final de `PreviousEncryptionKeys`.
2. Cambiar `EncryptionKey` por la nueva clave Base64 de 32 bytes.
3. Incrementar `CurrentKeyVersion`.
4. Reiniciar; las lecturas antiguas usan la clave histórica y las nuevas escriben
   con la nueva. No se reescriben documentos existentes.

La clave de firma de URL se deriva de la clave actual si no se configura una
independiente; los enlaces ya emitidos expiran por diseño (300 s por defecto).
No se registran claves ni tokens en logs.

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
Los campos de decisión y comparación de Sprint 5 usan el esquema flexible de las
colecciones existentes; **no se añadió una migración nueva ni se cambió el checksum
de `M06-001`**. Los repositorios comprueban los índices en modo lectura antes de
operar y responden `503` si falta la migración o diverge.

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
- `DocumentSecurity:EncryptionKey` debe tener 32 bytes en Base64 y provenir de un
  almacén de secretos. En Development se genera una clave efímera si falta; los
  documentos dejan de abrirse al reiniciar.
- El tamaño del expediente crece con cada versión. La política de retención y
  purga de versiones/expedientes pertenece a ADR-005 y no está implementada.
- La rotación de clave está soportada con `CurrentKeyVersion` y
  `PreviousEncryptionKeys`; no existe una herramienta automática de reescritura.
  Cambiar la clave sin conservar la anterior deja documentos ilegibles.
- `pdftoppm` (Poppler) rasteriza PDF escaneados para OCR y miniaturas. Está
  instalado en la imagen Docker; si falta en un entorno, el PDF sin capa de texto
  queda `NoDisponible` y la miniatura vuelve al PDF original.
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
