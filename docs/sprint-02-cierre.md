# Sprint 2 — Cierre backend M05

> **Actualizaci?n posterior:** DEC-04 se resolvi? en c?digo con Cloudinary Free en
> `feature/dec-04-cloudinary-storage`; ver [`dec-04-cloudinary.md`](dec-04-cloudinary.md).
> Este documento conserva el estado hist?rico del Sprint 2.

**Rama:** `feature/sprint-02-backend-integral`  
**Base:** `main` posterior al merge del Sprint 1 (`baba9c22afec47e42809296a93c2385a91ecd278`)  
**Alcance:** propiedades, unidades, fotografías, servicios, zona/reglas, vista previa y términos. No incluye HU-013/HU-027 de Sprint 4.

## Matriz de las 13 HU

| HU | RF | Clasificación | Endpoint / soporte |
|---|---|---|---|
| HU-010 | RF-48 | Backend implementado | `puedePublicar` en `GET /api/v1/perfil/mi-perfil` |
| HU-014 | RF-47 | Backend implementado | Edad revalidada en propiedad, unidad y términos; `403` |
| HU-029 | RF-14 | Backend implementado | `POST /api/v1/propiedades` |
| HU-030 | RF-49 | Backend implementado | `piso`/`numeroUnidad` obligatorios en Edificio; ignorados en otros tipos |
| HU-031 | RF-15 | Backend implementado | `POST /api/v1/propiedades/{id}/unidades`; Individual fuerza 1 lugar |
| HU-032 | RF-15 | Backend implementado con bloqueo externo | `POST /api/v1/unidades/{id}/fotos`; DEC-04 pendiente |
| HU-078 | RF-15 | Backend implementado | Ocupación, baño, mobiliario y catálogo de muebles |
| HU-079 | RF-15 | Backend implementado | `plantillaBase` rellena campos omitidos |
| HU-080 | RF-17 | Backend implementado | `GET /api/v1/unidades/{id}/vista-previa` sobre datos persistidos |
| HU-081 | RF-42 | Backend implementado | `PUT /api/v1/unidades/{id}/servicios`; Aparte exige monto |
| HU-082 | RF-43 | Backend implementado | `PUT /api/v1/unidades/{id}/zona-reglas` |
| HU-083 | RF-43 | Backend implementado | `PUT /api/v1/unidades/{id}/zona-reglas` |
| HU-091 | RF-14 | Backend implementado | `POST /api/v1/publicaciones/primera/terminos` |

## Endpoints y campos

### EP-M05-01 — `POST /api/v1/propiedades`

Request: `nombre`, `direccion`, `colonia`, `tipoPropiedad`, `universidadId`,
`latitud`, `longitud`, `piso`, `numeroUnidad`.

Respuesta `201`: `id`, `nombre`, `direccion`, `colonia`, `tipoPropiedad`,
`universidadId`, `latitud`, `longitud`, `piso`, `numeroUnidad`,
`estadoPublicacion`, `fechaAlta`, `ubicacionSugerida`.

Validaciones: titular autenticado y adulto; universidad activa; coordenadas
completas o ubicación sugerida del catálogo; Edificio exige piso y número;
duplicado de propietario/dirección/colonia/nombre responde `409`.

### EP-M05-02 — `POST /api/v1/propiedades/{id}/unidades`

Request: `tipoUnidad`, `ocupacion`, `lugares`, `precioMensualPorLugarMxn`,
`tamanoM2`, `paraQuien`, `bano`, `mobiliario`, `muebles`, `plantillaBase`.

Respuesta `201`: identidad, propiedad, universidad, campos de la unidad,
servicios, total de fotos, estado `Borrador` y fecha.

Validaciones: propiedad del titular; adulto; precio/tamaño positivos;
Individual fuerza 1 lugar; Compartida exige 2–50; muebles solo del catálogo y
vacíos si `SinMuebles`; plantilla solo rellena campos omitidos.

### EP-M05-03 — `POST /api/v1/unidades/{id}/fotos`

Multipart `Fotos[]`. Máximo 10 por unidad, 5 MB por archivo, JPG/PNG por firma
real, SHA-256 y metadatos en MongoDB. La primera foto es principal. El binario
se delega a `IUnitPhotoStorage`; sin proveedor DEC-04 responde `503` y no simula
almacenamiento.

### EP-M05-04 — `PUT /api/v1/unidades/{id}/servicios`

`servicios[]` con `nombre` del catálogo cerrado, `modalidad` Incluido/Aparte y
`montoMensualMxn` obligatorio solo para Aparte. No se permiten duplicados. El
monto Aparte no se suma a la renta base.

### EP-M05-05 — `PUT /api/v1/unidades/{id}/zona-reglas`

`informacionZona` y `reglasConvivencia` opcionales con límites de longitud. Solo
el propietario puede modificarlas.

### EP-M05-06 — `GET /api/v1/unidades/{id}/vista-previa`

Construye la tarjeta desde MongoDB, sin datos privados. Devuelve
`estadoPublicacion=Borrador`; `distanciaKm` solo si hay coordenadas reales;
`tiempoTraslado` es `null` porque no existe motor de rutas aprobado;
`arrendadorVerificado=false` y `esFavorito=null` porque M06/M07 no existen.

### EP-M05-07 — `POST /api/v1/publicaciones/primera/terminos`

`versionTerminos` obligatoria. Registra cuenta, versión y fecha en
`aceptaciones_terminos`; repetir la misma versión responde `422`. No envía a
revisión ni crea expedientes de M06.

## Seguridad

- Sujeto desde el claim `sub`; nunca desde body/query.
- Ownership en propiedad y unidad para todas las escrituras y la vista previa.
- Edad recalculada desde la fecha persistida; no se acepta del cliente.
- IDOR: propietario ajeno recibe `403`; recurso inexistente `404`.
- Límites de cuerpo, máximo de fotos, firma real de archivo y hash.
- Logs sin fotos, tokens, correos ni contenido privado.
- ProblemDetails homogéneo.

## MongoDB y migración

`M05-001` crea `propiedades`, `unidades` y `aceptaciones_terminos` con sus
índices. No se modifican M01-001 ni M03-001. Detalle en
[`mongodb-m05.md`](mongodb-m05.md).

## Pruebas locales

- Build Release: 0 advertencias, 0 errores.
- Unitarias: 515/515.
- Integración regular: 174/174; 5 MongoDB opt-in omitidas en el job normal.
- OpenAPI: 11/11.
- Python CI: 133/133.
- MongoDB real, SonarCloud, CodeQL, Semgrep, Gitleaks y gates: evidencia en el PR.

## Bloqueo operativo

DEC-04 sigue pendiente: no hay proveedor de blobs autorizado. El endpoint de
fotos está implementado y probado con doble, pero en producción falla cerrado
con `503`. No se declara funcionalidad de fotos lista para producción ni se
crean recursos cloud.
