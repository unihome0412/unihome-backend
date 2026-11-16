"""Token provider de RESTler para la API efímera de UniHome.

Modos admitidos:

- ``admin``: inicia sesión con la cuenta administradora sintética provisionada
  por ``provision_admin.py``. Es el modo de las ejecuciones principales Test y
  Fuzz-lean, porque los endpoints ``/admin/*`` exigen ese privilegio.
- ``disposable``: crea una cuenta sintética nueva, la confirma con el código
  capturado por el SMTP local y hace login. Se usa en la ejecución aislada de
  ``DELETE /api/v1/cuentas/mi-cuenta`` para que el borrado no invalide el token
  de las demás operaciones.

No se escriben tokens ni contraseñas en logs ni artefactos.
"""
from __future__ import annotations

import json
import sys
import uuid
from pathlib import Path
from typing import Any, Callable


sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from zap_dast import (  # noqa: E402
    HttpClient,
    _json_body,
    _register_payload,
    wait_for_verification_code,
)

ROOT_DIR = Path(__file__).resolve().parents[3]
TARGET = "http://127.0.0.1:5088"
MAILBOX_DIR = ROOT_DIR / ".dast-mail"


def format_token(access_token: str) -> str:
    if not access_token:
        raise ValueError("El login no devolvió un access token.")
    return (
        "{u'RESTler': {}}\n"
        f"Authorization: Bearer {access_token}\n"
    )


def _state_from_data(data: dict[str, Any]) -> Path:
    configured = str(data.get("state_file") or "").strip()
    return Path(configured) if configured else ROOT_DIR / ".ci-state/admin.json"


def _read_state(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise RuntimeError(
            "No se encontró el estado de la cuenta administradora sintética."
        ) from error
    if not isinstance(value, dict):
        raise RuntimeError("El estado del administrador no es un objeto JSON.")
    return value


def _login(
    http: HttpClient,
    email: str,
    password: str,
    log: Callable[[str], None],
) -> str:
    _, payload, _ = http.request(
        "POST", "/api/v1/auth/login",
        body={"correo": email, "contrasena": password},
        expected={200}, note="token login")
    tokens = _json_body(payload)
    access_token = str(tokens.get("accessToken") or "")
    if not access_token:
        raise RuntimeError("El login del token provider no devolvió accessToken.")
    return access_token


def _acquire_admin_token(
    data: dict[str, Any],
    log: Callable[[str], None],
) -> str:
    state = _read_state(_state_from_data(data))
    email = str(state.get("email") or "")
    password = str(state.get("password") or "")
    if not email or not password:
        raise RuntimeError(
            "El estado del administrador no contiene email y contraseña.")
    http = HttpClient(TARGET, None)
    access_token = _login(http, email, password, log)
    log("Token RESTler renovado con la cuenta administradora sintética.")
    return format_token(access_token)


def _acquire_disposable_token(
    data: dict[str, Any],
    log: Callable[[str], None],
) -> str:
    password = str(data.get("password") or "")
    if not password:
        raise RuntimeError(
            "El token provider no recibió la contraseña sintética desechable.")

    http = HttpClient(TARGET, None)
    email = f"restler.token.{uuid.uuid4().hex}@uni.edu"
    _, payload, _ = http.request(
        "POST", "/api/v1/auth/registro",
        body=_register_payload(email, password),
        expected={201}, note="token registro")
    response = _json_body(payload)
    if response.get("correoConfirmacionEnviado") is not True:
        raise RuntimeError("El registro del token provider no envió correo.")

    code = wait_for_verification_code(MAILBOX_DIR, email)
    http.request(
        "POST", "/api/v1/auth/confirmacion-correo",
        body={"correo": email, "codigo": code},
        expected={200}, note="token confirmación")

    access_token = _login(http, email, password, log)
    log("Token RESTler desechable renovado mediante registro, confirmación y login.")
    return format_token(access_token)


def acquire_token(data: dict[str, Any], log: Callable[[str], None]) -> str:
    mode = str(data.get("mode") or "disposable")
    if mode == "admin":
        return _acquire_admin_token(data, log)
    if mode == "disposable":
        return _acquire_disposable_token(data, log)
    raise RuntimeError(f"Modo de token provider no soportado: {mode!r}")
