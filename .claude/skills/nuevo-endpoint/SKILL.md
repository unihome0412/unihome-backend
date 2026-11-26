---
name: nuevo-endpoint
description: "Flujo real de este proyecto para agregar o modificar una operación de la API: spec primero, Clean Architecture, pruebas unitarias+integración+contrato OpenAPI, puerta de calidad local, commit en español y PR sin merge. Usar cuando se pida una HU nueva, un endpoint nuevo o cambiar el contrato de uno existente."
---

## Cuándo usar esto

Cuando la tarea implica agregar, cambiar el contrato de, o retirar una
operación de `src/UniHome.Api/Controllers/*`. No es para cambios que no
toquen la superficie HTTP (refactors internos, documentación suelta).

## Flujo

1. **Rama desde `main`**: `feature/<descripcion-corta>` (o `fix/...` si es
   una corrección). Nunca commitear directo a `main`.
2. **Spec antes que código.** Crear o actualizar
   `specs/<MODULO>/<metodo>-<ruta-sin-/api/v1/>.md` con el mismo formato que
   el resto de `specs/`: HU, autenticación/autorización, request/response,
   validaciones, códigos de error, reglas de negocio, controles de
   seguridad aplicados, pruebas que lo cubren. Si el nombre de archivo no
   sigue la convención (`<método>-<ruta con `/` → `-`, sin `{}`>.md`), la
   prueba `SpecsCoverageTests` del Bloque 1 falla en CI.
3. **Dominio → Application → Infrastructure → Api**, en ese orden (Clean
   Architecture, verificado por `ArchitectureTests`):
   - `Domain`: entidad/invariante nueva, sin dependencias externas.
   - `Application`: caso de uso (`<Modulo>/<Accion>/`), puerto en
     `Abstractions/` si necesita persistencia o un servicio externo.
   - `Infrastructure`: adaptador MongoDB si hace falta colección/índice
     nuevo — **siempre** vía una migración versionada en
     `tools/UniHome.MongoMigrator` (ver skill `promocion-migracion`), nunca
     `EnsureCollection` ad hoc. El adaptador debe fallar cerrado (`503`) si
     el esquema no coincide, igual que los módulos existentes.
   - `Api`: `Controller` + `Contracts/<Modulo>/*Request.cs` /
     `*Response.cs`, con los atributos de autorización reales (`[Authorize]`
     y, si aplica, la policy `Admin`).
4. **Pruebas, en este orden de preferencia** (así quedó cada sprint cerrado
   en `docs/sprint-0X-cierre.md`):
   - Unitarias del caso de uso con dobles en memoria
     (`tests/UniHome.UnitTests/Application/<Modulo>`).
   - Integración HTTP end-to-end contra `UniHomeApiFactory`/
     `SwaggerEnabledApiFactory` (`tests/UniHome.IntegrationTests`), cubriendo
     el camino feliz y **todos** los códigos de error listados en la spec
     (401/403/404/409/413/422/503 según aplique). Verificar explícitamente
     que la respuesta nunca exponga datos sensibles (contraseña, correo de
     otro usuario, `storageKey`, etc.) cuando la spec lo prohíba.
   - Si agrega una colección Mongo nueva: pruebas reales opt-in contra
     Mongo (`mongo-db-test` en CI) para el guard de esquema y, si hay
     concurrencia (CAS, cupo), una prueba de condición de carrera.
5. **Actualizar el catálogo OpenAPI real.** El documento se genera en
   runtime (`AddOpenApi` en `Program.cs`); no se edita a mano. Confirmar con
   `OpenApiTests` (`tests/UniHome.IntegrationTests/OpenApiTests.cs`) que la
   ruta nueva aparece con el método, la seguridad y el esquema esperados, y
   actualizar el conteo de operaciones si cambia (hoy 69).
6. **Actualizar `docs/matriz-pruebas-endpoints.md`** con la fila de la
   operación nueva y qué tipos de prueba la cubren.
7. **Puerta de calidad local antes de cualquier commit:**
   ```powershell
   dotnet build UniHome.Backend.slnx -warnaserror
   dotnet format --verify-no-changes
   dotnet test UniHome.Backend.slnx
   ```
8. **Commit en español**, mensaje en modo imperativo describiendo el *por
   qué* (ver estilo real en `git log`), nunca con `--no-verify`.
9. **PR sin merge automático.** Abrir con `gh pr create`; el merge lo decide
   la responsable de la cuenta. Nunca `gh pr merge`.
10. **Si el endpoint es sensible** (admin, datos personales, dinero,
    documentos): pasar también por la skill `revision-seguridad` antes de
    pedir el merge.
