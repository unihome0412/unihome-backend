# Preparación de CD hacia Azure Container Apps

**Fecha de auditoría:** 2026-11-10. **Estado:** preparado, **no desplegado**.
**El flujo de despliegue todavía no se ha probado en Azure.** Esta auditoría no
autenticó Azure, no creó recursos, no publicó imágenes, no cambió permisos y no
reinició Container Apps.

## Evidencia de preflight

- `origin/main` apunta a `0709042`; PR #17 (OpenAPI/Pact) está integrado.
- El job `deploy-azure` de `.github/workflows/ci.yml` es manual y está
  desactivado por `UNIHOME_ENABLE_AZURE_CD` y por el input
  `deploy_to_azure=false`.
- El environment `azure-dev` existe y su política de ramas permite `main`.
- El environment tiene los secrets `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` y
  `AZURE_SUBSCRIPTION_ID`; no se leyeron sus valores.
- La CLI de Azure está instalada, pero **no hay sesión iniciada**. No se pudo
  confirmar por API la existencia de `rg-unihome-dev`, `acrunihomedev`,
  `cae-unihome-dev` ni `unihome-api`.
- La URL histórica
  `https://unihome-api.agreeablesky-4cd63e24.eastus.azurecontainerapps.io`
  respondió `200` en `/health/live` y `/health/ready` el 2026-11-10. Esto
  demuestra un endpoint público alcanzable, **no** confirma los nombres de los
  recursos ni la identidad federada.
- No existe la variable de repositorio `UNIHOME_ENABLE_AZURE_CD`.

## Configuración OIDC esperada

El job usa `azure/login` con OIDC y no debe usar secretos de cliente:

| Elemento | Valor esperado |
|---|---|
| Emisor | `https://token.actions.githubusercontent.com` |
| Audiencia | `api://AzureADTokenExchange` |
| Subject | `repo:unihome0412/unihome-backend:environment:azure-dev` |
| Secretos del environment | `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` |
| Permisos del job | `id-token: write`, `contents: read`, `actions: read` |

El subject exacto debe verificarse en la identidad federada de Azure. Si se usa
una federated credential por rama, el subject sería
`repo:unihome0412/unihome-backend:ref:refs/heads/main`; el job está preparado
para el subject de environment `azure-dev`.

### Permisos mínimos propuestos

- Identidad de GitHub Actions:
  - `AcrPush` sobre `acrunihomedev`.
  - `Container Apps Contributor` sobre `unihome-api` o, si se requiere crear
    revisiones y leer el entorno, sobre `rg-unihome-dev`.
  - `Reader` sobre el alcance necesario para consultar la imagen/revisión/FQDN.
- Identidad administrada de la Container App:
  - `AcrPull` sobre `acrunihomedev`, para descargar la imagen sin usar el
    usuario/contraseña de administrador del registro.

No se concedieron roles en esta tarea. El documento histórico usa
`--admin-enabled true` y credenciales de ACR; eso debe revisarse antes de
producción. Se recomienda deshabilitar el usuario administrador después de
asignar `AcrPull` a la identidad administrada.

## Protecciones GitHub

- **Variable:** `UNIHOME_ENABLE_AZURE_CD=true` (ausente actualmente).
- **Evento:** solo `workflow_dispatch`.
- **Rama:** solo `refs/heads/main`; `azure-dev` tiene branch policy `main`.
- **Input:** `deploy_to_azure=true`, explícito y por defecto `false`.
- **Dependencias:** build, contrato OpenAPI, Docker/Dockle, PR Quality Gate y
  Hardening Report deben estar en `success`.
- **Environment:** `azure-dev`.
- **Permisos:** el job eleva únicamente `id-token: write` y `actions: read`; el
  workflow global conserva `contents: read`.
- **PR/push:** el job no se ejecuta (condición de evento/rama/input).
- **Aprobación humana:** el environment existe, pero **no tiene required
  reviewers** en la configuración consultada. Debe añadirse un revisor antes de
  autorizar el primer despliegue. El input manual no sustituye esa aprobación.

## Flujo CD preparado

1. Descarga la imagen `ci-container-image` construida y auditada por Dockle,
   usando `actions/download-artifact` fijado por SHA
   `d3f86a106a0bac45b974a628896c90dbdf5c8093` (v4.3.0, verificado contra el
   tag); es compatible con el artefacto v4 que publica
   `actions/upload-artifact` `ea165f8d65b6e75b540449e92b4886f43607fa02`
   (v4.6.2; ambos usan el protocolo de artefactos v4).
2. Autentica con OIDC.
3. Publica `acrunihomedev.azurecr.io/unihome-api:<GITHUB_SHA>` completo.
4. Captura la imagen, revisión y FQDN anteriores, exige
   `activeRevisionsMode=Single` y que la revisión anterior esté sana antes de
   actualizar.
5. Actualiza `unihome-api` con una revisión `sha-<run-id>`.
6. Espera el estado `Running` de la revisión nueva y consulta su FQDN.
7. Prueba `/health/live` y `/health/ready` por HTTPS.
8. Ante fallo de query, estado o health, restaura la imagen anterior y falla.
9. Publica `azure-cd-evidence` con tag SHA, digest, revisión y resultado.

No se ejecutó ninguno de estos pasos en Azure.

### Concurrencia

El workflow separa los grupos de concurrencia:

- CI: grupo `ci` para PR, push y dispatch sin input; conserva la cancelación
  de ejecuciones CI obsoletas.
- CD manual: el input verdadero usa el grupo `cd` y `cancel-in-progress: false`.
- El job `deploy-azure` tiene además su propio grupo
  `azure-dev-deploy-<ref>` con cancelación deshabilitada.

Así, un push a `main` no puede cancelar un despliegue en progreso. Dos deploys
manuales se serializan; el segundo espera.

### Rollback y límites

- Se exige `activeRevisionsMode=Single` y que la revisión anterior responda
  health checks (con reintentos acotados) antes de actualizar.
- Se captura `PREVIOUS_IMAGE`, `PREVIOUS_REVISION` y `PREVIOUS_FQDN`.
- La revisión nueva debe alcanzar `runningState=Running`; si no, se revierte.
- Cualquier fallo de query FQDN, estado o health dentro del paso dispara el trap
  `EXIT` y restaura la imagen anterior.
- Tras restaurar, se consulta el FQDN de rollback y se repiten health checks;
  si tampoco pasan, el job falla y conserva el error. No se declara éxito.
- **Límite:** una cancelación externa del job, terminación del runner o corte
  de red puede impedir que se ejecute el trap. En ese caso la revisión debe
  revisarse y recuperarse manualmente con autorización; no se promete rollback
  atómico ante cancelaciones externas.

### Trazabilidad de imagen

- El tag usa el SHA completo del commit.
- El digest se captura del resultado de `docker push` o de `RepoDigests` y se
  publica en la evidencia. Si no se puede capturar, el job falla.
- **ACR no garantiza por sí solo que un tag sea inmutable.** No se afirma
  inmutabilidad ni se activa tag locking/immutable tags: hacerlo requiere
  autorización y cambio de configuración en Azure. La retención/limpieza de
  imágenes también requiere una decisión posterior.

## FinOps

Los importes son **estimaciones de planificación**, no una garantía de costo ni
una cotización. Deben validarse con Azure Pricing Calculator y la suscripción
real antes de autorizar.

### Supuestos

- Container Apps Consumption, `0.25 vCPU`, `0.5 GiB`, `1..1` réplicas, 730 h/mes.
- ACR Basic, una imagen por SHA y retención limitada.
- Log Analytics con retención de 30 días y tráfico de demostración.
- MongoDB Atlas es un servicio externo; su costo no se estima aquí.

### Estimación gruesa

| Servicio | Consumo previsto | Estimación mensual orientativa |
|---|---|---|
| Container Apps | 0.25 vCPU/0.5 GiB, 1 réplica permanente | ~US$10–15 antes de aplicar créditos/franquicias |
| ACR Basic | registro y almacenamiento básico | ~US$5 |
| Log Analytics | ingesta/retensión pequeña de demo | ~US$1–5 |
| Egress/tráfico | bajo para una demo | ~US$0–5 |
| MongoDB Atlas | fuera de Azure | según plan contratado; no asumido |
| **Total Azure aproximado** | sin Atlas | **~US$16–25/mes** |

La estimación no incluye impuestos, soporte, tráfico extraordinario, builds,
réplicas adicionales ni cargos de la suscripción. No se puede garantizar costo
cero. Los créditos estudiantiles pueden cubrirlo o no; solo la facturación real
lo confirma.

### Recursos que consumen en reposo

- ACR Basic y sus imágenes.
- Log Analytics y la retención de logs.
- Container Apps con `min-replicas=1`.
- Atlas, si está contratado fuera de Azure.

### Control de consumo recomendado

- Configurar un presupuesto de suscripción o resource group con alertas al 50 %,
  80 % y 100 % de un importe aprobado explícitamente. No se creó ninguno.
- Mantener ACR Basic, no subir a Standard/Premium sin justificación.
- Eliminar imágenes antiguas y revisiones inactivas con una retención definida.
- Para una demo, evaluar `min-replicas=0` y aceptar cold start; el valor actual
  documentado es `1`.
- Al terminar la entrega, apagar o eliminar los recursos con autorización
  explícita; no ejecutar borrados automáticos desde CI.
- Revisar la facturación de Atlas por separado.

## Validaciones realizadas

- `actionlint .github/workflows/ci.yml`: correcto.
- YAML parse: correcto (`17` jobs).
- `python -m unittest discover -s tools/ci -p "test_*.py"`: **111 pruebas
  correctas**, incluidas 17 pruebas del contrato CD y 100 % de cobertura de
  `tools/ci/azure_cd_guard.py`.
- `dotnet build UniHome.Backend.slnx -c Release --no-restore -warnaserror`:
  0 advertencias, 0 errores.
- `dotnet format --verify-no-changes`: correcto.
- Pruebas .NET: unitarias 309/309 correctas; integración 103 correctas y 2
  omitidas por el opt-in de MongoDB real (las omitidas no se presentan como
  aprobadas).
- `git diff --check`: correcto.
- No se ejecutó `azure/login`, `docker push`, `az containerapp update` ni
  ninguna operación de escritura en Azure.
- PR #18, head `8cad47f01b2d2280014cf788ef683d62b7622ec6`:
  [run 42864050099](https://github.com/unihome0412/unihome-backend/actions/runs/42864050099)
  **success**; `deploy-azure` **skipped**. CodeQL
  [run 42864050256](https://github.com/unihome0412/unihome-backend/actions/runs/42864050256)
  **success**; PR **CLEAN** y **MERGEABLE**, sin merge.
- `workflow_dispatch` de verificación con `deploy_to_azure=false`:
  [run 42864581083](https://github.com/unihome0412/unihome-backend/actions/runs/42864581083)
  **success**; `deploy-azure` **skipped**. El Hardening Report registró
  `skipped` como no aprobado y el Quality Gate pasó.
- **No se probó un despliegue real en Azure.**

## Pasos exactos para autorizar el primer despliegue

1. Con una cuenta autorizada, ejecutar únicamente consultas de lectura:
   `az account show`, `az group show -n rg-unihome-dev`, `az acr show -n
   acrunihomedev`, `az containerapp show -n unihome-api -g rg-unihome-dev` y
   `az role assignment list`.
2. Verificar que la identidad federada tenga issuer, audience y subject
   `repo:unihome0412/unihome-backend:environment:azure-dev`.
3. Agregar required reviewers al environment `azure-dev`; mantener la branch
   policy `main`.
4. Asignar `AcrPush` a la identidad de GitHub Actions y `Container Apps
   Contributor` sobre el recurso o resource group; asignar `AcrPull` a la
   identidad administrada de la Container App.
5. Verificar que ACR no dependa del usuario administrador para pulls.
6. Configurar un presupuesto y alertas aprobados; no crear sin autorización.
7. Crear la variable `UNIHOME_ENABLE_AZURE_CD=true` solo tras la revisión.
8. Ejecutar `workflow_dispatch` desde `main` con
   `deploy_to_azure=true`, aprobar el environment y revisar el artefacto
   `azure-cd-evidence`.

No ejecutar ninguno de estos pasos sin autorización humana específica.
