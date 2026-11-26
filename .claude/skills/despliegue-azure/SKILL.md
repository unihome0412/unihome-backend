---
name: despliegue-azure
description: "Flujo real usado para construir, subir y desplegar la imagen de UniHome a Azure Container Apps (rg-unihome-dev), con verificación posterior y rollback preparado. Usar cuando se pida desplegar una imagen nueva a Azure o diagnosticar un despliegue que no quedó Healthy."
---

## Cuándo usar esto

Para llevar una imagen nueva de `unihome-api` a la Container App de
`rg-unihome-dev`, o para diagnosticar una revisión que no llegó a `Ready`.
No es para crear infraestructura nueva (eso es Bicep/`az deployment group`,
fuera del alcance de esta skill y con su propio candado de aprobación).

## Candados que no se tocan sin autorización explícita y acotada

`.claude/settings.local.json` deniega por defecto `az containerapp update`,
`az containerapp secret`, `az containerapp revision`, `az containerapp
ingress` y `az containerapp delete` (y `az deployment group create`, para
no crear infraestructura nueva sin revisión). Se destapan temporalmente,
uno por uno y solo los que la ventana necesita, con autorización explícita
de la responsable de la cuenta, y se reponen al cerrar la ventana — nunca
se dejan destapados entre sesiones (precedente: `docs/sprint-08-cierre.md`
§13.4).

## Flujo

1. **Construir y publicar la imagen:**
   ```powershell
   az acr login --name acrunihomedev
   docker build --tag acrunihomedev.azurecr.io/unihome-api:<sha-del-commit> .
   docker push acrunihomedev.azurecr.io/unihome-api:<sha-del-commit>
   ```
   Etiquetar siempre con el SHA del commit que se está desplegando, nunca
   `latest` — es la única forma de saber después qué código corre en
   producción (ver las tablas de verificación de cada
   `docs/sprint-0X-cierre.md`).
2. **Confirmar la imagen en ACR** antes de tocar la Container App, y anotar
   el digest real devuelto por ACR (no inventarlo).
3. **Confirmar el objetivo de rollback**: la imagen que corre sana *antes*
   del despliegue debe seguir presente en ACR. Si no está, no se continúa.
4. **Actualizar la revisión**, con un sufijo que identifique la ventana:
   ```powershell
   az containerapp update --name unihome-api --resource-group rg-unihome-dev `
     --image acrunihomedev.azurecr.io/unihome-api:<sha-del-commit> `
     --revision-suffix <sprintNN-YYYYMMDD>
   ```
   No cambiar variables de entorno, secretos ni el modo de revisión
   (`Single`) a menos que la ventana lo pida explícitamente y por separado.
5. **Verificar, nunca asumir:**
   - Estado de la revisión: `healthState: Healthy`, `runningState:
     RunningAtMaxScale`, `trafficWeight: 100`.
   - `GET /health/live` → `200`.
   - `GET /health/ready` → `200` (este *no* depende de todas las
     migraciones — revisar qué colecciones toca realmente
     `StorageHealthCheck`/`MongoStorageReadinessProbe` antes de bloquear un
     despliegue esperando una migración que el health check ni siquiera
     consulta; fue el criterio real para desplegar Sprint 8 antes de
     M11-001).
   - Rutas nuevas/corregidas de la ventana, sin sesión, con el código
     esperado (p. ej. `401` y no `404`/`503`) — mismo patrón que la tabla de
     verificación de `docs/sprint-08-cierre.md` §13.3.
6. **Dejar el comando de rollback preparado pero no ejecutarlo** salvo que
   la verificación falle:
   ```powershell
   az containerapp update --name unihome-api --resource-group rg-unihome-dev `
     --image acrunihomedev.azurecr.io/unihome-api:<sha-anterior-sano> `
     --revision-suffix rollback-<sprintNN-YYYYMMDD>
   ```
7. **Registrar la ventana** en `docs/sprint-0X-cierre.md` (o un documento de
   despliegue dedicado): comandos ejecutados, digest real, tabla de
   verificación y si se usó o no el rollback. Nunca omitir un paso fallido
   del registro.
8. **Reponer los candados** de `.claude/settings.local.json` que se hayan
   destapado para la ventana.

## Si la revisión no llega a `Ready`

No repetir el despliegue a ciegas. Primero diagnosticar con lectura (logs
de la revisión, `az containerapp revision list`) si la causa es la imagen o
una dependencia externa (ver el incidente de la credencial compartida en
`Memoria.md`: un rollback de control a una imagen que ya corría sana dio el
mismo error, lo que confirmó que la causa no era el código).
