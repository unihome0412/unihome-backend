# M03 — Universidades (HU-075)

## Alcance implementado

- `POST /api/v1/admin/universidades` (EP-M03-02): alta administrativa de una
  universidad con el campo único `nombreYCampus`.
- `GET /api/v1/universidades?buscar=` (EP-M03-01): búsqueda pública de hasta 10
  universidades activas, sin distinguir mayúsculas ni acentos.
- `GET /api/v1/admin/universidades` (EP-M03-03): listado paginado de activas e
  inactivas, con `X-Total-Count`.
- `PUT /api/v1/admin/universidades/{id}` (EP-M03-04): edición de nombre y campus.
- `PUT /api/v1/admin/universidades/{id}/estado` (EP-M03-05): activación o
  desactivación lógica; no existe borrado físico.

## Autorización

El privilegio administrativo es interno y se configura en el servidor:

```text
Admin__AccountIds__0=<GUID de cuenta administradora>
```

La policy `Admin` exige un JWT válido y que el claim `sub` pertenezca a esa
lista. No se acepta un rol enviado por el cliente, no se expone en las respuestas
y una lista vacía deniega todas las rutas `/admin/*`. La pertenencia se revalida
en cada petición; retirar el identificador revoca el acceso sin esperar a que
expire el token.

## Persistencia y migración

Colección: `universidades`.

| Campo | Tipo BSON | Uso |
|---|---|---|
| `_id` | string (Guid N) | Identificador público |
| `nombreYCampus` | string | Nombre y campus en un solo campo |
| `nombreNormalizado` | string | Duplicados y búsqueda sin acentos/mayúsculas |
| `activa` | bool | Solo las activas aparecen en la búsqueda pública |
| `createdAt` | date UTC | Fecha de alta |

Índice obligatorio:

- `ux_universidades_nombre_normalizado`, único sobre `nombreNormalizado`.

Migración versionada: **`M03-001`**. Crea la colección y el índice único. La API
no ejecuta DDL; solo verifica los índices en modo lectura y falla cerrada si
faltan o divergen. HU-077 reutiliza `M03-001`: editar y activar/desactivar no
requieren cambios de esquema, por lo que no se creó otra migración.

```powershell
dotnet run --project tools/UniHome.MongoMigrator -- apply `
  --database unihome_modelo_pruebas --confirm --version M03-001
dotnet run --project tools/UniHome.MongoMigrator -- verify `
  --database unihome_modelo_pruebas --version M03-001
```

El job `mongo-db-test` aplica `M01-001`, `M01-002` y `M03-001` en la base efímera
`unihome_ci_verificacion`, verifica ledger, checksum e índices y ejecuta la prueba
real `MongoUniversityPersistenceTests`, que cubre alta, listado, edición,
duplicado, desactivación, reactivación y búsqueda.

## Riesgo de conciliación con Atlas

El inventario externo observado contiene `universidades` con el índice
`ix_universidades_nombre`, no necesariamente único, y un validador previo. Antes
de habilitar HU-075 en un entorno Atlas se debe revisar y autorizar la
conciliación del índice único y del validador. **No se modificó Atlas ni se
aplicó `M03-001` fuera del MongoDB efímero de CI.**
