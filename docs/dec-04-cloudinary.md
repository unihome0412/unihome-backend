# DEC-04 — Cloudinary para fotografías de unidades

**Estado:** proveedor elegido por el usuario: **Cloudinary Free**  
**Rama de implementación:** `feature/dec-04-cloudinary-storage`  
**Alcance:** sustituir el fail-closed de `IUnitPhotoStorage` por almacenamiento y entrega reales, sin cambiar el endpoint `POST /api/v1/unidades/{unitId}/fotos`.

> Esta decisión reemplaza la recomendación Azure Blob Storage que figura en la
> evaluación histórica de PR #35. Esa evaluación no se reescribe en este PR; la
> conciliación documental de PR #35 queda pendiente para después de integrar
> Cloudinary, tal como se solicitó. Azure no fue modificado.

## 1. Decisión y razón

Cloudinary Free fue aprobado explícitamente por el usuario. Se elige porque:

- Ya existen Cloud name, API Key y API Secret bajo control del usuario.
- No requiere crear recursos Azure ni habilitar presupuesto/capacidad adicional.
- El SDK oficial .NET está disponible y el plan Free no caduca.
- Permite delivery **authenticated**, de modo que el original y las versiones
  derivadas requieren autorización firmada.
- La entrega se puede intermediar por la API, sin exponer la URL firmada ni las
  credenciales al cliente.

## 2. Límites del plan Free

Cloudinary factura por créditos. Según la documentación vigente:

- **25 créditos** en ventana móvil de 30 días.
- 1 crédito equivale a **1 GB de almacenamiento**, **1 GB de bandwidth** o
  **1 000 transformaciones**.
- El almacenamiento es el total actual, no una ventana móvil.
- No se usan add-ons, token-based access, cookies, private CDN ni funciones de
  planes pagos.
- Si se agotan los créditos, Cloudinary puede rechazar transformaciones o
  entregas; la API debe responder de forma controlada (`503`) y nunca simular
  una subida exitosa.

Con fotos de hasta 5 MB y 10 por unidad, 25 GB de almacenamiento equivalen a un
máximo teórico de ~5 000 fotos antes de consumir otros créditos de bandwidth o
transformaciones. El límite real debe monitorearse en la consola de Cloudinary.

## 3. Arquitectura

1. La API valida tamaño, cantidad, firma JPEG/PNG, hash y ownership.
2. `CloudinaryUnitPhotoStorage` sube el archivo con `type=authenticated` y
   `public_id` opaco bajo `unihome/unidades/{guid}`.
3. La transformación **incoming** `fl_strip_profile` elimina EXIF, incluida la
   ubicación GPS, antes de guardar. No se solicitan metadatos EXIF.
4. MongoDB guarda en `unidades.fotos[]` el `storageKey` (public_id de
   Cloudinary), MIME, tamaño, SHA-256, orden, principal y fecha. No se guarda el
   binario.
5. `GetReadUrl` devuelve una URL del **proxy de la API**:
   `GET /api/v1/fotos/{photoRef}`; no devuelve la URL firmada de Cloudinary.
6. El proxy decodifica la referencia opaca, localiza la unidad por
   `FindByPhotoStorageKeyAsync`, comprueba visibilidad y ownership, y recién
   entonces descarga el binario con una URL firmada generada en servidor.
7. La URL firmada y las credenciales nunca salen del backend. El proxy responde
   con `Cache-Control: private, no-store` para borradores y
   `public, max-age=300` para unidades aprobadas.

## 4. Privacidad

- Cloudinary **authenticated** protege original y derivados; una URL sin firma
  no entrega el archivo.
- No se usa `access_type=token` porque es una función de plan Advanced y no está
  disponible en Free.
- La autorización temporal se implementa mediante proxy de backend y control de
  visibilidad en cada request; no se depende de que una URL firmada expire.
- Borradores: solo el propietario autenticado. Sin sesión: `401`; otro usuario:
  `404`.
- Unidades aprobadas: entrega pública; si dejan de ser públicas, la URL antigua
  deja de funcionar en el siguiente request.
- No se exponen correo, teléfono ni datos privados en la respuesta de imagen.
- EXIF/GPS se elimina con `fl_strip_profile`.

## 5. Configuración segura

Variables de entorno o User Secrets; nunca en `appsettings.json`, Git, logs o
GitHub Actions sin autorización:

```text
Cloudinary__CloudName
Cloudinary__ApiKey
Cloudinary__ApiSecret
```

Opcionales:

```text
Cloudinary__Folder=unihome/unidades
Cloudinary__PublicBaseUrl=https://<host-api>
Cloudinary__TimeoutSeconds=30
```

`PublicBaseUrl` permite construir URLs absolutas del proxy. Si no se configura,
la API devuelve una ruta relativa y antepone el host de la request cuando hay
`HttpContext`.

Procedimiento local recomendado:

```powershell
dotnet user-secrets set "Cloudinary:CloudName" "<cloud>" --project src/UniHome.Api
dotnet user-secrets set "Cloudinary:ApiKey" "<key>" --project src/UniHome.Api
dotnet user-secrets set "Cloudinary:ApiSecret" "<secret>" --project src/UniHome.Api
```

No se solicitan ni se imprimen los valores. `appsettings.json` solo contiene la
sección con cadenas vacías y parámetros no sensibles.

## 6. Comportamiento cuando falta configuración

Si faltan `CloudName`, `ApiKey` o `ApiSecret`, se registra
`UnconfiguredUnitPhotoStorage`:

- La API inicia normalmente.
- `POST /unidades/{id}/fotos` responde `503` controlado.
- La lectura de fotos responde `503`/`404` según el recurso.
- No se simula un proveedor ni se escribe en disco/MongoDB.

## 7. Compatibilidad M05/M04

- El contrato de EP-M05-03 no cambia: multipart `Fotos`, máximo 10, 5 MB,
  JPG/PNG, ownership, hash y primera foto principal.
- `Unit.fotos[].storageKey` conserva el public_id de Cloudinary.
- M05 preview usa `GetReadUrl`, por lo que recibe la URL del proxy.
- M04/M07 (PR #35) usan el mismo puerto y `UnitPhotoUrlResolver`; al integrarse
  recibirán la misma URL de proxy sin cambio de contrato. No se modifica el PR
  #35 en este trabajo.
- No se requiere migración MongoDB: el campo `storageKey` ya existe en M05-001.
  No se altera el checksum de M05-001.

## 8. Pruebas

- Unitarias: subida válida, límite 5 MB, error de proveedor, cancelación,
  lectura, 404, borrado y URL de proxy.
- Integración: entrega pública, borrador solo propietario, 401/404, headers de
  caché y ausencia de URL Cloudinary.
- Prueba real opt-in: `CloudinaryPersistenceTests`.
  - Requiere `UNIHOME_RUN_CLOUDINARY_INTEGRATION_TESTS=1` y las tres variables.
  - Sube un PNG sintético, comprueba lectura, elimina y verifica 404.
  - **Estado actual: PENDIENTE**. No hay credenciales Cloudinary en el entorno
    local ni en User Secrets; no se declara la integración real como aprobada.
  - No se añadieron secretos a GitHub Actions.

## 9. Riesgos y límites

- Cuota Free de 25 créditos; monitorear almacenamiento, bandwidth y
  transformaciones.
- El proxy consume bandwidth del API y de Cloudinary; se prefiere no transformar
  en entrega y cachear solo unidades públicas.
- `fl_strip_profile` es una transformación de subida; verificar en la prueba real
  que el plan/entorno la acepta y que no genera derivados públicos.
- Cloudinary es un proveedor externo; la caída o cuota agotada produce `503`, no
  datos simulados.
- La URL firmada es permanente pero solo se usa servidor a servidor. No se
  expone; el control de acceso efectivo es el proxy y la visibilidad actual.
- No se configuró `PublicBaseUrl` en Azure; si se omite, el cliente debe resolver
  la ruta relativa o se debe configurar antes de desplegar.

## 10. Acciones pendientes / no ejecutadas

- Configurar las tres credenciales en el entorno seguro de desarrollo.
- Ejecutar la prueba real opt-in y documentar PASS real.
- Configurar `Cloudinary__PublicBaseUrl` en Container Apps cuando se autorice el
  despliegue; no se modificó Azure.
- Reconciliar la evaluación Azure de PR #35 y actualizar ese PR en un cambio
  posterior autorizado; no se tocó PR #35.
