# UniHome.MongoMigrator

Herramienta manual y versionada para auditar, planificar y verificar las
migraciones `M01-001` de `cuentas`/`sesiones_refresh` y `M03-001` de
`universidades`, y para ejecutar una promoción explícita a `unihome_desarrollo`
bajo controles separados. No forma parte de la API, no se invoca durante
`dotnet test` y no imprime la cadena de conexión, correos, códigos, tokens ni
documentos.

## Seguridad

- `apply` solo escribe en `unihome_modelo_pruebas` o en una base listada en
  `UNIHOME_MIGRATION_ALLOWED_DATABASES`. `unihome_desarrollo`, `admin`, `local` y
  `config` están siempre bloqueadas para esa ruta.
- `promote` es la única ruta de escritura para `unihome_desarrollo`. Exige:
  destino exacto, `--confirm-database` idéntico, autorización
  `UNIHOME_PROMOTION_AUTHORIZED_DATABASE=unihome_desarrollo`, `--approval-ref` y
  credencial exclusiva.
- `promote` no consulta la allowlist ordinaria y no se ejecuta desde la API.
- La cadena de conexión normal se lee de User Secrets
  (`MongoDb:ConnectionString`) o de `MongoDb__ConnectionString`. La promoción usa
  exclusivamente `MongoDb:MigrationConnectionString`, sin fallback. Si la
  credencial operativa está presente, se comparan usuario, `authSource`, mecanismo,
  hosts, réplica y base, no solo la cadena completa.
- `promote` y cualquier comando con `--migration-credential` exigen TLS con
  validación de certificado. Rechazan TLS deshabilitado, `tlsInsecure`,
  certificados/hostnames inválidos y callbacks personalizados. No modifican TLS ni
  certificados.
- El valor de la URI nunca se pasa como argumento ni se imprime.

## Comandos

```powershell
dotnet run --project tools/UniHome.MongoMigrator -- audit --database unihome_modelo_pruebas
dotnet run --project tools/UniHome.MongoMigrator -- plan --database unihome_modelo_pruebas
dotnet run --project tools/UniHome.MongoMigrator -- apply --database unihome_modelo_pruebas --confirm
dotnet run --project tools/UniHome.MongoMigrator -- verify --database unihome_modelo_pruebas
```

Lectura con la credencial técnica (no escribe):

```powershell
dotnet run --project tools/UniHome.MongoMigrator -- audit --database unihome_desarrollo --migration-credential
dotnet run --project tools/UniHome.MongoMigrator -- plan --database unihome_desarrollo --migration-credential
```

Promoción (no ejecutar sin completar autorización, usuario técnico y respaldo):

```powershell
$env:UNIHOME_PROMOTION_AUTHORIZED_DATABASE = 'unihome_desarrollo'
dotnet user-secrets set "MongoDb:MigrationConnectionString" "<URI_TECNICA>" `
  --id unihome-backend-migrations
dotnet run --project tools/UniHome.MongoMigrator -- promote `
  --database unihome_desarrollo `
  --confirm-database unihome_desarrollo `
  --approval-ref "<REFERENCIA_DE_APROBACION>"
```

`--version M01-001|M03-001|M05-001` es opcional; el valor predeterminado es
M01-001. No se admiten alias, mayúsculas distintas, espacios ni nombres
parciales.

## Alcance

La migración crea, si faltan:

- `cuentas` con `ux_cuentas_correo_normalizado` único sobre `correoNormalizado`.
- `sesiones_refresh` con hash actual único, índice por `cuentaId` y TTL de
  limpieza sobre `expiraEnUtc`.
- `migraciones` con la versión en `_id` (unicidad nativa de MongoDB) y el
  checksum de la definición.

`M03-001` crea, si faltan:

- `universidades` con `ux_universidades_nombre_normalizado` único sobre
  `nombreNormalizado`, usado por HU-075 para rechazar duplicados y buscar sin
  acentos ni mayúsculas.

`M05-001` crea, si faltan:

- `propiedades`, `unidades` y `aceptaciones_terminos` con sus índices de
  propietario, búsqueda, duplicado de propiedad y unicidad de términos.

Las migraciones M01, M03 y M05 se seleccionan con `--version`; no se aplican en
cascada. El TTL solo elimina sesiones vencidas; la autorización de una sesión se comprueba
siempre en el backend. No se crean validadores JSON para M01 porque el contrato no
los define.

La herramienta verifica las opciones críticas de los índices existentes y rechaza
divergencias en clave, unicidad, TTL, filtro parcial, colación, `sparse` u oculto.
No renombra ni elimina índices existentes. Rechaza TTL o restricciones únicas
adicionales no previstas en las colecciones M01. La API solo lee metadatos para
comprobar los índices antes de operar; no aplica migraciones.

El ledger usa inserción inmutable y compara el checksum ante inserciones
concurrentes. Cuando el registro lo inserta `promote`, incluye un subdocumento
`promocion` con `aprobacionRef`, base autorizada, versión, checksum y `ownerId`;
la referencia de aprobación queda así vinculada al registro. `apply` no tiene
bloqueo distribuido: ejecutar un solo migrador por base y auditar cualquier fallo
parcial antes de reintentar. `promote` sí adquiere un bloqueo en `migraciones` con
lease de 30 minutos, `fencingToken` y heartbeat cada 10 minutos; solo toma bloqueos
vencidos usando el reloj del servidor, renueva el lease y comprueba propietario,
token y vigencia antes de cada escritura. La primera adquisición crea el ledger y
esa escritura se contabiliza en el resultado.

## Preflight y promoción

El preflight es de solo lectura y clasifica el destino como `Empty`, `NotStarted`,
`PartiallyApplied`, `Applied` o `Divergent`. Rechaza:

- Colecciones no administradas por la migración seleccionada.
- Documentos en `cuentas` o `sesiones_refresh`, mediante lectura exacta acotada y
  revalidación final.
- Checksum, validador o índice divergente.
- Versiones de ledger duplicadas o no registradas.
- Validador o índice adicional en `migraciones`.
- Plan de otro destino, de otra versión o con escrituras fuera del contrato.

Un estado `Divergent` no se repara automáticamente. No borrar colecciones, índices
ni registros del ledger para “reintentar limpio”. Un estado parcial se reintenta de
forma idempotente solo después de revisar `audit` y `plan`. Incluso un no-op se
ejecuta bajo bloqueo para no solaparse con otra promoción; no cuenta escrituras de
migración.

`MongoValidatorDefinition` permite declarar y verificar validadores para
migraciones futuras, pero no se copiaron los 20 validadores de Atlas ni se
inventaron contratos BSON/DTO. `M01-001` conserva su checksum publicado.

## Diagnóstico seguro de conectividad

Repetir únicamente la auditoría de lectura desde PowerShell, desde la raíz del repo:

```powershell
dotnet run --project tools/UniHome.MongoMigrator/UniHome.MongoMigrator.csproj `
  --configuration Release --no-build -- audit --database unihome_modelo_pruebas
```

No ejecutar `apply` ni `promote` para diagnosticar conectividad. No usar
`dotnet user-secrets list`, volcados del entorno, trazas de red o mensajes crudos
del driver para compartir el resultado. No pasar la URI como argumento.

Ante una excepción, stderr contiene exclusivamente estos cuatro campos por
excepción (incluidas causas internas y fallos de transporte observados):

```text
Stage=MongoCommandExecution
ExceptionType=System.TimeoutException
InnerExceptionType=(none)
HResult=0x80131505
```

Etapas: `ConfigurationLoading`, `ConfigurationValidation`, `SettingsParsing`,
`ClientConstruction`, `DatabaseSelection`, `Promotion`,
`MongoCommandExecution` y `ClientDisposal`. Los eventos del driver aportan
`ConnectionOpening`, `ServerSelection`, `TlsNegotiation` o `MongoAuthentication`
cuando el tipo permite distinguirlos. No se imprimen mensajes, stack traces, Data,
endpoints ni cadenas de conexión. Los eventos se deduplican y acotan; no conservan
objetos Exception.

`MongoCommandExecution` significa que se invocó la operación del driver, no que el
servidor haya recibido un comando: la selección de servidor y la conexión son
diferidas. Un timeout puede no tener InnerException aunque los heartbeats contengan
una causa TLS; por eso se capturan esos eventos sin analizar sus mensajes. Los
eventos son históricos del cliente, sin correlación causal con la excepción final,
y se limitan a 32 entradas únicas: no son un inventario exhaustivo ni una
certificación de la causa única del fallo. Un fallo de Dispose se informa
separadamente sin sustituir la excepción original de la operación.

Configuración normal: `UserSecretsId=unihome-backend-api`, primero User Secrets y
después entorno (este tiene prioridad). `AddEnvironmentVariables` normaliza
`MongoDb__ConnectionString` a `MongoDb:ConnectionString`. La promoción usa
explícitamente el almacén `unihome-backend-migrations` y no cae en la credencial
operativa. El timeout conserva su comportamiento: 5 segundos por defecto, valores
válidos entre 1 y 60; valores inválidos usan el default. No se han cambiado
timeouts de conexión/TLS, protocolos, certificados, validación de certificados,
credenciales ni `MongoMigrationGuard`.

### Reproducción de esta revisión

Una ejecución de `audit` de solo lectura sobre `unihome_modelo_pruebas` superó carga
de configuración, parseo y construcción del cliente, y falló con:

| Stage | ExceptionType | InnerExceptionType | HResult |
|---|---|---|---|
| MongoCommandExecution | System.TimeoutException | (none) | 0x80131505 |
| TlsNegotiation | MongoDB.Driver.MongoConnectionException | System.Security.Authentication.AuthenticationException | 0x80131500 |
| TlsNegotiation | System.Security.Authentication.AuthenticationException | System.ComponentModel.Win32Exception | 0x80131501 |
| TlsNegotiation | System.ComponentModel.Win32Exception | (none) | 0x80004005 |

La reproducción confirma timeout y fallos de negociación TLS observados en la
capa de seguridad de Windows. La selección de servidor no consiguió una conexión
utilizable; TLS es una causa de transporte observada compatible con ese timeout,
no una prueba de causa única. No falló el parseo de configuración en esa ejecución
ni se observó una respuesta de autenticación MongoDB. Esto no demuestra que las
credenciales sean válidas: la negociación TLS observada falla antes.

El código nativo `0x80090304` aportado en el reporte corresponde a
`SEC_E_INTERNAL_ERROR` de SSPI. `Win32Exception.HResult` no necesariamente coincide
con `NativeErrorCode`: esta herramienta muestra solo HResult, conforme al límite
del diagnóstico. No se ha demostrado qué condición interna de Windows/LSA causa
ese error; no se deben modificar certificados, desactivar TLS o cambiar credenciales
para intentar resolverlo especulativamente. Cualquier investigación adicional del
sistema operativo requiere evidencia y autorización separadas.

## Respaldo y límites

El procedimiento de respaldo/restauración con MongoDB Database Tools está en
[`docs/mongodb-promocion.md`](../../docs/mongodb-promocion.md). **No se ejecutó un
respaldo ni una restauración en esta fase.** No declarar un respaldo válido sin
restaurarlo en una base aislada y comprobarlo.

La promoción real no se ejecutó. Siguen abiertos: conectividad TLS/SSPI, usuario
técnico separado verificado, respaldo/restauración y autorización humana. Las 20
colecciones externas no tienen migración versionada ni contratos BSON/DTO
aprobados.
