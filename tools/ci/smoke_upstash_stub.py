#!/usr/bin/env python3
"""Stub HTTPS mínimo de Upstash REST para el Container Smoke aislado.

No sustituye Redis en Production: solo permite que la imagen real, arrancada
con ASPNETCORE_ENVIRONMENT=Production, ejerza /health/ready y el rate limiting
contra un endpoint HTTPS local. No usa secretos reales ni red externa.
"""
from __future__ import annotations

import argparse
import json
import ssl
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from typing import Any


def command_result(command: Any) -> Any:
    if not isinstance(command, list) or not command:
        raise ValueError("El comando Redis debe ser un arreglo no vacío.")
    name = str(command[0]).upper()
    if name == "PING":
        return "PONG"
    if name == "EVAL":
        # [permitido, contador, ttl] equivalente al script Lua de la API.
        return [1, 1, 60]
    raise ValueError(f"Comando no soportado por el stub: {name}")


class StubHandler(BaseHTTPRequestHandler):
    def _write_json(self, status: int, payload: dict[str, Any]) -> None:
        body = json.dumps(payload).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self) -> None:  # noqa: N802 - firma de BaseHTTPRequestHandler
        if self.path == "/health":
            self._write_json(200, {"status": "Healthy"})
            return
        self._write_json(404, {"error": "not found"})

    def do_POST(self) -> None:  # noqa: N802 - firma de BaseHTTPRequestHandler
        try:
            length = int(self.headers.get("Content-Length", "0"))
            command = json.loads(self.rfile.read(length) or b"[]")
            self._write_json(200, {"result": command_result(command)})
        except (ValueError, json.JSONDecodeError) as exception:
            self._write_json(400, {"error": str(exception)})

    def log_message(self, format: str, *args: Any) -> None:
        # Los smoke logs no deben incluir tokens ni cuerpos.
        del format, args


def main() -> int:  # pragma: no cover - entrada de infraestructura
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, required=True)
    parser.add_argument("--cert", required=True)
    parser.add_argument("--key", required=True)
    args = parser.parse_args()

    server = ThreadingHTTPServer((args.host, args.port), StubHandler)
    context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
    context.load_cert_chain(certfile=args.cert, keyfile=args.key)
    server.socket = context.wrap_socket(server.socket, server_side=True)
    try:
        server.serve_forever()
    finally:
        server.server_close()
    return 0


if __name__ == "__main__":  # pragma: no cover - entrada de infraestructura
    raise SystemExit(main())
