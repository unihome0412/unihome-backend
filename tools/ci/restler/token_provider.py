"""Token provider de RESTler para la API efímera de UniHome.

Cada refresco crea una cuenta sintética nueva, la confirma con el código
capturado por el SMTP local y hace login mediante los endpoints reales. No se
escriben tokens ni contraseñas en logs ni artefactos.
"""
from __future__ import annotations

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


def acquire_token(data: dict[str, Any], log: Callable[[str], None]) -> str:
    password = str(data.get("password") or "")
    if not password:
        raise RuntimeError("El token provider no recibió la contraseña sintética.")

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

    _, login_payload, _ = http.request(
        "POST", "/api/v1/auth/login",
        body={"correo": email, "contrasena": password},
        expected={200}, note="token login")
    tokens = _json_body(login_payload)
    access_token = str(tokens.get("accessToken") or "")
    log("Token RESTler renovado mediante registro, confirmación y login reales.")
    return format_token(access_token)
