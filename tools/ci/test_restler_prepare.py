"""Pruebas del preparador de datos sintéticos de RESTler."""
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import restler.restler_prepare as prepare


SPEC = {
    "openapi": "3.1.1",
    "paths": {
        "/api/v1/auth/registro": {"post": {}},
        "/api/v1/auth/confirmacion-correo": {"post": {}},
        "/api/v1/auth/confirmacion-correo/reenvio": {"post": {}},
        "/api/v1/auth/login": {"post": {}},
        "/api/v1/auth/refresh": {"post": {}},
        "/api/v1/auth/logout": {"post": {}},
        "/api/v1/cuentas/mi-cuenta": {"delete": {}},
    },
}


class FakeHttp:
    def __init__(self, target, proxy):
        del target, proxy

    def request(self, method, path, *, body=None, expected=None, openapi=True, note=""):
        del body, openapi, note
        if method == "GET" and path == "/openapi/v1.json":
            status, payload = 200, json.dumps(SPEC).encode()
        elif method == "POST" and path == "/api/v1/auth/registro":
            status, payload = 201, b'{"correoConfirmacionEnviado":true}'
        elif method == "POST" and path == "/api/v1/auth/confirmacion-correo":
            status, payload = 200, b'{"correoConfirmado":true}'
        elif method == "POST" and path == "/api/v1/auth/login":
            status, payload = 200, b'{"accessToken":"synthetic-access","refreshToken":"synthetic-refresh"}'
        else:
            raise AssertionError(f"Ruta simulada inesperada: {method} {path}")
        if expected is not None and status not in expected:
            raise AssertionError(f"Estado {status} no esperado para {path}")
        return status, payload, {}


class RestlerPrepareTests(unittest.TestCase):
    def test_diccionarios_conservan_valores_sin_tokens_reales(self):
        dictionaries = prepare.build_private_dictionaries(
            register_email="reg@uni.edu",
            register_password="register-password",
            confirm_email="confirm@uni.edu",
            confirmation_code="123456",
            login_email="login@uni.edu",
            login_password="login-password",
            refresh_token="refresh-synthetic",
            token_password="token-password",
        )
        self.assertEqual(
            "reg@uni.edu",
            dictionaries["registro"]["restler_custom_payload"]["correo"][0])
        self.assertEqual(
            "123456",
            dictionaries["confirmacion"]["restler_custom_payload"]["codigo"][0])
        self.assertEqual(
            "refresh-synthetic",
            dictionaries["refresh"]["restler_custom_payload"]["refreshToken"][0])
        self.assertEqual(
            '"Mujer"',
            dictionaries["registro"]["restler_custom_payload"]["genero"][0])

    def test_settings_contiene_limites_y_modulo_de_autenticacion(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            settings = prepare.build_engine_settings(
                token_password="token-password",
                per_resource={"/api/v1/auth/login": root / "login.json"},
            )
        self.assertEqual(3, settings["max_combinations"])
        self.assertEqual(2, settings["max_sequence_length"])
        self.assertEqual(20, settings["max_request_execution_time"])
        self.assertTrue(settings["save_results_in_fixed_dirname"])
        self.assertEqual(
            "acquire_token",
            settings["authentication"]["token"]["module"]["function"])
        self.assertIn("/api/v1/auth/login", settings["per_resource_settings"])

    def test_prepare_escribe_estado_privado_y_reporte_publico(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            artifacts = root / "artifacts"
            work = root / "work"
            state = root / "state"
            mailbox = root / "mail"
            with (
                patch.object(prepare, "ARTIFACTS_DIR", artifacts),
                patch.object(prepare, "WORK_DIR", work),
                patch.object(prepare, "STATE_DIR", state),
                patch.object(prepare, "MAILBOX_DIR", mailbox),
                patch.object(prepare, "TOKEN_PROVIDER", root / "token_provider.py"),
                patch.object(prepare, "HttpClient", FakeHttp),
                patch.object(prepare, "wait_for_verification_code", return_value="123456"),
            ):
                summary = prepare.prepare()

            self.assertEqual(7, summary["operations"])
            self.assertTrue((artifacts / "openapi-original.json").exists())
            self.assertTrue((artifacts / "openapi-restler-30.json").exists())
            self.assertTrue((artifacts / "openapi-transformation.json").exists())
            self.assertTrue((state / "engine-settings.json").exists())
            self.assertTrue((state / "dict-registro.json").exists())
            self.assertTrue((state / "compile-config.json").exists())
            public = (artifacts / "prepare-summary.json").read_text(encoding="utf-8")
            self.assertNotIn("register-password", public)
            self.assertNotIn("synthetic-refresh", public)
            self.assertNotIn("123456", public)
            settings = json.loads((state / "engine-settings.json").read_text(encoding="utf-8"))
            self.assertIn("password", settings["authentication"]["token"]["module"]["data"])


if __name__ == "__main__":
    unittest.main()
