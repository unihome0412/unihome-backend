# Sprint 2 — Inventario previsto (no implementado)

**Fuente:** Sprint Planning vigente y SDD M05.  
**Estado:** implementado en la rama `feature/sprint-02-backend-integral`; la
evidencia y los bloqueos se registran en [`sprint-02-cierre.md`](sprint-02-cierre.md).
El único bloqueo operativo es DEC-04 para el proveedor de blobs de fotografías.

## Historias del Sprint 2

HU-010 (RF-48), HU-014 (RF-47), HU-029 (RF-14), HU-030 (RF-49),
HU-031 (RF-15), HU-032 (RF-15), HU-078 (RF-15), HU-079 (RF-15),
HU-080 (RF-17), HU-081 (RF-42), HU-082 (RF-43), HU-083 (RF-43),
HU-091 (RF-14).

## Endpoints previstos por M05

| ID | Método | Ruta | HU | Dependencias |
|---|---|---|---|---|
| EP-M05-01 | POST | `/api/v1/propiedades` | HU-029, HU-030, HU-014 | M01/M03; edad ≥18 |
| EP-M05-02 | POST | `/api/v1/propiedades/{id}/unidades` | HU-031, HU-078, HU-079 | Propiedad del titular |
| EP-M05-03 | POST | `/api/v1/unidades/{id}/fotos` | HU-032 | Almacenamiento de archivos (DEC-04) |
| EP-M05-04 | PUT | `/api/v1/unidades/{id}/servicios` | HU-081 | Catálogo de servicios |
| EP-M05-05 | PUT | `/api/v1/unidades/{id}/zona-reglas` | HU-082, HU-083 | Propiedad del titular |
| EP-M05-06 | GET | `/api/v1/unidades/{id}/vista-previa` | HU-080 | Contrato de tarjeta M04 |
| EP-M05-07 | POST | `/api/v1/publicaciones/primera/terminos` | HU-091 | Primera publicación |

HU-010 se apoya en el campo existente `puedePublicar` de
`GET /api/v1/perfil/mi-perfil`; no requiere un endpoint nuevo.
HU-013/HU-027 (envío a revisión) son Sprint 4 y quedan fuera de este inventario.
