# DEC-04 — Evaluación histórica de almacenamiento de fotografías

**Estado:** **CERRADA / SUPERADA.** El proveedor vigente es **Cloudinary Free**,
documentado en [`dec-04-cloudinary.md`](dec-04-cloudinary.md). Esta evaluación se
conserva solo como registro histórico y **no es una propuesta de implementación**.

**Fecha original:** 13 de noviembre de 2026
**Ámbito:** Azure, integración con `IUnitPhotoStorage` de M05
**Resultado:** no se creó, modificó ni desplegó ningún recurso Azure.

## 1. Qué se evaluó

Cuando M05 todavía no tenía proveedor de fotografías se analizó Azure Blob Storage
como alternativa:

- Resource group `rg-unihome-dev`, East US, sin Storage Accounts.
- Proveedor `Microsoft.Storage` no registrado.
- Container App `unihome-api` con identidad administrada y rol `AcrPull`, sin
  roles de Storage.
- Presupuesto `unihome-dev-budget` de USD 25/mes.

La conclusión técnica fue que reutilizar una cuenta existente no era posible sin
crear un recurso nuevo y registrar el proveedor.

## 2. Decisión vigente: Cloudinary, no Azure Blob

El usuario aprobó **Cloudinary Free** para el MVP. La implementación vigente:

1. Sube las fotos como `authenticated` con `public_id` opaco.
2. Elimina EXIF/GPS con `fl_strip_profile`.
3. Guarda en MongoDB solo metadatos; nunca el binario.
4. Entrega las imágenes mediante el proxy autorizado
   `GET /api/v1/fotos/{photoRef}`, no con una URL firmada de Cloudinary.
5. Falla cerrado con `503` si faltan `Cloudinary__CloudName`,
   `Cloudinary__ApiKey` o `Cloudinary__ApiSecret`.
6. En Development permite un almacenamiento efímero **solo** para los harnesses
   DAST/RESTler; en Production ese flag se ignora.

Detalle completo, límites del plan Free, configuración y riesgos en
[`dec-04-cloudinary.md`](dec-04-cloudinary.md).

## 3. Acciones Azure que NO se ejecutaron

Ninguna de estas acciones forma parte de Sprint 3 ni está autorizada por esta
evaluación:

1. `az provider register --namespace Microsoft.Storage`.
2. Crear una Storage Account Standard GPv2/LRS/Hot.
3. Crear el contenedor `unidad-fotos`.
4. Asignar roles de Storage a la identidad administrada.
5. Añadir configuración `PhotoStorage:Provider=AzureBlob` ni desplegar Container
   Apps.
6. Crear lifecycle, diagnóstico, private endpoint o VNet.
7. Ejecutar pruebas de integración con Storage real.

No se modificaron Azure Container Apps, secretos, variables de GitHub ni Atlas
productivo.

## 4. Consecuencia para M04/M07

- `fotoPrincipalUrl` y `galeriaFotos` usan el proxy de Cloudinary cuando está
  configurado.
- Si el proveedor no está configurado, M04 devuelve `null`/lista vacía y no
  inventa URLs; M05 responde `503` en la subida.
- La prueba real opt-in de Cloudinary permanece **PENDIENTE** de credenciales
  seguras; no se declara validada en producción.

**DEC-04 ya no bloquea el contrato de M04/M07.** Cualquier cambio futuro de
proveedor requiere una nueva decisión y una evaluación de costo/seguridad.
