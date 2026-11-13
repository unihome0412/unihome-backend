# Readiness de despliegue — Cloudinary, Atlas verificacion y Redis

**Estado:** preparación técnica. **No se ejecutó ninguna escritura en Atlas, no
se creó ninguna revisión de Container Apps y no se habilitó CD.**

API existente: `unihome-api` en `rg-unihome-dev`.
Base configurada actualmente: `unihome_verificacion`.

## 1. Orden de migraciones

El registro versionado queda ordenado así:

1. `M01-001`: `cuentas` y `sesiones_refresh`.
2. `M01-002`: `restablecimientos_contrasena`.
3. `M03-001`: `universidades`.
4. `M05-001`: `propiedades`, `unidades` y `aceptaciones_terminos`.

`M01-002` crea la colección de recuperación con:

- `ux_restablecimientos_token` único sobre `tokenHash`.
- `ix_restablecimientos_cuenta` sobre `accountId`.
- `ttl_restablecimientos_expires` TTL 0 sobre `expiresAt`.
- Validador `strict/error`: `expiresAt`, `createdAt` date; `consumedAt`
  date/null; `tokenHash` string; `accountId` string/ObjectId/binData.

No se modifican `M01-001`, `M03-001` ni `M05-001`. Sus checksums deben permanecer
iguales.

## 2. Regla de promoción incremental

`promote` solo acepta dos destinos fijos:

- `unihome_desarrollo`
- `unihome_verificacion`

No se acepta un nombre arbitrario desde la CLI. Además se exige:

- `--confirm-database` con el valor exacto.
- `UNIHOME_PROMOTION_AUTHORIZED_DATABASE` con el valor exacto del destino.
- `--approval-ref` no secreto.
- Credencial exclusiva `MongoDb:MigrationConnectionString` en
  `UserSecretsId=unihome-backend-migrations`.
- TLS con validación de certificado.

La promoción exige que **todas las migraciones anteriores** estén aplicadas y
coincidan en checksum, colecciones, índices y validador. Solo la migración
seleccionada puede estar pendiente. Los datos existentes en migraciones
anteriores se permiten; la migración seleccionada debe estar vacía.

Por eso, en `unihome_verificacion` el orden es:

```text
M01-001 (ya aplicada y válida)
M01-002
M03-001
M05-001
```

No se debe promover `M03-001` antes de `M01-002`.

## 3. Respaldo y verificación previa

No ejecutar `promote` sin respaldo y restauración de verificación.

1. Si Atlas permite snapshot bajo demanda, crearlo y registrar su identificador
   fuera del repositorio.
2. Si no, usar una credencial de lectura separada y `mongodump` a una ruta
   cifrada fuera del repositorio. No usar Docker.
3. Restaurar en una base aislada nueva, por ejemplo
   `unihome_restauracion_<fecha>`, y verificar M01/M01-002/M03/M05.
4. No restaurar sobre `unihome_verificacion` ni `unihome_desarrollo`.

Comandos de solo lectura:

```powershell
dotnet run --project tools/UniHome.MongoMigrator/UniHome.MongoMigrator.csproj `
  --configuration Release --no-build -- `
  audit --database unihome_verificacion --migration-credential

dotnet run --project tools/UniHome.MongoMigrator/UniHome.MongoMigrator.csproj `
  --configuration Release --no-build -- `
  plan --database unihome_verificacion --migration-credential --version M01-002

dotnet run --project tools/UniHome.MongoMigrator/UniHome.MongoMigrator.csproj `
  --configuration Release --no-build -- `
  plan --database unihome_verificacion --migration-credential --version M03-001

dotnet run --project tools/UniHome.MongoMigrator/UniHome.MongoMigrator.csproj `
  --configuration Release --no-build -- `
  plan --database unihome_verificacion --migration-credential --version M05-001
```

El plan debe mostrar únicamente las colecciones/índices de la versión seleccionada
y el registro de ledger; cualquier colección externa es divergencia.

## 4. Promoción autorizada (solo después del Gate)

Con la credencial técnica ya cargada de forma segura y la autorización humana
registrada:

```powershell
$env:UNIHOME_PROMOTION_AUTHORIZED_DATABASE = 'unihome_verificacion'

dotnet run --project tools/UniHome.MongoMigrator/UniHome.MongoMigrator.csproj `
  --configuration Release --no-build -- promote `
  --database unihome_verificacion `
  --confirm-database unihome_verificacion `
  --approval-ref "<REFERENCIA_DE_APROBACION>" `
  --version M01-002
```

Repetir para `M03-001` y `M05-001` solo después de verificar el paso anterior.
No ejecutar los tres comandos en paralelo. Un solo migrador y una sola ventana.

Después de cada paso:

```powershell
dotnet run --project tools/UniHome.MongoMigrator/UniHome.MongoMigrator.csproj `
  --configuration Release --no-build -- `
  verify --database unihome_verificacion --migration-credential --version <VERSION>
```

No borrar colecciones, índices ni el ledger para “reintentar limpio”. Si hay una
divergencia, detenerse y auditar.

## 5. Redis/Upstash en Production

El backend exige Redis fuera de Development. La Container App está en
`Production`. Sin esto la imagen nueva no arranca.

Variables necesarias:

- `Redis__Enabled=true`.
- Una de estas formas:
  - `Redis__RestUrl` + `Redis__RestToken`, o
  - `Redis__ConnectionString` con URI `rediss://` de Upstash (el adaptador extrae
    host y token para su API REST HTTPS).
- `Auth__RateLimit__IpHashSalt`, mínimo 16 caracteres; usar un valor aleatorio de
  32+ caracteres.
- Opcional: `Redis__CommandTimeoutMs`; el valor por defecto es 250 ms.
- La política por defecto es `FailClosed`; no cambiarla a `FailOpen` sin
  aprobación de seguridad.

Procedimiento si no existe la base Upstash:

1. Crear/obtener una base Upstash Redis **Free** con TLS.
2. Copiar el endpoint REST y el token, o la URI `rediss://`, a un almacén seguro.
3. Crear secretos en la Container App existente para el token/URI y para la sal
   de IP. No reutilizar la sal de otro entorno.
4. Referenciar los secretos desde las variables anteriores.
5. Validar arranque y una operación de login real; `/health/ready` no comprueba
   Redis por sí solo.
6. Verificar que un fallo de Redis devuelve 503 y no crea contadores locales.

No crear otra Container App, otro servicio Azure ni contratar un plan pago.

## 6. Cloudinary

Al cierre del preflight, la Container App ya conten?a los nombres de secretos
`cloudinary-cloud-name`, `cloudinary-api-key`, `cloudinary-api-secret`,
`upstash-rest-url`, `upstash-rest-token` y `auth-rate-limit-ip-hash-salt`. No se
crearon, modificaron ni leyeron sus valores. Las variables de entorno a?n no los
referencian. Referencias previstas (sin recrear secretos):

```text
Cloudinary__CloudName=secretref:cloudinary-cloud-name
Cloudinary__ApiKey=secretref:cloudinary-api-key
Cloudinary__ApiSecret=secretref:cloudinary-api-secret
Cloudinary__PublicBaseUrl=https://unihome-api.agreeablesky-4cd63e24.eastus.azurecontainerapps.io
Redis__Enabled=true
Redis__RestUrl=secretref:upstash-rest-url
Redis__RestToken=secretref:upstash-rest-token
Auth__RateLimit__IpHashSalt=secretref:auth-rate-limit-ip-hash-salt
```

`Cloudinary__PublicBaseUrl` es la base HTTPS que el proxy devuelve a los
clientes. No es un secreto. No se expone ninguna URL directa ni firma de
Cloudinary.

La validación real debe ejecutarse contra la API desplegada: subir, leer mediante
`GET /api/v1/fotos/{photoRef}` y eliminar el activo de prueba. Una prueba local
opt-in no sustituye esta comprobación.

## 7. Requisitos de arranque

| Requisito | Estado actual |
|---|---|
| `MongoDb__ConnectionString` | Presente como secreto |
| `MongoDb__DatabaseName=unihome_verificacion` | Presente |
| `Jwt__SigningKey` | Presente como secreto |
| `VerificationCode__HmacSecret` | Presente como secreto |
| SMTP | Presente |
| `Proxy__TrustForwardedHeaders=true` | Presente |
| `Redis__*` | **Faltante** |
| `Auth__RateLimit__IpHashSalt` | **Faltante** |
| Cloudinary env refs | **Faltantes** |
| `Cloudinary__PublicBaseUrl` | **Faltante** |
| `PasswordRecovery__ResetLinkBaseUrl` | Faltante; necesario para recuperación |
| `Admin__AccountIds` | Faltante; no hay M06, no inventar |

`Api__EnableSwagger=true` está activo en Production; se recomienda cambiarlo a
`false` en la ventana de configuración.

## 8. Gate de despliegue

No se ejecuta hasta recibir autorizaciones independientes para:

1. Modificar Atlas.
2. Crear/configurar Upstash.
3. Cambiar variables/secretos de Container Apps.
4. Habilitar CD.
5. Desplegar imagen.
6. Ejecutar pruebas que escriban datos reales.

La secuencia de despliegue, rollback y pruebas HTTPS está en el Gate entregado
por la sesión; este documento no autoriza ninguna escritura.
