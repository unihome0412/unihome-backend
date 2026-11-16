#!/usr/bin/env python3
"""Deriva una copia OpenAPI 3.0 compatible con RESTler.

La API de UniHome publica OpenAPI 3.1 con tipos union que RESTler 9.3.1 no
interpreta. Esta transformación es mecánica, se registra en un reporte y no
elimina ni renombra rutas. El contrato original se conserva como evidencia.
"""
from __future__ import annotations

import copy
import json
from typing import Any


class NormalizationError(RuntimeError):
    """La transformación cambiaría la superficie observable del contrato."""


# El generador nativo de ASP.NET describe estos enums como integer, pero la API
# usa JsonStringEnumConverter. RESTler necesita el tipo real para no enviar
# enteros que el contrato rechaza. Solo se corrige la copia derivada y el
# cambio queda registrado en openapi-transformation.json.
STRING_ENUMS = {
    "Gender": ["Mujer", "Hombre", "PrefiereNoDecir"],
    "AccountLabel": [
        "UsuarioUniHome",
        "PublicacionEnRevision",
        "ConEspaciosPublicados",
    ],
    "PropertyType": ["Casa", "Departamento", "Edificio"],
    "UnitType": ["Habitacion", "Departamento", "Estudio"],
    "Occupancy": ["Individual", "Compartida"],
    "ForWhom": ["SoloMujeres", "SoloHombres", "Mixto"],
    "BathroomType": ["Privado", "Compartido"],
    # SinMuebles primero: el valor por defecto que elige RESTler evita que la
    # lista de muebles (texto libre en el contrato generado) invalide la unidad.
    "FurnitureType": ["SinMuebles", "Amueblado", "Parcial"],
    "UnitTemplate": [
        "HabitacionIndividual",
        "HabitacionCompartida",
        "DepartamentoCompleto",
        "Estudio",
    ],
    "ServiceName": [
        "Agua",
        "Luz",
        "Gas",
        "Internet",
        "Lavadora",
        "CocinaEquipada",
        "Estacionamiento",
        "LimpiezaAreasComunes",
    ],
    "ServiceMode": ["Incluido", "Aparte"],
    "PublicationState": ["Borrador", "EnRevision", "Aprobada", "Rechazada"],
}
TYPE_PREFERENCE = ("integer", "number", "boolean", "string", "array", "object")
PHOTOS_PATH = "/api/v1/unidades/{unitId}/fotos"
PHOTOS_FIELD = "Fotos"


def operation_keys(document: dict[str, Any]) -> set[tuple[str, str]]:
    operations: set[tuple[str, str]] = set()
    for path, item in (document.get("paths") or {}).items():
        if not isinstance(item, dict):
            continue
        for method in ("get", "post", "put", "patch", "delete", "head", "options"):
            if isinstance(item.get(method), dict):
                operations.add((method.upper(), path))
    return operations


def _preferred_type(values: list[str]) -> str:
    for candidate in TYPE_PREFERENCE:
        if candidate in values:
            return candidate
    return values[0]


def _convert_type_union(schema: dict[str, Any], location: str) -> dict[str, Any]:
    raw = schema.get("type")
    if not isinstance(raw, list):
        return schema

    values = [value for value in raw if isinstance(value, str)]
    non_null = [value for value in values if value != "null"]
    schema = dict(schema)
    schema.pop("type", None)
    if non_null:
        # RESTler no interpreta uniones escalares; se elige el tipo más
        # específico (p. ej. number sobre string para latitud/longitud).
        schema["type"] = _preferred_type(non_null)
        if "null" in values:
            schema["nullable"] = True
    else:
        # No existe un caso así en el contrato actual; se deja explícito para
        # que una futura aparición sea visible en el reporte en vez de silenciosa.
        schema["type"] = "string"
        schema["nullable"] = True
    return schema


def _convert_one_of(
    schema: dict[str, Any],
    string_enums: dict[str, list[Any]],
) -> tuple[dict[str, Any], bool]:
    """Resuelve oneOf [null, $ref] a un tipo nullable que RESTler compila."""
    values = schema.get("oneOf")
    if not isinstance(values, list):
        return schema, False
    non_null = [
        value for value in values
        if isinstance(value, dict) and value.get("type") != "null"
    ]
    if len(non_null) != 1:
        return schema, False

    candidate = non_null[0]
    reference = candidate.get("$ref")
    if isinstance(reference, str) and reference in string_enums:
        # Se inlinea el enum para que NJsonSchema lo reconozca sin depender
        # de la resolución de $ref + nullable.
        replacement: dict[str, Any] = {
            "type": "string",
            "enum": list(string_enums[reference]),
        }
    else:
        replacement = dict(candidate)
    replacement.update({
        key: value
        for key, value in schema.items()
        if key not in {"oneOf", "nullable"}
    })
    replacement["nullable"] = any(
        isinstance(value, dict) and value.get("type") == "null"
        for value in values
    )
    return replacement, True


def _relax_request_uuid_formats(document: dict[str, Any]) -> list[dict[str, Any]]:
    """Quita format=uuid de parámetros de entrada para permitir IDs sintéticos.

    RESTler trata uuid4 como generador dinámico y no acepta un valor del
    diccionario por endpoint. En la copia derivada los IDs de entrada se
    declaran string para poder fijarlos a recursos sintéticos reales. Los
    esquemas de respuesta conservan su formato.
    """
    changes: list[dict[str, Any]] = []

    for path, item in (document.get("paths") or {}).items():
        if not isinstance(item, dict):
            continue
        for method in ("get", "post", "put", "patch", "delete", "head", "options"):
            operation = item.get(method)
            if not isinstance(operation, dict):
                continue
            for parameter in operation.get("parameters") or []:
                if not isinstance(parameter, dict):
                    continue
                schema = parameter.get("schema")
                if (
                    isinstance(schema, dict)
                    and schema.get("type") == "string"
                    and schema.get("format") == "uuid"
                ):
                    schema.pop("format", None)
                    changes.append({
                        "location": (
                            f"#/paths/{path}/{method}/parameters/"
                            f"{parameter.get('name')}"),
                        "before_format": "uuid",
                        "after_type": "string",
                    })

    schemas = (document.get("components") or {}).get("schemas")
    if isinstance(schemas, dict):
        request = schemas.get("CrearPropiedadRequest")
        properties = request.get("properties") if isinstance(request, dict) else None
        university = properties.get("universidadId") if isinstance(properties, dict) else None
        if isinstance(university, dict) and university.get("format") == "uuid":
            university.pop("format", None)
            changes.append({
                "location": "#/components/schemas/CrearPropiedadRequest/properties/universidadId",
                "before_format": "uuid",
                "after_type": "string",
            })

    return changes


def _fix_string_enums(document: dict[str, Any]) -> list[dict[str, Any]]:
    changes: list[dict[str, Any]] = []
    schemas = (document.get("components") or {}).get("schemas")
    if not isinstance(schemas, dict):
        return changes

    for name, values in STRING_ENUMS.items():
        schema = schemas.get(name)
        if not isinstance(schema, dict):
            continue
        if schema.get("type") == "string" and schema.get("enum") == values:
            continue
        before = {
            "type": schema.get("type"),
            "enum": schema.get("enum"),
        }
        schema["type"] = "string"
        schema["enum"] = list(values)
        schema.pop("format", None)
        changes.append({
            "location": f"#/components/schemas/{name}",
            "before": before,
            "after": {"type": "string", "enum": list(values)},
        })
    return changes


def _validate_multipart_photos(document: dict[str, Any]) -> None:
    """Evita fuzzear fotos con un contrato distinto al runtime.

    El endpoint real exige multipart/form-data con Fotos como array de binarios.
    Si el documento fuente declara urlencoded o un tipo distinto, se aborta la
    preparación en vez de inventar tráfico o perder cobertura silenciosamente.
    """
    item = (document.get("paths") or {}).get(PHOTOS_PATH)
    operation = item.get("post") if isinstance(item, dict) else None
    if not isinstance(operation, dict):
        return

    request_body = operation.get("requestBody")
    content = request_body.get("content") if isinstance(request_body, dict) else None
    if not isinstance(content, dict):
        raise NormalizationError(
            "POST de fotos no declara requestBody; se esperaba multipart/form-data.")

    if "application/x-www-form-urlencoded" in content:
        raise NormalizationError(
            "POST de fotos declara application/x-www-form-urlencoded; "
            "el runtime exige multipart/form-data.")

    media = content.get("multipart/form-data")
    if not isinstance(media, dict):
        raise NormalizationError(
            "POST de fotos no declara multipart/form-data.")

    schema = media.get("schema")
    properties = schema.get("properties") if isinstance(schema, dict) else None
    photos = properties.get(PHOTOS_FIELD) if isinstance(properties, dict) else None
    if not isinstance(photos, dict) or photos.get("type") != "array":
        raise NormalizationError(
            f"POST de fotos no declara {PHOTOS_FIELD} como array.")

    required = schema.get("required") if isinstance(schema, dict) else None
    if not isinstance(required, list) or PHOTOS_FIELD not in required:
        raise NormalizationError(
            f"POST de fotos no declara {PHOTOS_FIELD} como obligatorio.")

    photos_item = photos.get("items")
    if (
        not isinstance(photos_item, dict)
        or photos_item.get("type") != "string"
        or photos_item.get("format") != "binary"
    ):
        raise NormalizationError(
            f"POST de fotos no declara {PHOTOS_FIELD} como binario.")

    encoding = media.get("encoding")
    if not isinstance(encoding, dict) or PHOTOS_FIELD not in encoding:
        raise NormalizationError(
            f"POST de fotos no declara la codificación repetible de {PHOTOS_FIELD}.")


def convert(document: dict[str, Any]) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    """Convierte tipos union 3.1 a nullable 3.0 y devuelve los cambios."""
    original_operations = operation_keys(document)
    converted = copy.deepcopy(document)
    _validate_multipart_photos(converted)
    changes: list[dict[str, Any]] = _fix_string_enums(converted)
    changes += _relax_request_uuid_formats(converted)
    schemas = (converted.get("components") or {}).get("schemas") or {}
    string_enums = {
        f"#/components/schemas/{name}": schema["enum"]
        for name, schema in schemas.items()
        if isinstance(schema, dict)
        and schema.get("type") == "string"
        and isinstance(schema.get("enum"), list)
    }

    def walk(value: Any, location: str) -> Any:
        if isinstance(value, dict):
            if isinstance(value.get("type"), list):
                before = list(value["type"])
                replacement = _convert_type_union(value, location)
                changes.append({
                    "location": location,
                    "before_type": before,
                    "after_type": replacement.get("type"),
                    "after_one_of": replacement.get("oneOf"),
                    "nullable": replacement.get("nullable", False),
                })
                value = replacement
            if "oneOf" in value:
                before = list(value["oneOf"])
                replacement, changed = _convert_one_of(value, string_enums)
                if changed:
                    changes.append({
                        "location": location,
                        "before_one_of": before,
                        "after_ref": replacement.get("$ref"),
                        "after_type": replacement.get("type"),
                        "nullable": replacement.get("nullable", False),
                    })
                    value = replacement
            for key, child in list(value.items()):
                value[key] = walk(child, f"{location}.{key}")
            return value
        if isinstance(value, list):
            return [walk(child, f"{location}[{index}]") for index, child in enumerate(value)]
        return value

    converted = walk(converted, "$")
    converted["openapi"] = "3.0.3"
    converted_operations = operation_keys(converted)
    if original_operations != converted_operations:
        raise NormalizationError(
            "La normalización cambió las operaciones OpenAPI; se aborta para "
            "no ocultar rutas.")
    return converted, changes


def report(
    source: dict[str, Any],
    converted: dict[str, Any],
    changes: list[dict[str, Any]],
) -> dict[str, Any]:
    return {
        "source_openapi": source.get("openapi"),
        "target_openapi": converted.get("openapi"),
        "source_operations": len(operation_keys(source)),
        "target_operations": len(operation_keys(converted)),
        "changes": changes,
        "change_count": len(changes),
        "operations_preserved": operation_keys(source) == operation_keys(converted),
    }


def load(path) -> dict[str, Any]:
    with open(path, encoding="utf-8") as file:
        value = json.load(file)
    if not isinstance(value, dict):
        raise NormalizationError("El documento OpenAPI no es un objeto JSON.")
    return value
