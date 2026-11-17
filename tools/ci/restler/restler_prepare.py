#!/usr/bin/env python3
"""Prepara RESTler y los datos sintéticos sobre la API efímera de UniHome.

El script usa únicamente la API real y el SMTP de captura local. Los valores
sensibles (contraseñas, tokens y códigos) quedan en `.restler-state` y
`.ci-state`, fuera de los artefactos; el reporte solo contiene metadatos no
sensibles. La cuenta administradora la provisiona `provision_admin.py` antes de
reiniciar la API con `Admin__AccountIds`.
"""
from __future__ import annotations

import json
import os
import subprocess
import sys
import urllib.parse
import uuid
from dataclasses import dataclass
from pathlib import Path
from typing import Any

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from restler import normalize_openapi
from zap_dast import (
    DAST_PHOTO_PNG,
    HttpClient,
    _generate_password,
    _json_body,
    _register_payload,
    load_admin_state,
    wait_for_password_reset_token,
    wait_for_verification_code,
)

ROOT_DIR = Path(__file__).resolve().parents[3]
TARGET = "http://127.0.0.1:5088"
MAILBOX_DIR = ROOT_DIR / ".dast-mail"
ARTIFACTS_DIR = ROOT_DIR / "artifacts/restler"
WORK_DIR = ROOT_DIR / ".restler-work"
STATE_DIR = ROOT_DIR / ".restler-state"
TOKEN_PROVIDER = Path(__file__).with_name("token_provider.py").resolve()


@dataclass(frozen=True)
class SyntheticData:
    register_email: str
    register_password: str
    confirm_email: str
    confirmation_code: str
    login_email: str
    login_password: str
    refresh_token: str
    delete_password: str
    university_id: str
    property_id: str
    unit_id: str
    photo_ref: str
    recovery_email: str
    recovery_request_email: str
    reset_token: str
    reset_password: str


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


def _create_university(http: HttpClient, token: str) -> str:
    _, payload, _ = http.request(
        "POST", "/api/v1/admin/universidades",
        body={"nombreYCampus": f"RESTler Base {uuid.uuid4().hex}"},
        token=token, expected={201}, note="restler universidad base")
    value = _json_object(payload)
    identifier = str(value.get("id") or "")
    if not identifier:
        raise RuntimeError("El alta de universidad no devolvió id.")
    return identifier


def _create_property(
    http: HttpClient, token: str, university_id: str,
) -> str:
    _, payload, _ = http.request(
        "POST", "/api/v1/propiedades",
        body={
            "nombre": f"RESTler Propiedad Base {uuid.uuid4().hex}",
            "direccion": "Calle RESTler 123",
            "colonia": "Centro",
            "tipoPropiedad": "Casa",
            "universidadId": university_id,
            "latitud": 19.4326,
            "longitud": -99.1332,
        },
        token=token, expected={201}, note="restler propiedad base")
    value = _json_object(payload)
    identifier = str(value.get("id") or "")
    if not identifier:
        raise RuntimeError("El alta de propiedad no devolvió id.")
    return identifier


def _create_unit(http: HttpClient, token: str, property_id: str) -> str:
    _, payload, _ = http.request(
        "POST", f"/api/v1/propiedades/{property_id}/unidades",
        body={
            "tipoUnidad": "Habitacion",
            "ocupacion": "Individual",
            "lugares": 1,
            "precioMensualPorLugarMxn": 3500,
            "tamanoM2": 15,
            "paraQuien": "Mixto",
            "bano": "Privado",
            "mobiliario": "Amueblado",
            "muebles": ["Cama"],
            "plantillaBase": "HabitacionIndividual",
        },
        token=token, expected={201}, note="restler unidad base")
    value = _json_object(payload)
    identifier = str(value.get("id") or "")
    if not identifier:
        raise RuntimeError("El alta de unidad no devolvió id.")
    return identifier


def _publish_unit_for_exploration(unit_id: str) -> None:
    """Publica el fixture M04/M07 en la base efímera de RESTler.

    RESTler conserva este fixture para que las rutas públicas tengan datos; el
    job DAST usa la aprobación real de Sprint 5. La base es exclusiva y
    desechable. Si no se recibe el contenedor (por ejemplo, en pruebas locales)
    no se hace nada; en CI se exige el fixture para las rutas públicas.
    """
    container = os.environ.get("MONGO_CONTAINER_ID", "").strip()
    database = os.environ.get("MONGO_RESTLER_DATABASE", "").strip()

    if not container or not database:
        return

    try:
        normalized = uuid.UUID(unit_id).hex
    except ValueError as error:
        raise RuntimeError("El alta de unidad devolvió un id no válido.") from error

    script = (
        f'const database = db.getSiblingDB({json.dumps(database)});'
        f'const result = database.unidades.updateOne('
        f'{{_id: {json.dumps(normalized)}}},'
        f'{{$set: {{estadoPublicacion: "Aprobada", estadoOcupacion: "Disponible"}}}});'
        'if (result.matchedCount !== 1) {'
        '  throw new Error("fixture M04 no encontrado");'
        '}'
    )
    completed = subprocess.run(
        ["docker", "exec", container, "mongosh", "--quiet", "--eval", script],
        capture_output=True,
        text=True,
        check=False,
    )

    if completed.returncode != 0:
        raise RuntimeError("No se pudo publicar el fixture M04 en MongoDB efímero.")


def _upload_photo_and_read_ref(
    http: HttpClient,
    token: str,
    unit_id: str,
) -> str:
    http.request_multipart(
        "POST", f"/api/v1/unidades/{unit_id}/fotos",
        files=[("Fotos", "foto.png", "image/png", DAST_PHOTO_PNG)],
        token=token, expected={200}, note="restler foto base")
    _, payload, _ = http.request(
        "GET", f"/api/v1/unidades/{unit_id}/vista-previa",
        token=token, expected={200}, auth=True, note="restler vista previa base")
    value = _json_object(payload)
    url = str(value.get("fotoPrincipalUrl") or "")
    path = urllib.parse.urlsplit(url).path
    prefix = "/api/v1/fotos/"
    if not path.startswith(prefix) or len(path) <= len(prefix):
        raise RuntimeError("La vista previa no devolvió una referencia de foto.")
    return path[len(prefix):]


def _request_password_reset(http: HttpClient, email: str) -> str:
    http.request(
        "POST", "/api/v1/auth/recuperacion/solicitud",
        body={"correo": email}, expected={200}, note="restler recuperación")
    return wait_for_password_reset_token(MAILBOX_DIR, email)


def _custom_dictionary(values: dict[str, str]) -> dict[str, Any]:
    return {"restler_custom_payload": {key: [value] for key, value in values.items()}}


def build_private_dictionaries(data: SyntheticData) -> dict[str, dict[str, Any]]:
    """Valores para restler_custom_payload.

    RESTler ya entrecomilla los payloads de esquemas string. Por eso los
    strings se guardan sin comillas JSON; los IDs/path y números van crudos.
    Los enums y campos opcionales se dejan a los valores por defecto válidos
    del grammar para evitar conflictos de quoting entre endpoints.
    """
    return {
        "registro": _custom_dictionary({
            "nombreCompleto": "Cuenta RESTler",
            "correo": data.register_email,
            "fechaNacimiento": "2004-05-10",
            "genero": "Mujer",
            "contrasena": data.register_password,
            "confirmarContrasena": data.register_password,
            "aceptoAvisoPrivacidad": "true",
            "versionAvisoPrivacidad": "2026-01",
        }),
        "confirmacion": _custom_dictionary({
            "correo": data.confirm_email,
            "codigo": data.confirmation_code,
        }),
        # Un correo aún no registrado evita invalidar el código de confirmación.
        "reenvio": _custom_dictionary({"correo": data.register_email}),
        "login": _custom_dictionary({
            "correo": data.login_email,
            "contrasena": data.login_password,
        }),
        "refresh": _custom_dictionary({"refreshToken": data.refresh_token}),
        "delete": _custom_dictionary({
            "contrasena": data.delete_password,
            "confirmacionPerdidaInformacion": "true",
        }),
        "admin-update": _custom_dictionary({
            "id": data.university_id,
            "nombreYCampus": "RESTler Universidad Editada",
        }),
        "admin-status": _custom_dictionary({
            "id": data.university_id,
            "activa": "false",
        }),
        "property": _custom_dictionary({
            "universidadId": data.university_id,
            "latitud": "19.4326",
            "longitud": "-99.1332",
        }),
        "unit": _custom_dictionary({"propertyId": data.property_id}),
        "exploration-query": _custom_dictionary({
            "universidadId": data.university_id,
            "precioMaxMxn": "4500",
            "soloIndividual": "true",
            "paraQuien": "Mixto",
            "page": "1",
            "pageSize": "20",
        }),
        "services": _custom_dictionary({"unitId": data.unit_id}),
        "zone": _custom_dictionary({"unitId": data.unit_id}),
        "unit-path": _custom_dictionary({"unitId": data.unit_id}),
        "photo": _custom_dictionary({"photoRef": data.photo_ref}),
        "recovery-request": _custom_dictionary({
            "correo": data.recovery_request_email,
        }),
        "recovery-confirm": _custom_dictionary({
            "token": data.reset_token,
            "nuevaContrasena": data.reset_password,
        }),
    }


def build_compile_dictionary(data: SyntheticData) -> dict[str, Any]:
    """Defaults válidos; los valores se afinan por endpoint."""
    return _custom_dictionary({
        "nombreCompleto": "placeholder",
        "correo": "placeholder@uni.edu",
        "fechaNacimiento": "2004-05-10",
        "genero": "Mujer",
        "contrasena": "placeholder",
        "confirmarContrasena": "placeholder",
        "versionAvisoPrivacidad": "2026-01",
        "codigo": data.confirmation_code,
        "refreshToken": data.refresh_token,
        "token": data.reset_token,
        "nuevaContrasena": data.reset_password,
        "photoRef": data.photo_ref,
        "universidadId": data.university_id,
        "nombreYCampus": "RESTler Universidad",
        "propertyId": data.property_id,
        "unitId": data.unit_id,
        "id": data.university_id,
        "latitud": "19.4326",
        "longitud": "-99.1332",
    })


def build_engine_settings(
    *,
    token_data: dict[str, Any],
    per_resource: dict[str, Path],
    max_sequence_length: int,
    max_combinations: int,
    time_budget: float,
    exclude_requests: list[dict[str, Any]] | None = None,
    include_requests: list[dict[str, Any]] | None = None,
) -> dict[str, Any]:
    settings: dict[str, Any] = {
        "max_combinations": max_combinations,
        "max_schema_combinations": 1,
        "max_sequence_length": max_sequence_length,
        "max_request_execution_time": 20,
        "max_async_resource_creation_time": 10,
        "garbage_collection_interval": 30,
        "garbage_collector_cleanup_time": 30,
        "dyn_objects_cache_size": 5,
        "request_throttle_ms": 50,
        # GET /api/v1/fotos/{photoRef} devuelve image/png. RESTler 9.3.1
        # decodifica todo el socket como UTF-8 y aborta con UnicodeDecodeError.
        # La opción oficial conserva status/headers y solo omite bytes no UTF-8;
        # no desactiva checkers ni cambia la cobertura.
        "ignore_decoding_failures": True,
        "time_budget": time_budget,
        "save_results_in_fixed_dirname": True,
        "custom_retry_settings": {
            "status_codes": ["429"],
            "interval_sec": 1,
        },
        "authentication": {
            "token": {
                "module": {
                    "file": str(TOKEN_PROVIDER),
                    "function": "acquire_token",
                    "data": token_data,
                },
                "token_refresh_interval": 300,
            }
        },
        "per_resource_settings": {
            endpoint: {"custom_dictionary": str(path.resolve())}
            for endpoint, path in per_resource.items()
        },
    }
    if exclude_requests:
        settings["exclude_requests"] = exclude_requests
    if include_requests:
        settings["include_requests"] = include_requests
    return settings


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


def _provision_synthetic_data(http: HttpClient) -> SyntheticData:
    admin_state = Path(os.environ.get(
        "UNIHOME_ADMIN_STATE", ROOT_DIR / ".ci-state/admin.json"))
    admin = load_admin_state(admin_state)
    admin_tokens = _login(http, admin["email"], admin["password"])
    admin_access = str(admin_tokens["accessToken"])

    login_email = f"restler.login.{uuid.uuid4().hex}@uni.edu"
    login_password = _generate_password()
    login_code = _register(http, login_email, login_password)
    _confirm(http, login_email, login_code)
    login_tokens = _login(http, login_email, login_password)

    # Los recursos base pertenecen a la cuenta administradora porque el token
    # provider de Test/Fuzz usa esa misma cuenta. Así las rutas de unidad y
    # foto conservan la comprobación real de propiedad.
    university_id = _create_university(http, admin_access)
    property_id = _create_property(http, admin_access, university_id)
    unit_id = _create_unit(http, admin_access, property_id)
    _publish_unit_for_exploration(unit_id)
    photo_ref = _upload_photo_and_read_ref(http, admin_access, unit_id)

    confirm_email = f"restler.confirm.{uuid.uuid4().hex}@uni.edu"
    confirm_password = _generate_password()
    confirmation_code = _register(http, confirm_email, confirm_password)

    recovery_email = f"restler.recovery.{uuid.uuid4().hex}@uni.edu"
    recovery_password = _generate_password()
    recovery_code = _register(http, recovery_email, recovery_password)
    _confirm(http, recovery_email, recovery_code)
    reset_token = _request_password_reset(http, recovery_email)
    reset_password = _generate_password()

    recovery_request_email = f"restler.recovery.request.{uuid.uuid4().hex}@uni.edu"
    recovery_request_password = _generate_password()
    recovery_request_code = _register(
        http, recovery_request_email, recovery_request_password)
    _confirm(http, recovery_request_email, recovery_request_code)

    register_email = f"restler.register.{uuid.uuid4().hex}@uni.edu"
    register_password = _generate_password()
    delete_password = _generate_password()

    return SyntheticData(
        register_email=register_email,
        register_password=register_password,
        confirm_email=confirm_email,
        confirmation_code=confirmation_code,
        login_email=login_email,
        login_password=login_password,
        refresh_token=str(login_tokens["refreshToken"]),
        delete_password=delete_password,
        university_id=university_id,
        property_id=property_id,
        unit_id=unit_id,
        photo_ref=photo_ref,
        recovery_email=recovery_email,
        recovery_request_email=recovery_request_email,
        reset_token=reset_token,
        reset_password=reset_password,
    )


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

    data = _provision_synthetic_data(http)
    dictionaries = build_private_dictionaries(data)
    dictionary_paths: dict[str, Path] = {}
    for name, value in dictionaries.items():
        path = STATE_DIR / f"dict-{name}.json"
        _write_json(path, value, private=True)
        dictionary_paths[name] = path

    compile_dictionary = STATE_DIR / "compile-dict.json"
    _write_json(compile_dictionary, build_compile_dictionary(data), private=True)

    admin_state = Path(os.environ.get(
        "UNIHOME_ADMIN_STATE", ROOT_DIR / ".ci-state/admin.json"))
    admin_token_data = {
        "mode": "admin",
        "state_file": str(admin_state.resolve()),
    }
    delete_token_data = {
        "mode": "disposable",
        "password": data.delete_password,
    }
    excluded = [
        {"endpoint": "/api/v1/auth/logout", "methods": ["POST"]},
        {"endpoint": "/api/v1/cuentas/mi-cuenta", "methods": ["DELETE"]},
    ]
    logout_only = [
        {"endpoint": "/api/v1/auth/logout", "methods": ["POST"]},
    ]
    delete_only = [
        {"endpoint": "/api/v1/cuentas/mi-cuenta", "methods": ["DELETE"]},
    ]

    per_resource = {
        "/api/v1/auth/registro": dictionary_paths["registro"],
        "/api/v1/auth/confirmacion-correo": dictionary_paths["confirmacion"],
        "/api/v1/auth/confirmacion-correo/reenvio": dictionary_paths["reenvio"],
        "/api/v1/auth/login": dictionary_paths["login"],
        "/api/v1/auth/refresh": dictionary_paths["refresh"],
        "/api/v1/auth/recuperacion/solicitud": dictionary_paths["recovery-request"],
        "/api/v1/auth/recuperacion/confirmacion": dictionary_paths["recovery-confirm"],
        "/api/v1/cuentas/mi-cuenta": dictionary_paths["delete"],
        "/api/v1/admin/universidades/{id}": dictionary_paths["admin-update"],
        "/api/v1/admin/universidades/{id}/estado": dictionary_paths["admin-status"],
        "/api/v1/propiedades": dictionary_paths["property"],
        "/api/v1/propiedades/{propertyId}/unidades": dictionary_paths["unit"],
        "/api/v1/unidades/{unitId}/servicios": dictionary_paths["services"],
        "/api/v1/unidades/{unitId}/zona-reglas": dictionary_paths["zone"],
        "/api/v1/unidades/{unitId}/fotos": dictionary_paths["unit-path"],
        "/api/v1/unidades/{unitId}/vista-previa": dictionary_paths["unit-path"],
        "/api/v1/unidades/{unitId}/tarjeta": dictionary_paths["unit-path"],
        "/api/v1/unidades/{unitId}/ficha": dictionary_paths["unit-path"],
        "/api/v1/exploracion/mapa": dictionary_paths["exploration-query"],
        "/api/v1/exploracion/lista": dictionary_paths["exploration-query"],
        "/api/v1/favoritos/{unitId}": dictionary_paths["unit-path"],
        "/api/v1/fotos/{photoRef}": dictionary_paths["photo"],
    }

    test_settings = build_engine_settings(
        token_data=admin_token_data,
        per_resource=per_resource,
        max_sequence_length=10,
        max_combinations=3,
        time_budget=1,
        exclude_requests=excluded,
    )
    fuzz_settings = build_engine_settings(
        token_data=admin_token_data,
        per_resource=per_resource,
        max_sequence_length=10,
        max_combinations=2,
        time_budget=0.08,
        exclude_requests=excluded,
    )
    logout_settings = build_engine_settings(
        token_data=admin_token_data,
        per_resource=per_resource,
        max_sequence_length=3,
        max_combinations=2,
        time_budget=1,
        include_requests=logout_only,
    )
    delete_settings = build_engine_settings(
        token_data=delete_token_data,
        per_resource=per_resource,
        max_sequence_length=3,
        max_combinations=2,
        time_budget=1,
        include_requests=delete_only,
    )

    # Los IDs de entrada se fijan por diccionario. No se usa create_once:
    # un fallo de preprocesamiento abortaba TestMain antes de ejecutar el
    # catálogo completo.
    settings_paths = {
        "test-settings.json": test_settings,
        "fuzz-settings.json": fuzz_settings,
        "logout-settings.json": logout_settings,
        "delete-settings.json": delete_settings,
    }
    for name, value in settings_paths.items():
        _write_json(STATE_DIR / name, value, private=True)

    compile_config = build_compile_config(
        normalized_spec=WORK_DIR / "openapi-restler-30.json",
        compile_dictionary=compile_dictionary,
        engine_settings=STATE_DIR / "test-settings.json",
    )
    _write_json(STATE_DIR / "compile-config.json", compile_config, private=True)

    summary = {
        "target": TARGET,
        "source_openapi": source_spec.get("openapi"),
        "target_openapi": normalized_spec.get("openapi"),
        "operations": len(normalize_openapi.operation_keys(source_spec)),
        "transformation_changes": len(changes),
        "synthetic_university_prepared": True,
        "synthetic_property_prepared": True,
        "synthetic_unit_prepared": True,
        "synthetic_photo_prepared": True,
        "confirmation_account_prepared": True,
        "recovery_account_prepared": True,
        "admin_token_provider_configured": True,
        "destructive_operations_isolated": True,
        "fuzz_time_budget_hours": fuzz_settings["time_budget"],
    }
    _write_json(ARTIFACTS_DIR / "prepare-summary.json", summary)
    print(
        "RESTler preparado: "
        f"{summary['operations']} operaciones, "
        f"{summary['transformation_changes']} conversiones OpenAPI 3.1->3.0, "
        "recursos sintéticos y operaciones destructivas aisladas.")
    return summary


if __name__ == "__main__":  # pragma: no cover - entrada de infraestructura
    prepare()
