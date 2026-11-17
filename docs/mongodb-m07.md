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
M01-001, M01-002, M03-001 ni M05-001; por tanto no cambia sus checksums
históricos. La integración con `main` conserva M01-002 (recuperación de
contraseña) y añade M07-001 al registro, sin reordenar ni reescribir las
migraciones existentes.

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
- Cloudinary es el proveedor vigente de fotografías (DEC-04 cerrado). M07 y M04
  devuelven URLs del proxy de la API cuando está configurado; si falta
  configuración, `fotoPrincipalUrl` es null y `galeriaFotos` queda vacía, sin
  inventar URLs. La prueba real opt-in sigue pendiente de credenciales.
- La ubicación de universidad debe cargarse por un proceso controlado; el
  contrato M03 actual no la administra.

## Checksum y preflight de producción (2026-11-17)

- Versión: **M07-001**.
- Checksum SHA-256 de la definición: **003D4CC39CCB6ACC6E44C8762AEEBD60C417A08C03ABA35B2AE585392F38887E**.
- La prueba unitaria `MigracionesPublicadas_DebenConservarSuChecksum` fija este
  valor junto con los de M01-001, M01-002, M03-001, M05-001 y M06-001. Cambiar
  cualquiera de ellos exige una versión nueva; nunca reescribir la publicada.
- `M07-001` solo declara `favoritos` y sus dos índices. No modifica otras
  colecciones ni los checksums históricos.

Comandos **de solo lectura** para la base autorizada (no escribir en Atlas):

```powershell
dotnet run --project tools/UniHome.MongoMigrator -- audit `
  --database unihome_verificacion --migration-credential
dotnet run --project tools/UniHome.MongoMigrator -- plan `
  --database unihome_verificacion --migration-credential --version M07-001
dotnet run --project tools/UniHome.MongoMigrator -- verify `
  --database unihome_verificacion --migration-credential --version M07-001
```

El preflight debe mostrar `Registrada: True`, `Checksum coincide: True`,
`Índices M07-001 coinciden: True`, `Escrituras pendientes: 0` y
`Resultado: VÁLIDO`. Si la colección o un índice falta, los endpoints de
favoritos fallan cerrados con `503`; no se crean colecciones al arrancar la API.

## Respaldo, promoción y recuperación

1. **Autorización:** obtener aprobación humana con referencia, ventana y
   responsable. `unihome_desarrollo` y `unihome_verificacion` no se promueven
   con `apply`; solo con `promote`, credencial exclusiva y confirmación literal.
2. **Respaldo:** snapshot de Atlas o `mongodump` autorizado, con verificación de
   restauración en una base aislada. No se declara un respaldo válido sin haber
   restaurado y comprobado al menos `favoritos`, `migraciones`, `cuentas`,
   `unidades` y `propiedades`.
3. **Promoción:** ejecutar `promote` únicamente en la ventana autorizada. La
   herramienta valida ledger, checksum, bloqueo distribuido y fencing; no se
   usa `apply` contra una base compartida.
4. **Verificación posterior:** repetir `verify` y una prueba CRUD real de
   favoritos con una cuenta sintética; comprobar que no se modificaron otros
   checksums ni índices.
5. **Recuperación:** si el preflight o la verificación fallan, detener la
   promoción. Para revertir M07, restaurar el snapshot o, si `favoritos` está
   vacía y respaldada, eliminar solo esa colección y el registro `_id=M07-001`
   del ledger con autorización explícita. Nunca borrar ni recrear otras
   colecciones.
6. **Rollback de aplicación:** la API no crea DDL; el rollback de imagen es
   independiente. Una versión anterior seguirá funcionando sin `favoritos` y
   responderá `503` en esos endpoints, sin afectar identidad, publicaciones ni
   verificación documental.

No se aplicó ni promovió M07-001 en Atlas en esta intervención. La ejecución
real de `MongoExplorationPersistenceTests` y de las migraciones queda en el job
manual autorizado `mongo-db-test`; su resultado debe registrarse como evidencia.
