"""Pruebas del aprovisionamiento del administrador sintético."""
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import provision_admin


class FakeHttp:
    def __init__(self, target, proxy):
        del target, proxy
        self.calls = []

    def request(self, method, path, *, body=None, expected=None, openapi=True, note=""):
        del openapi, note
        self.calls.append((method, path, body))
        if path == "/api/v1/auth/registro":
            status, payload = 201, b'{"id":"11111111-1111-1111-1111-111111111111","correoConfirmacionEnviado":true}'
        elif path == "/api/v1/auth/confirmacion-correo":
            status, payload = 200, b'{"correoConfirmado":true}'
        elif path == "/api/v1/auth/login":
            status, payload = 200, b'{"accessToken":"synthetic-token","refreshToken":"synthetic-refresh"}'
        else:
            raise AssertionError(f"Ruta inesperada: {method} {path}")
        if expected is not None and status not in expected:
            raise AssertionError("Estado inesperado")
        return status, payload, {}


class ProvisionAdminTests(unittest.TestCase):
    def test_provisiona_cuenta_y_guarda_estado_privado(self):
        with tempfile.TemporaryDirectory() as directory:
            state_file = Path(directory) / ".ci-state" / "admin.json"
            with (
                patch.object(provision_admin, "HttpClient", FakeHttp),
                patch.object(
                    provision_admin, "wait_for_verification_code",
                    return_value="123456"),
            ):
                state = provision_admin.provision(
                    "http://127.0.0.1:5088", Path(directory), state_file)

            self.assertTrue(state_file.exists())
            saved = json.loads(state_file.read_text(encoding="utf-8"))
            self.assertEqual(state, saved)
            self.assertEqual(
                "11111111-1111-1111-1111-111111111111",
                saved["accountId"])
            self.assertTrue(saved["email"].startswith("admin.dast."))
            self.assertNotIn(saved["password"], json.dumps(saved["accountId"]))

    def test_registro_sin_id_falla(self):
        class NoIdHttp(FakeHttp):
            def request(self, method, path, *, body=None, expected=None,
                        openapi=True, note=""):
                del method, path, body, expected, openapi, note
                return 201, b'{"correoConfirmacionEnviado":true}', {}

        with tempfile.TemporaryDirectory() as directory:
            with patch.object(provision_admin, "HttpClient", NoIdHttp):
                with self.assertRaises(provision_admin.ProvisionError):
                    provision_admin.provision(
                        "http://127.0.0.1:5088",
                        Path(directory),
                        Path(directory) / "admin.json")


if __name__ == "__main__":
    unittest.main()
