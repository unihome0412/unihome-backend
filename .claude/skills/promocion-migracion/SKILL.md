---
name: promocion-migracion
description: "Flujo real y con candados para auditar, planear, verificar y promover una migración de UniHome.MongoMigrator contra una base real (Atlas). Usar cuando se pida crear una colección/índice nuevo en Mongo o llevar una migración ya escrita a un entorno real."
---

## Cuándo usar esto

Cuando una migración versionada (`tools/UniHome.MongoMigrator`) ya existe y
se necesita verificarla o promoverla contra una base real, o cuando hay que
diagnosticar por qué una promoción fue rechazada. Para crear la migración en
sí, ver primero la skill `nuevo-endpoint`.

## Candados que no se tocan sin autorización explícita y acotada

`dotnet run --project tools/UniHome.MongoMigrator*promote*` está en el
`deny` de `.claude/settings.local.json`. Si una ventana de trabajo real lo
necesita, se quita temporalmente con autorización explícita de la
responsable de la cuenta y se repone al cerrar la ventana (así se hizo en
`docs/sprint-08-cierre.md` §13.4) — nunca se deja destapado "por si acaso".

## Flujo de solo lectura (siempre primero, nunca se salta)

```powershell
dotnet run --project tools/UniHome.MongoMigrator --configuration Release --no-build -- `
  audit --database <base> --migration-credential

dotnet run --project tools/UniHome.MongoMigrator --configuration Release --no-build -- `
  plan --database <base> --migration-credential

dotnet run --project tools/UniHome.MongoMigrator --configuration Release --no-build -- `
  verify --database <base> --version <M0X-00Y> --migration-credential
```

- `audit`: lista colecciones reales vs. ledger de migraciones aplicadas.
- `plan`: muestra las escrituras pendientes de una versión, sin ejecutarlas.
- `verify` sobre una migración no aplicada debe reportar "no registrada" —
  ese es el resultado esperado antes de promover, no un error.
- Ninguno de los tres escribe. Si alguno falla por conectividad (TLS/SSPI),
  se detiene ahí: no se reintenta con TLS relajado ni credenciales nuevas
  especulativas (riesgo P-01 de `docs/mongodb-promocion.md`).

## Promoción (escribe; requiere todo lo siguiente a la vez)

```powershell
dotnet run --project tools/UniHome.MongoMigrator --configuration Release --no-build -- promote `
  --database <base> --confirm-database <base> --approval-ref "<referencia-de-aprobacion>" `
  --version <M0X-00Y>
```

Antes de ejecutarlo:

1. **Aprobación humana registrada** con una referencia real (no inventar un
   `--approval-ref`; debe trazarse a una decisión de la responsable de la
   cuenta).
2. **Credencial exclusiva de migración**, nunca la misma identidad que usa
   la API en producción — el incidente de `Memoria.md` ("la contraseña
   compartida entre el migrador y la API") pasó exactamente por reusar un
   usuario de Atlas entre ambos.
3. **Respaldo o snapshot verificado** si la base no está vacía (ver §5 de
   `docs/mongodb-promocion.md`); no declarar un respaldo válido sin haberlo
   restaurado y comprobado en una base aislada.
4. Revisar si el preflight puede rechazar la promoción por una migración
   *posterior* ya aplicada (el caso de M11/M12 en `Memoria.md`): correr
   `audit` primero y confirmar con qué versiones ya están en el ledger.

## Si el preflight rechaza la promoción

No se diagnostica contra Atlas con escrituras de prueba ni imprimiendo
datos sensibles. El patrón ya usado (PR #66): agregar temporalmente un
`Console.Error.WriteLine` de las *issues* del preflight directamente en el
código (nunca en una copia desplegada), reconstruir localmente, confirmar
la causa exacta, y revertir el cambio de diagnóstico antes de cualquier
escritura real. Corregir con una prueba que reproduzca el caso exacto
(`MongoPromotionIncrementalTests` es el precedente) antes de reintentar la
promoción.

## Después de promover

Correr `verify` de la versión promovida contra la base real y registrar el
resultado (colecciones, índices, checksum) en `docs/sprint-0X-cierre.md` o
en el documento de promoción correspondiente, igual que las ventanas
anteriores. Nunca declarar una promoción exitosa solo porque el comando no
devolvió error: verificar explícitamente.
