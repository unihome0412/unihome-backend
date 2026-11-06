# Promoción controlada de MongoDB y evolución versionada

**Estado:** mecanismo implementado y probado localmente. **No se ejecutó ninguna
promoción ni escritura en `unihome_desarrollo`.** La ejecución real sigue bloqueada
por autorización humana, credencial técnica verificada y respaldo/restauración
comprobados.
**Fecha:** 2026-11-04.
**Alcance:** `M01-001`, registro versionado y preparación de validadores para
migraciones futuras. No modifica `.local-sdd`, credenciales, certificados ni TLS.

## 1. Inventario y contrato de M01

El código solo contiene una migración versionada:

| Versión | Nombre | Colecciones | Índices | Checksum |
|---|---|---|---|---|
| `M01-001` | Identidad M01: cuentas y sesiones_refresh | `cuentas`, `sesiones_refresh` | `ux_cuentas_correo_normalizado`; `ux_sesiones_refresh_token_actual`, `ix_sesiones_refresh_cuenta`, `ttl_sesiones_refresh_expiracion` | `6FE8415A25A62BFA8C2A6D2F12C591B6645FF388D69E1B5260298B940E08CC4D` |

El ledger es la colección `migraciones`. Cada registro usa `_id` como versión y
contiene `nombre`, `descripcion`, `checksumSha256` y `aplicadaEnUtc`. Cuando el
registro lo inserta `promote`, incluye además un subdocumento `promocion` con
`aprobacionRef`, `baseAutorizada`, `versionAprobada`, `checksumAprobado` y `ownerId`
(identificador aleatorio no personal del ejecutor). La inserción es inmutable: una
definición distinta con la misma versión se rechaza y exige una migración nueva.
`M01-001` no declara validador porque el contrato de identidad no lo define; no se le
añadirá un validador sin aprobación de contrato.

Sobre una base vacía, el plan de `M01-001` es determinista:

1. Crear `cuentas`.
2. Crear `ux_cuentas_correo_normalizado`.
3. Crear `sesiones_refresh`.
4. Crear `ux_sesiones_refresh_token_actual`.
5. Crear `ix_sesiones_refresh_cuenta`.
6. Crear `ttl_sesiones_refresh_expiracion`.
7. Crear `migraciones`.
8. Insertar el registro de `M01-001`.

Son **8 escrituras** de M01: 2 colecciones de identidad, 4 índices, 1 colección de
ledger y 1 registro. `promote` crea la colección del ledger antes de tomar el
bloqueo; por eso el preflight inicial puede informar 8 y el preflight con bloqueo 7.
El resultado suma la creación del ledger al total aplicado, de modo que la primera
promoción informa 8 escrituras efectivas. No se crean las 20 colecciones externas.

## 2. Estado observado de las bases

| Base | Colecciones | `M01-001` | Validadores externos | Documentos de identidad | Evidencia |
|---|---:|---|---|---|---:|---|
| `unihome_modelo_pruebas` | 23 | Registrada y checksum coincidente | 20/20 en `strict/error` | 0 cuentas, 0 sesiones, 1 ledger | Auditoría de solo lectura previa al cierre |
| `unihome_verificacion` | 3 | Registrada; sin escrituras pendientes | No aplica: solo contiene M01 | Limpieza completada | Prueba real opt-in 1/1 aprobada en una ejecución anterior |
| `unihome_desarrollo` | 0 | No registrada | No aplica | Vacía | Última auditoría de solo lectura previa; no revalidada al cierre por el bloqueo TLS |

Las 20 colecciones externas observadas en `unihome_modelo_pruebas` son:

`auditoria_admin`, `conversaciones`, `correos_salientes`, `denuncias`,
`expedientes_verificacion`, `favoritos`, `invitaciones_unidad`, `mensajes`,
`notificaciones`, `pagos_informativos`, `propiedades`, `rentas_periodo`,
`reparaciones`, `restablecimientos_contrasena`, `servicios_catalogo`,
`solicitudes_eliminacion`, `tareas_eliminacion_archivos`, `unidades`,
`universidades` y `vinculos_ocupacion`.

Sus validadores e índices están inventariados en `docs/mongodb-m01.md`. No tienen
clases C#, DTOs ni manifiestos de migración aprobados en este repositorio.
**No se deben promover copiando la base, sus validadores o su ledger:** primero
deben existir migraciones versionadas aprobadas y contrastadas con cada módulo.
`MongoPromotionPreflightEvaluator` rechaza cualquier colección no administrada por
la migración seleccionada.

## 3. Controles implementados

### 3.1 Guard de promoción separado

`MongoPromotionGuard` es independiente de `MongoMigrationGuard`:

- El destino debe ser exactamente `unihome_desarrollo`, con comparación ordinal;
  se rechazan mayúsculas, espacios y nombres parciales.
- `--confirm-database` debe coincidir exactamente con `--database`.
- Debe existir `UNIHOME_PROMOTION_AUTHORIZED_DATABASE` con el mismo valor exacto.
  Esta variable no sustituye la aprobación humana; registra la autorización de la
  sesión.
- Debe existir `--approval-ref`, no secreto, de hasta 128 caracteres y sin
  caracteres de control.
- La allowlist ordinaria `UNIHOME_MIGRATION_ALLOWED_DATABASES` no se consulta
  para promover y no puede habilitar `unihome_desarrollo`.
- `apply` sigue usando `MongoMigrationGuard` y sigue bloqueado para
  `unihome_desarrollo`. La promoción es un comando separado.

### 3.2 Credencial exclusiva

- `promote` usa exclusivamente `MongoDb:MigrationConnectionString`.
- No existe fallback a `MongoDb:ConnectionString` ni a
  `MongoDb__ConnectionString`.
- Si la credencial de migración falta, o su identidad efectiva (usuario,
  `authSource`, mecanismo, hosts, réplica y base) coincide con la credencial
  operativa presente en la misma configuración, el comando falla antes de crear el
  cliente. La comparación no se limita a la cadena completa.
- El almacén recomendado es el `UserSecretsId` separado
  `unihome-backend-migrations`. Los comandos de solo lectura pueden usar
  `--migration-credential` para leer con esa misma configuración; `apply` lo
  rechaza.
- La URI nunca se imprime, no se pasa como argumento y no se escribe en el
  repositorio.
- El usuario Atlas de migración debe tener solo los privilegios necesarios:
  `listCollections` y `listIndexes`; `createCollection` y `createIndex` sobre las
  colecciones administradas; `find` y `count` sobre esas colecciones y
  `migraciones`; `insert` en `migraciones`; y `findAndModify`/`update`/`delete`
  sobre el documento de bloqueo `promotion-lock` de `migraciones`. La definición
  exacta de roles se aprueba en Atlas; no se crean usuarios en esta fase.

### 3.3 TLS

`MongoPromotionTlsGuard` se aplica a `promote` y a cualquier comando que use
`--migration-credential`. Rechaza, sin modificar la configuración:

- TLS deshabilitado (`UseTls` distinto de `true`, `tls=false` o `ssl=false`).
- `AllowInsecureTls` o `tlsInsecure=true`.
- `VerifySslCertificate=false`, `tlsAllowInvalidCertificates=true`,
  `tlsAllowInvalidHostnames=true` o `sslInvalidHostNameAllowed=true`.
- Callbacks personalizados de validación de certificado.
- `tlsDisableCertificateRevocationCheck=true`.

No se desactivan TLS ni la validación de certificados, no se modifican
certificados de Windows y no se añade un callback propio.

### 3.4 Preflight de solo lectura

`MongoPromotionService.PreflightAsync` lee colecciones, opciones, validadores,
índices y ledger, comprueba con una lectura acotada si cada colección administrada
tiene al menos un documento (`find` con `limit 1`, no un conteo estimado) y calcula
el plan con `MongoMigrationRunner.PlanAsync`. No escribe. Clasifica el destino en:

| Estado | Significado | ¿Promovible? |
|---|---|---|
| `Empty` | No existen colecciones ni ledger. | Sí |
| `NotStarted` | Solo existe el ledger vacío. | Sí |
| `PartiallyApplied` | Hay colecciones/índices de M01 o falta el registro, pero no hay divergencias. | Sí; el runner completa lo pendiente de forma idempotente. |
| `Applied` | Ledger con checksum correcto, colecciones/índices/validador coherentes y 0 escrituras. | Sí; no-op con verificación. |
| `Divergent` | Cualquier condición insegura o desconocida. | No |

Se rechazan, entre otras condiciones:

- Colecciones no administradas por la migración seleccionada.
- Documentos existentes en `cuentas` o `sesiones_refresh`, detectados con una
  lectura exacta y acotada antes de aplicar; la verificación final vuelve a
  comprobarlo para detectar escrituras concurrentes.
- Checksum del ledger distinto del registrado.
- Versiones de ledger duplicadas o no registradas.
- Validador preexistente distinto del declarado, o validador no previsto.
- Índices M01 faltantes con opciones incompatibles o índices peligrosos
  adicionales.
- Validador o índice adicional en `migraciones`.
- Plan de otro destino, de otra versión o con escrituras fuera del contrato.

El estado `Divergent` no se repara automáticamente. Un fallo parcial se reintenta
solo después de revisar `audit`, `plan` y el ledger; nunca se borran colecciones,
índices o registros para “empezar limpio”.

### 3.5 Registro versionado y validadores

`MongoMigrationRegistry`:

- Ordena las migraciones por versión.
- Rechaza versiones duplicadas.
- Rechaza una colección compartida por dos migraciones.
- Reserva `migraciones` para el ledger.
- Expone `GetUpTo` para evolución ordenada.
- Verifica que el checksum del ledger coincida con la definición.

`MongoValidatorDefinition` permite declarar filtro JSON Schema, `validationLevel` y
`validationAction`, canonicaliza el filtro para comparar sin depender del orden de
claves y lo incluye en el checksum de las migraciones nuevas. El runner crea la
colección con el validador y `verify` detecta divergencias. **No se definieron
validadores para M01 ni para las 20 colecciones externas**: hacerlo requiere el
contrato BSON/DTO aprobado de cada módulo.

### 3.6 Bloqueo distribuido

`promote` usa un documento de bloqueo en `migraciones`:

- `_id = "promotion-lock"`.
- Campos `tipo`, `database`, `version`, `ownerId`, `fencingToken`,
  `adquiridoEnUtc`, `renovadoEnUtc` y `expiraEnUtc`.
- Lease de 30 minutos. Solo se toma un bloqueo vencido; un bloqueo activo o
  malformado no se sobrescribe.
- La expiración y la renovación se comparan con el reloj del servidor mediante
  `$$NOW` en `$expr`; no se decide el takeover con el reloj local.
- `fencingToken` se incrementa al tomar un bloqueo vencido. Cada renovación,
  comprobación y liberación exige `_id`, base, versión, `ownerId` y token.
- Un heartbeat renueva el lease cada `lease / 3` (10 minutos con el lease por
  defecto). Antes de cada escritura se comprueba que el bloqueo siga vigente y sea
  del mismo propietario/token. Si la renovación falla, se cancela la operación.
- La liberación elimina únicamente el documento cuyo `ownerId` y `fencingToken`
  coinciden.
- Si falla la liberación, el lease vence y el siguiente intento queda bloqueado
  hasta entonces; no se declara éxito por la liberación.
- La primera adquisición crea `migraciones`. Esa creación es una escritura de
  infraestructura y se contabiliza en el resultado.
- La promoción se adquiere también para un no-op ya aplicado, para que la
  verificación no se solape con otro promovedor. El resultado sigue siendo 0
  escrituras de migración y `createdLedgerCollection=false` en ese caso.

Limitación residual: MongoDB no ofrece una transacción que abarque DDL de
colecciones/índices y el documento de bloqueo. El heartbeat, el token y la
comprobación previa a cada escritura reducen el riesgo de solapamiento, pero si un
proceso queda pausado justo entre la comprobación y una escritura DDL podría existir
una ventana. No se probó contra Atlas real en esta fase; las pruebas usan dobles.

## 4. Comando de promoción

El comando es manual y no se ejecuta desde la API ni durante las pruebas. **No
ejecutar los ejemplos de esta sección sin completar las puertas de la sección 7.**

Configurar la autorización solo en la sesión operativa (sin guardarla en el
repositorio):

```powershell
$env:UNIHOME_PROMOTION_AUTHORIZED_DATABASE = 'unihome_desarrollo'
```

Configurar la credencial exclusiva en el almacén separado. El valor real no debe
pegarse en documentación, historial ni logs:

```powershell
dotnet user-secrets set "MongoDb:MigrationConnectionString" "<URI_TECNICA>" `
  --id unihome-backend-migrations
```

El comando exige simultáneamente destino exacto, confirmación literal,
autorización específica, referencia de aprobación y credencial exclusiva:

```powershell
dotnet run --project tools/UniHome.MongoMigrator/UniHome.MongoMigrator.csproj `
  --configuration Release --no-build -- promote `
  --database unihome_desarrollo `
  --confirm-database unihome_desarrollo `
  --approval-ref "<REFERENCIA_DE_APROBACION>"
```

`--version M01-001` es opcional; el valor predeterminado ya es M01-001. No se
admiten alias ni nombres parciales.

Comandos de solo lectura con la credencial técnica:

```powershell
dotnet run --project tools/UniHome.MongoMigrator/UniHome.MongoMigrator.csproj `
  --configuration Release --no-build -- audit --database unihome_desarrollo --migration-credential

dotnet run --project tools/UniHome.MongoMigrator/UniHome.MongoMigrator.csproj `
  --configuration Release --no-build -- plan --database unihome_desarrollo --migration-credential
```

`verify` contra una base vacía reporta “no registrada”, que es el resultado
esperado antes de promover. No usar `apply` para `unihome_desarrollo`: el guard lo
rechaza.

## 5. Procedimiento de respaldo y restauración

**No se ejecutó ningún respaldo ni restauración en esta fase.** Los comandos son
procedimiento pendiente, no evidencia de ejecución. No declarar un respaldo como
válido hasta restaurarlo y comprobarlo en una base aislada.

### 5.1 Respaldo

1. Registrar fuera del repositorio: fecha, base, versión, checksum del plan y
   responsable de la ventana.
2. Comprobar en Atlas si el tier permite un snapshot bajo demanda. Si existe,
   crearlo y guardar su identificador y hora fuera del repositorio. No asumir que
   el tier actual lo permite.
3. Si no hay snapshot, usar MongoDB Database Tools con una credencial de lectura
   separada. No usar Docker. Inyectar la URI desde un almacén seguro; no escribirla
   en el repositorio, en argumentos compartidos ni en logs.
4. Guardar el dump en una ruta cifrada fuera del repositorio y con ACL restringida.
5. Para una base vacía, el respaldo es principalmente de metadatos; aun así debe
   registrarse y probarse.

Ejemplo de referencia con `mongodump` (no ejecutado):

```powershell
$env:MONGODB_URI = '<URI_DE_LECTURA_SEGURA>'
mongodump --uri="$env:MONGODB_URI" --db=unihome_desarrollo `
  --out="<RUTA_CIFRADA_FUERA_DEL_REPOSITORIO>"
Remove-Item Env:MONGODB_URI
```

### 5.2 Restauración de verificación

Restaurar en una base aislada, nunca en `unihome_desarrollo` ni en una base
operativa. El nombre de destino debe ser nuevo y revisado antes de ejecutar.

Ejemplo de referencia (no ejecutado):

```powershell
$env:MONGODB_URI = '<URI_DE_ESCRITURA_CONTROLADA>'
mongorestore --uri="$env:MONGODB_URI" `
  --nsFrom="unihome_desarrollo.*" `
  --nsTo="unihome_restauracion_20261104.*" `
  --dir="<RUTA_DEL_DUMP>"
Remove-Item Env:MONGODB_URI
```

Después, comprobar de forma read-only que la base aislada tenga las colecciones,
índices, validadores y ledger esperados. `--drop` solo puede considerarse dentro de
la base aislada; **nunca** debe usarse contra `unihome_desarrollo`. Borrar la base
aislada es una operación destructiva que requiere aprobación separada.

### 5.3 Recuperación ante fallo

- Detener el migrador; no ejecutar otro `promote` en paralelo.
- Ejecutar `audit` y `plan` de solo lectura para ver el estado parcial.
- No borrar colecciones, índices ni `migraciones` para “reintentar limpio”.
- Si el ledger es divergente, restaurar el respaldo en una base aislada, verificar
  y decidir con aprobación humana si se restaura el destino o se completa la
  migración.
- Un índice o una colección incompatible se corrige con una migración nueva o una
  intervención aprobada; no editando `M01-001`.
- Registrar el incidente sin secretos, URI, documentos ni datos personales.

## 6. Riesgos y límites vigentes

| ID | Gravedad | Riesgo | Mitigación / estado |
|---|---|---|---|
| P-01 | Alta | La comprobación en vivo no puede conectar: timeout de selección y fallo de negociación TLS/SSPI. | No promover hasta corregir la conectividad. No desactivar TLS, no cambiar certificados ni credenciales de forma especulativa. |
| P-02 | Alta | Solo M01 está versionada; las 20 colecciones externas no tienen migración ni clases C#. | No copiar la base modelo. Crear migraciones por módulo tras aprobar specs, DTOs y BSON. |
| P-03 | Media | El usuario técnico de migración no está verificado y los privilegios mínimos de Atlas no se probaron. | La credencial exclusiva ya está separada en código; falta crear/verificar el usuario y sus roles. |
| P-04 | Alta | No hay respaldo ni restauración de `unihome_desarrollo` verificados. | Snapshot o `mongodump` fuera del repositorio; restaurar en base aislada antes de autorizar. No se ejecutó. |
| P-05 | Alta | El DDL no es transaccional y un fallo deja estado parcial. | Un solo migrador, bloqueo, ventana, respaldo, auditoría y reintento idempotente. No borrar automáticamente. |
| P-06 | Media | Editar `M01-001` cambiaría el checksum y rompería el ledger. | Mantener `M01-001` inmutable; cambios requieren versión nueva. Hay prueba del checksum. |
| P-07 | Media | `unihome_desarrollo` está prohibida en la migración ordinaria. | Se conserva `MongoMigrationGuard`; `promote` es una ruta separada y protegida. |
| P-08 | Media | `audit`, `plan` y `verify` son de solo lectura y pueden apuntar a otra base si se escribe mal el nombre. | Confirmar el destino; `promote` exige el nombre exacto. `apply` sigue bloqueado. |
| P-09 | Media | `cuentas` y `sesiones_refresh` no tienen validador JSON. | Mantener la decisión de M01; no añadir validadores sin contrato aprobado. |
| P-10 | Media | Los 20 validadores `strict/error` pueden rechazar escrituras si los DTOs futuros no coinciden. | Validar cada DTO contra su validador antes del módulo; no relajar el validador para hacer pasar código. |
| P-11 | Media | La autorización técnica existe, pero la aprobación humana real no está registrada. | `promote` exige `UNIHOME_PROMOTION_AUTHORIZED_DATABASE`, confirmación literal y `--approval-ref`; falta la decisión humana. |
| P-12 | Baja | La salida de diagnóstico podría exponer datos. | Solo muestra etapa, tipos, InnerException y HResult; no URI, mensajes, usuarios ni stack traces. |
| P-13 | Baja | El soporte de validadores no tiene contratos para las 20 colecciones externas. | Motor y pruebas listos; no se copiaron validadores de Atlas ni se inventaron contratos. |
| P-14 | Media | El DDL no es transaccional con el bloqueo; el heartbeat reduce pero no elimina la ventana entre comprobar y escribir. | Lease renovado cada 10 minutos, token de fencing y comprobación antes de cada escritura. No probado contra Atlas; no ejecutar otras escrituras durante la ventana. |
| P-15 | Media | La promoción selecciona una versión; el preflight actual solo admite colecciones de M01 más el ledger. | No promover versiones futuras sin extender y probar el alcance ordenado. |
| P-16 | Media | El bloqueo, el heartbeat y la aplicación real no se probaron contra Atlas; las pruebas usan dobles. | Repetir `audit`, `plan` y una prueba controlada en base aislada antes de la ventana real. |

## 7. Comandos de previsualización y puertas

Desde la raíz del repositorio, con conectividad TLS resuelta y credencial técnica
configurada. Son de solo lectura; **no ejecutar `promote` ni `apply`**:

```powershell
dotnet run --project tools/UniHome.MongoMigrator/UniHome.MongoMigrator.csproj `
  --configuration Release --no-build -- audit --database unihome_desarrollo --migration-credential

dotnet run --project tools/UniHome.MongoMigrator/UniHome.MongoMigrator.csproj `
  --configuration Release --no-build -- plan --database unihome_desarrollo --migration-credential
```

Comprobaciones de referencia, sin escrituras:

```powershell
dotnet run --project tools/UniHome.MongoMigrator/UniHome.MongoMigrator.csproj `
  --configuration Release --no-build -- audit --database unihome_modelo_pruebas

dotnet run --project tools/UniHome.MongoMigrator/UniHome.MongoMigrator.csproj `
  --configuration Release --no-build -- verify --database unihome_modelo_pruebas
```

Puertas antes de autorizar la primera escritura:

1. Conectividad TLS/SSPI corregida y `audit`/`plan` reales revisados.
2. Usuario Atlas de migración separado, con privilegios mínimos y sin DDL para la
   API; verificado, no supuesto.
3. Respaldo creado y restauración comprobada en base aislada.
4. Plan de exactamente 8 escrituras para M01 y checksum
   `6FE8415A25A62BFA8C2A6D2F12C591B6645FF388D69E1B5260298B940E08CC4D`.
5. Ventana operativa, un solo migrador y autorización humana registrada fuera del
   repositorio.
6. La API no apunta a development durante la escritura.
7. Después: `verify` válido, readiness y prueba de identidad controlada.

## 8. Pruebas ejecutadas

Resultados reales de la preparación local (sin Atlas y sin escrituras):

| Comprobación | Resultado |
|---|---|
| `dotnet restore UniHome.Backend.slnx --locked-mode` | Correcto; proyectos actualizados |
| `dotnet build UniHome.Backend.slnx -c Release --no-restore -warnaserror` | 0 advertencias, 0 errores |
| `dotnet test` unitarias | 280 aprobadas, 0 fallos |
| `dotnet test` integración con dobles | 56 aprobadas, 1 omitida (Atlas opt-in), 0 fallos |
| `dotnet format UniHome.Backend.slnx --verify-no-changes --no-restore` | Correcto |
| `git diff --check` | Sin errores de whitespace; solo avisos de normalización LF/CRLF |
| `promote` / `apply` sobre `unihome_desarrollo` | No ejecutados |
| Respaldo/restauración | No ejecutados |
| Auditoría/plan real de cierre | Bloqueados por TLS/SSPI |

Cobertura nueva relevante:

- Destino no autorizado, confirmación incorrecta, autorización ausente y
  referencia inválida.
- Credencial exclusiva ausente, igual a la operativa (incluida la misma identidad
  con opciones distintas) y lectura sin fallback.
- TLS deshabilitado, inseguro, certificados/hostnames inválidos y callback
  personalizado.
- Registro ordenado, versiones duplicadas, colecciones compartidas, ledger
  reservado y checksum divergente.
- Base vacía, estado parcial, aplicación completa, checksum distinto, índice
  incompatible, colección no administrada, documentos existentes detectados con
  lectura exacta, validador divergente, ledger con índice adicional y plan de otro
  destino.
- Concurrencia: solo una promoción aplica; la segunda recibe bloqueo.
- Pérdida de lease: el heartbeat falla, cancela la aplicación y el bloqueo se
  libera sin declarar éxito.
- Interrupción: la siguiente ejecución identifica el estado parcial y completa el
  registro pendiente.
- Verificación inválida: no se declara éxito.
- Metadatos de aprobación entregados al registro de promoción (aprobación, base,
  versión, checksum y propietario).
- Preservación del checksum de `M01-001`.

## 9. Criterios de aceptación

- [x] Guard de promoción separado y allowlist ordinaria sin efecto.
- [x] Credencial exclusiva y almacén separado documentado.
- [x] TLS y validación de certificado exigidos sin modificar configuración.
- [x] Registro versionado, checksums inmutables y detección de divergencias.
- [x] Preflight de base vacía/parcial/aplicada/divergente.
- [x] Bloqueo distribuido con fencing, reloj de servidor, heartbeat, liberación
  segura y revalidación antes de cada escritura.
- [x] Aplicación idempotente y verificación posterior.
- [x] Soporte de validadores e índices para migraciones nuevas.
- [x] Pruebas locales de seguridad, concurrencia, recuperación y M01.
- [x] Sin escrituras en `unihome_desarrollo`; sin cambios en `.local-sdd`.
- [x] Sin commit ni push.
- [ ] Conectividad TLS/SSPI restablecida y preflight real repetido.
- [ ] Usuario técnico de migración separado y verificado.
- [ ] Respaldo/restauración verificado.
- [ ] Manifiestos BSON/DTO aprobados para las 20 colecciones externas.
- [ ] Autorización humana explícita para la primera escritura en
  `unihome_desarrollo`.

**Punto de parada:** no ejecutar `apply`, `promote` ni ninguna escritura sobre
`unihome_desarrollo` hasta completar las puertas abiertas y obtener autorización
específica.
