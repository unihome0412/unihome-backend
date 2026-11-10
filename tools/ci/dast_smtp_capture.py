#!/usr/bin/env python3
"""Servidor SMTP de captura para el job DAST aislado.

Solo se ejecuta en el runner efímero de GitHub Actions, escucha en loopback y
exige STARTTLS y AUTH. No se usa en producción ni sustituye a un proveedor real:
su único objetivo es capturar el código de confirmación que la propia API envía
durante el flujo legítimo de registro.
"""
from __future__ import annotations

import argparse
import ssl
import time
import uuid
from pathlib import Path
from typing import Any


class CaptureHandler:
    """Escribe cada mensaje aceptado como un archivo .eml en el directorio."""

    def __init__(self, output: Path) -> None:
        self._output = output

    async def handle_DATA(self, server, session, envelope) -> str:  # noqa: N802
        del server, session
        content = envelope.original_content
        if content is None:
            content = envelope.content.encode("utf-8", errors="replace")

        name = f"{time.time_ns()}-{uuid.uuid4().hex}.eml"
        target = self._output / name
        target.write_bytes(content)
        return "250 Mensaje aceptado para el escaneo DAST"


def authenticator(server, session, envelope, mechanism, auth_data) -> Any:
    """Acepta cualquier credencial sintética; no hay usuarios reales."""
    from aiosmtpd.smtp import AuthResult

    del server, session, envelope, mechanism, auth_data
    return AuthResult(success=True)


def build_controller(
    output: Path,
    host: str,
    port: int,
    cert_file: Path,
    key_file: Path,
) -> Any:
    from aiosmtpd.controller import Controller

    context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
    context.minimum_version = ssl.TLSVersion.TLSv1_2
    context.load_cert_chain(certfile=str(cert_file), keyfile=str(key_file))

    return Controller(
        CaptureHandler(output),
        hostname=host,
        port=port,
        tls_context=context,
        require_starttls=True,
        auth_required=True,
        auth_require_tls=True,
        authenticator=authenticator,
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=587)
    parser.add_argument("--cert", required=True, type=Path)
    parser.add_argument("--key", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    args.output.mkdir(parents=True, exist_ok=True)

    controller = build_controller(
        args.output, args.host, args.port, args.cert, args.key)
    controller.start()

    ready = args.output / ".ready"
    ready.write_text("ready\n", encoding="utf-8")
    print(f"SMTP de captura escuchando en {args.host}:{args.port}", flush=True)

    try:
        while True:
            time.sleep(3600)
    except KeyboardInterrupt:
        pass
    finally:
        controller.stop()

    return 0


if __name__ == "__main__":  # pragma: no cover - proceso de infraestructura
    raise SystemExit(main())
