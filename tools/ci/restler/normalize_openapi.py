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


def operation_keys(document: dict[str, Any]) -> set[tuple[str, str]]:
    operations: set[tuple[str, str]] = set()
    for path, item in (document.get("paths") or {}).items():
        if not isinstance(item, dict):
            continue
        for method in ("get", "post", "put", "patch", "delete", "head", "options"):
            if isinstance(item.get(method), dict):
                operations.add((method.upper(), path))
    return operations


def _convert_type_union(schema: dict[str, Any], location: str) -> dict[str, Any]:
    raw = schema.get("type")
    if not isinstance(raw, list):
        return schema

    values = [value for value in raw if isinstance(value, str)]
    non_null = [value for value in values if value != "null"]
    schema = dict(schema)
    schema.pop("type", None)
    if non_null:
        if len(non_null) == 1:
            schema["type"] = non_null[0]
        else:
            schema["oneOf"] = [{"type": value} for value in non_null]
        if "null" in values:
            schema["nullable"] = True
    else:
        # No existe un caso así en el contrato actual; se deja explícito para
        # que una futura aparición sea visible en el reporte en vez de silenciosa.
        schema["type"] = "string"
        schema["nullable"] = True
    return schema


def convert(document: dict[str, Any]) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    """Convierte tipos union 3.1 a nullable 3.0 y devuelve los cambios."""
    original_operations = operation_keys(document)
    converted = copy.deepcopy(document)
    changes: list[dict[str, Any]] = []

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
