# Preparación de CD hacia Azure Container Apps

**Fecha de auditoría:** 2026-11-10. **Estado:** preparado, **no desplegado**.
Esta auditoría no autenticó Azure, no creó recursos, no publicó imágenes, no
cambió permisos y no reinició Container Apps.

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

1. Descarga la imagen `ci-container-image` construida y auditada por Dockle.
2. Autentica con OIDC.
3. Publica `acrunihomedev.azurecr.io/unihome-api:<GITHUB_SHA>`.
4. Captura la imagen y revisión anteriores.
5. Actualiza `unihome-api` con una revisión `sha-<run-id>`.
6. Consulta el FQDN y prueba `/health/live` y `/health/ready` por HTTPS.
7. Si los health checks fallan, restaura la imagen anterior y falla el job.
8. Publica `azure-cd-evidence` con imagen, revisión y resultado.

No se ejecutó ninguno de estos pasos en Azure.

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
- YAML parse: correcto.
- `dotnet build UniHome.Backend.slnx -c Release --no-restore -warnaserror`: correcto.
- Pruebas unitarias/integración pertinentes: correctas; 2 integraciones omitidas
  por el opt-in de MongoDB real.
- `git diff --check`: correcto.
- No se ejecutó `azure/login`, `docker push`, `az containerapp update` ni
  ninguna operación de escritura.
- Ejecución manual de verificación
  [42856630200](https://github.com/unihome0412/unihome-backend/actions/runs/42856630200):
  workflow completo **success**; `deploy-azure` **skipped** por
  `deploy_to_azure=false` y `UNIHOME_ENABLE_AZURE_CD` ausente. PR #18 con todos
  los checks aplicables en verde.

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
