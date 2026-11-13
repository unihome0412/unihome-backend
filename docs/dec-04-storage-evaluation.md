# DEC-04 — Evaluación de almacenamiento de fotografías (solo lectura)

**Fecha:** 13 de noviembre de 2026  
**Suscripción:** `6d553636-41d1-4c29-bbb3-418894b0b659`  
**Ámbito:** Azure, integración con `IUnitPhotoStorage` de M05  
**Estado:** propuesta; **ningún recurso fue creado, modificado ni desplegado**

## 1. Hallazgos reales de la suscripción

| Elemento | Resultado |
|---|---|
| Resource group | `rg-unihome-dev`, East US |
| Storage Accounts | **Ninguna** |
| Proveedor `Microsoft.Storage` | **NotRegistered** |
| Container App | `unihome-api`, East US, ingress externo HTTPS |
| Managed identity | `SystemAssigned`, principal `ed105e32-49aa-4a2e-9e37-b784389ed26a` |
| Roles actuales de la identidad | `AcrPull` sobre el ACR; ningún rol de Storage |
| Container Apps Environment | `cae-unihome-dev`, East US, sin VNet/private endpoint configurado |
| Presupuesto | `unihome-dev-budget`: **USD 25/mes**, alertas 50 %, 80 % y 100 % |
| Gasto actual | USD 1.2557415024 (consulta de presupuesto) |
| Perfil de facturación | Microsoft Azure Plan, cuenta Individual, moneda USD, `spendingLimit=On` |
| Créditos/prepago | **No se identificó saldo de crédito ni sponsorship**; solo el plan de Azure y el presupuesto indicado |

No existe una Storage Account reutilizable. La evaluación concluye que la reutilización no es posible sin crear un recurso nuevo y registrar el proveedor.

## 2. Alternativa recomendada

**Azure Blob Storage, cuenta Standard GPv2, Hot, LRS, East US, contenedor privado**, con identidad administrada y SAS de delegación de usuario de corta duración.

| Decisión | Valor propuesto |
|---|---|
| Región | East US, junto a Container Apps |
| Tipo | StorageV2 / Standard GPv2 |
| Rendimiento | Standard |
| Redundancia | LRS (suficiente para desarrollo; sin SLA de producción crítica) |
| Tier | Hot |
| Contenedor | `unidad-fotos`, privado (`allowBlobPublicAccess=false`) |
| Cifrado | Microsoft-managed keys por defecto; TLS 1.2 mínimo |
| Autenticación backend | Managed identity existente; `DefaultAzureCredential` |
| Acceso de imágenes | SAS de delegación de usuario, solo lectura, por blob, TTL 5–15 min |
| Claves de cuenta | **No** se almacenan ni se usan en código |
| Acceso de red | Sin private endpoint en esta fase (no hay VNet); contenedor privado y HTTPS |

### Flujo propuesto

1. La API valida tamaño, cantidad, firma JPEG/PNG, hash, unidad y titular.
2. La API sube el blob con managed identity y una clave opaca `unidades/{unitId}/{guid}`.
3. Mongo guarda solo metadatos (`storageKey`, MIME, tamaño, SHA-256, orden, principal).
4. Para una tarjeta/ficha, la API comprueba que la unidad sea visible o que el usuario tenga acceso y genera una SAS de lectura para **ese blob**, con expiración corta.
5. Nunca se acepta una `storageKey` del cliente ni se permite listar el contenedor.
6. Si falla la escritura en Mongo, se borra el blob recién subido; si falla el borrado, se registra para reconciliación sin exponer datos.

## 3. RBAC mínimo

La identidad del Container App ya existe. Opción recomendada, una sola asignación:

- `Storage Blob Data Contributor` en el **Storage Account** (incluye lectura/escritura/borrado y `generateUserDelegationKey`).

Opción de mínimo privilegio por contenedor:

- `Storage Blob Data Contributor` en el contenedor `unidad-fotos` para datos.
- `Storage Blob Delegator` en el Storage Account para generar la clave de delegación de usuario.

No se asigna `Owner`, `Contributor` ni `Storage Account Contributor` a la identidad de ejecución. La creación del recurso y la asignación requieren la identidad del operador autorizado, no la identidad de la app.

## 4. Costo estimado

Precios minoristas consultados para East US (API pública de precios de Azure, 13-nov-2026):

| Concepto | Precio unitario | Escenario mensual | Costo |
|---|---:|---:|---:|
| Hot LRS Data Stored | USD 0.0208/GB-mes | 10 GB | USD 0.21 |
| Hot LRS Write Operations | USD 0.05/10 000 | 100 000 escrituras | USD 0.50 |
| Hot LRS Read Operations | USD 0.005/10 000 | 1 000 000 lecturas | USD 0.50 |
| Egress estándar a Internet | primeros 100 GB sin cargo; luego USD 0.087/GB | 100 GB | USD 0.00 |
| **Total estimado** | | | **≈ USD 1.21/mes** |

Escenario conservador de 50 GB, 500 000 escrituras y 5 000 000 lecturas: **≈ USD 3.0/mes**, todavía por debajo del presupuesto de USD 25. No incluye private endpoint ni VNet; si se añadieran, el costo fijo adicional debe cotizarse y autorizarse aparte. No se creó ningún recurso para verificar precios reales de facturación.

## 5. Seguridad y riesgos

| Riesgo | Mitigación propuesta |
|---|---|
| Fuga de fotos privadas | Contenedor privado; SAS por blob; TTL corto; sin listado |
| IDOR de fotografías | Comprobar unidad/visibilidad en servidor antes de firmar; nunca confiar en la URL |
| Claves en código/config | Managed identity; no connection strings ni account keys |
| Archivo malicioso | Validar magic bytes, MIME real, tamaño, cantidad y SHA-256 |
| Blobs huérfanos | Borrado compensatorio y job de reconciliación posterior |
| Hotlinking/abuso de ancho de banda | URLs de corta vida; límites de tamaño; monitoreo de egress |
| Costo | Presupuesto USD 25 y alertas existentes; tamaño máximo por foto |
| Identity propagation | Verificar asignación RBAC y prueba con identidad real antes de producción |
| Sin VNet | No se promete aislamiento de red; se usa contenedor privado y HTTPS |

## 6. Acciones que requieren autorización explícita

Ninguna de estas acciones fue ejecutada:

1. `az provider register --namespace Microsoft.Storage`.
2. Crear Storage Account Standard GPv2/LRS/Hot en East US.
3. Crear el contenedor privado `unidad-fotos` con `allowBlobPublicAccess=false`.
4. Asignar `Storage Blob Data Contributor` o la combinación mínima de roles a la managed identity.
5. Añadir configuración de la API (`PhotoStorage:Provider=AzureBlob`, URL de cuenta y contenedor) y redesplegar Container Apps.
6. Añadir lifecycle/retención, diagnóstico o private endpoint, si se aprueba.
7. Ejecutar la prueba de integración con Storage real y una identidad de prueba.

## 7. Gate

**DEC-04 permanece ABIERTO.** El código de M05 sigue fallando cerrado con `503` cuando no hay proveedor; las pruebas usan un doble exclusivo de test. La evaluación no autoriza por sí sola la creación de recursos ni el despliegue. Se requiere aprobación humana del Gate anterior, incluyendo presupuesto y identidad, antes de implementar/activar el adaptador Azure Blob en producción.

## 8. Alternativa considerada

Proxy de imágenes a través de la API con `Storage Blob Data Reader` y sin SAS. Es más fácil de auditar y permite validar permisos en cada request, pero consume ancho de banda de Container Apps y añade latencia. Para el MVP se prefiere SAS de delegación de usuario por blob; el proxy queda como alternativa si se prohíben URLs temporales.
