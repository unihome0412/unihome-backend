#!/usr/bin/env python3
"""Prepara RESTler y los datos sintéticos sobre la API efímera de UniHome.

El script usa únicamente la API real y el SMTP de captura local. Los valores
sensibles (contraseñas, tokens y códigos) quedan en `.restler-state`, fuera de
los artefactos; el reporte solo contiene metadatos no sensibles.
"""
from __future__ import annotations

import json
import os
import sys
import uuid
from pathlib import Path
from typing import Any

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from restler import normalize_openapi
from zap_dast import (
    HttpClient,
    _generate_password,
    _json_body,
    _register_payload,
    wait_for_verification_code,
)

ROOT_DIR = Path(__file__).resolve().parents[3]
TARGET = "http://127.0.0.1:5088"
MAILBOX_DIR = ROOT_DIR / ".dast-mail"
ARTIFACTS_DIR = ROOT_DIR / "artifacts/restler"
WORK_DIR = ROOT_DIR / ".restler-work"
STATE_DIR = ROOT_DIR / ".restler-state"
TOKEN_PROVIDER = Path(__file__).with_name("token_provider.py").resolve()


def _write_json(path: Path, value: Any, private: bool = False) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")
    if private:
        try:
            os.chmod(path, 0o600)
        except OSError:
            pass


def _json_object(payload: bytes) -> dict[str, Any]:
    return _json_body(payload)


def _register(http: HttpClient, email: str, password: str) -> str:
    _, payload, _ = http.request(
        "POST", "/api/v1/auth/registro",
        body=_register_payload(email, password),
        expected={201}, note="restler registro")
    response = _json_object(payload)
    if response.get("correoConfirmacionEnviado") is not True:
        raise RuntimeError("El SMTP local no confirmó el envío de registro.")
    return wait_for_verification_code(MAILBOX_DIR, email)


def _confirm(http: HttpClient, email: str, code: str) -> None:
    http.request(
        "POST", "/api/v1/auth/confirmacion-correo",
        body={"correo": email, "codigo": code},
        expected={200}, note="restler confirmación")


def _login(http: HttpClient, email: str, password: str) -> dict[str, Any]:
    _, payload, _ = http.request(
        "POST", "/api/v1/auth/login",
        body={"correo": email, "contrasena": password},
        expected={200}, note="restler login")
    tokens = _json_object(payload)
    if not tokens.get("accessToken") or not tokens.get("refreshToken"):
        raise RuntimeError("El login sintético no devolvió tokens completos.")
    return tokens


def _custom_dictionary(values: dict[str, str]) -> dict[str, Any]:
    return {"restler_custom_payload": {key: [value] for key, value in values.items()}}


def build_private_dictionaries(
    *,
    register_email: str,
    register_password: str,
    confirm_email: str,
    confirmation_code: str,
    login_email: str,
    login_password: str,
    refresh_token: str,
    token_password: str,
) -> dict[str, dict[str, Any]]:
    common_registration = {
        "nombreCompleto": "Cuenta RESTler",
        "correo": register_email,
        "fechaNacimiento": "2004-05-10",
        # El OpenAPI actual declara Gender como integer, pero la API serializa
        # el enum como string. Se envía el JSON string literal sin ocultar la
        # discrepancia de contrato; queda registrada en el reporte.
        "genero": '"Mujer"',
        "contrasena": register_password,
        "confirmarContrasena": register_password,
        "versionAvisoPrivacidad": "2026-01",
    }
    return {
        "registro": _custom_dictionary(common_registration),
        "confirmacion": _custom_dictionary({
            "correo": confirm_email,
            "codigo": confirmation_code,
        }),
        "reenvio": _custom_dictionary({"correo": confirm_email}),
        "login": _custom_dictionary({
            "correo": login_email,
            "contrasena": login_password,
        }),
        "refresh": _custom_dictionary({"refreshToken": refresh_token}),
        "delete": _custom_dictionary({"contrasena": token_password}),
    }


def build_compile_dictionary() -> dict[str, Any]:
    """Fuerza custom payloads en la gramática; los valores reales van por endpoint."""
    return _custom_dictionary({
        "nombreCompleto": "placeholder",
        "correo": "placeholder@uni.edu",
        "fechaNacimiento": "2004-05-10",
        "genero": '"Mujer"',
        "contrasena": "placeholder",
        "confirmarContrasena": "placeholder",
        "versionAvisoPrivacidad": "2026-01",
        "codigo": "000000",
        "refreshToken": "placeholder",
    })


def build_engine_settings(
    *,
    token_password: str,
    per_resource: dict[str, Path],
) -> dict[str, Any]:
    return {
        "max_combinations": 4,
        "max_sequence_length": 3,
        "max_request_execution_time": 20,
        "max_async_resource_creation_time": 10,
        "garbage_collection_interval": 30,
        "garbage_collector_cleanup_time": 30,
        "dyn_objects_cache_size": 5,
        "request_throttle_ms": 50,
        "save_results_in_fixed_dirname": True,
        "authentication": {
            "token": {
                "module": {
                    "file": str(TOKEN_PROVIDER),
                    "function": "acquire_token",
                    "data": {"password": token_password},
                },
                "token_refresh_interval": 120,
            }
        },
        "per_resource_settings": {
            endpoint: {"custom_dictionary": str(path.resolve())}
            for endpoint, path in per_resource.items()
        },
    }


def build_compile_config(
    *,
    normalized_spec: Path,
    compile_dictionary: Path,
    engine_settings: Path,
) -> dict[str, Any]:
    return {
        "SwaggerSpecFilePath": [str(normalized_spec.resolve())],
        "GrammarOutputDirectoryPath": str((WORK_DIR / "Compile").resolve()),
        "CustomDictionaryFilePath": str(compile_dictionary.resolve()),
        "EngineSettingsFilePath": str(engine_settings.resolve()),
        "IncludeOptionalParameters": True,
        "UseHeaderExamples": True,
        "UsePathExamples": False,
        "UseQueryExamples": True,
        "UseBodyExamples": True,
        "UseAllExamplePayloads": False,
        "DiscoverExamples": False,
        "DataFuzzing": True,
        "ReadOnlyFuzz": False,
        "ResolveQueryDependencies": True,
        "ResolveBodyDependencies": True,
        "ResolveHeaderDependencies": False,
        "UseRefreshableToken": True,
        "AllowGetProducers": False,
        "TrackFuzzedParameterNames": True,
    }


def prepare() -> dict[str, Any]:
    for directory in (ARTIFACTS_DIR, WORK_DIR, STATE_DIR, MAILBOX_DIR):
        directory.mkdir(parents=True, exist_ok=True)

    http = HttpClient(TARGET, None)
    _, spec_payload, _ = http.request(
        "GET", "/openapi/v1.json", expected={200}, openapi=False,
        note="OpenAPI RESTler")
    source_spec = _json_object(spec_payload)
    _write_json(ARTIFACTS_DIR / "openapi-original.json", source_spec)

    normalized_spec, changes = normalize_openapi.convert(source_spec)
    _write_json(WORK_DIR / "openapi-restler-30.json", normalized_spec)
    _write_json(ARTIFACTS_DIR / "openapi-restler-30.json", normalized_spec)
    transformation = normalize_openapi.report(source_spec, normalized_spec, changes)
    _write_json(ARTIFACTS_DIR / "openapi-transformation.json", transformation)

    register_email = f"restler.register.{uuid.uuid4().hex}@uni.edu"
    register_password = _generate_password()

    confirm_email = f"restler.confirm.{uuid.uuid4().hex}@uni.edu"
    confirm_password = _generate_password()
    confirmation_code = _register(http, confirm_email, confirm_password)

    login_email = f"restler.login.{uuid.uuid4().hex}@uni.edu"
    login_password = _generate_password()
    login_code = _register(http, login_email, login_password)
    _confirm(http, login_email, login_code)
    tokens = _login(http, login_email, login_password)

    token_password = _generate_password()
    dictionaries = build_private_dictionaries(
        register_email=register_email,
        register_password=register_password,
        confirm_email=confirm_email,
        confirmation_code=confirmation_code,
        login_email=login_email,
        login_password=login_password,
        refresh_token=str(tokens["refreshToken"]),
        token_password=token_password,
    )
    dictionary_paths: dict[str, Path] = {}
    for name, value in dictionaries.items():
        path = STATE_DIR / f"dict-{name}.json"
        _write_json(path, value, private=True)
        dictionary_paths[name] = path

    compile_dictionary = STATE_DIR / "compile-dict.json"
    _write_json(compile_dictionary, build_compile_dictionary(), private=True)

    per_resource = {
        "/api/v1/auth/registro": dictionary_paths["registro"],
        "/api/v1/auth/confirmacion-correo": dictionary_paths["confirmacion"],
        "/api/v1/auth/confirmacion-correo/reenvio": dictionary_paths["reenvio"],
        "/api/v1/auth/login": dictionary_paths["login"],
        "/api/v1/auth/refresh": dictionary_paths["refresh"],
        "/api/v1/cuentas/mi-cuenta": dictionary_paths["delete"],
    }
    settings = build_engine_settings(
        token_password=token_password, per_resource=per_resource)
    settings_path = STATE_DIR / "engine-settings.json"
    _write_json(settings_path, settings, private=True)

    compile_config = build_compile_config(
        normalized_spec=WORK_DIR / "openapi-restler-30.json",
        compile_dictionary=compile_dictionary,
        engine_settings=settings_path,
    )
    _write_json(STATE_DIR / "compile-config.json", compile_config, private=True)

    summary = {
        "target": TARGET,
        "source_openapi": source_spec.get("openapi"),
        "target_openapi": normalized_spec.get("openapi"),
        "operations": len(normalize_openapi.operation_keys(source_spec)),
        "transformation_changes": len(changes),
        "confirmation_account_prepared": True,
        "login_account_prepared": True,
        "registration_dictionary_prepared": True,
        "token_provider_configured": True,
    }
    _write_json(ARTIFACTS_DIR / "prepare-summary.json", summary)
    print(
        "RESTler preparado: "
        f"{summary['operations']} operaciones, "
        f"{summary['transformation_changes']} conversiones OpenAPI 3.1->3.0.")
    return summary


if __name__ == "__main__":  # pragma: no cover - entrada de infraestructura
    prepare()
