#!/usr/bin/env python3
"""Provisiona la cuenta administradora sintética de los harnesses DAST/RESTler.

La cuenta se crea y confirma mediante los endpoints reales de la API efímera.
El identificador se escribe en un estado privado para que el workflow reinicie
la API con `Admin__AccountIds__0` apuntando a esa cuenta. No se imprimen ni se
publican contraseñas ni tokens.
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import uuid
from pathlib import Path
from typing import Any

sys.path.insert(0, str(Path(__file__).resolve().parent))

from zap_dast import (  # noqa: E402
    HttpClient,
    _generate_password,
    _json_body,
    _register_payload,
    wait_for_verification_code,
)

ROOT_DIR = Path(__file__).resolve().parents[2]
# Rutas fijas: no se aceptan rutas de archivo por CLI para evitar que un
# argumento manipulable llegue a operaciones de lectura/escritura/chmod.
STATE_FILE = ROOT_DIR / ".ci-state" / "admin.json"
MAILBOX_DIR = ROOT_DIR / ".dast-mail"


class ProvisionError(RuntimeError):
    """Error controlado durante la provisión del administrador sintético."""


def write_private_json(value: dict[str, Any]) -> None:
    STATE_FILE.parent.mkdir(parents=True, exist_ok=True)
    STATE_FILE.write_text(
        json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")
    try:
        os.chmod(STATE_FILE, 0o600)
    except OSError:
        pass


def provision(target: str) -> dict[str, Any]:
    password = _generate_password()
    email = f"admin.dast.{uuid.uuid4().hex}@uni.edu"
    http = HttpClient(target, None)

    _, payload, _ = http.request(
        "POST", "/api/v1/auth/registro",
        body=_register_payload(email, password),
        expected={201}, note="registro administrador sintético")
    registration = _json_body(payload)
    account_id = str(registration.get("id") or "")
    if not account_id:
        raise ProvisionError("El registro del administrador no devolvió id.")
    if registration.get("correoConfirmacionEnviado") is not True:
        raise ProvisionError(
            "El SMTP de captura no confirmó el envío del administrador.")

    code = wait_for_verification_code(MAILBOX_DIR, email)
    http.request(
        "POST", "/api/v1/auth/confirmacion-correo",
        body={"correo": email, "codigo": code},
        expected={200}, note="confirmación administrador sintético")

    _, login_payload, _ = http.request(
        "POST", "/api/v1/auth/login",
        body={"correo": email, "contrasena": password},
        expected={200}, note="login administrador sintético")
    tokens = _json_body(login_payload)
    if not tokens.get("accessToken"):
        raise ProvisionError("El login del administrador no devolvió token.")

    state = {
        "email": email,
        "password": password,
        "accountId": account_id,
    }
    write_private_json(state)
    return state


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--target", required=True)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        provision(args.target)
    except Exception as error:
        print(
            f"::error::No se pudo provisionar el administrador sintético: "
            f"{type(error).__name__}: {error}",
            file=sys.stderr,
        )
        return 1

    print("Administrador sintético provisionado y confirmado.")
    return 0


if __name__ == "__main__":  # pragma: no cover - entrada de infraestructura
    raise SystemExit(main())
