"""Pruebas del token provider de RESTler."""
import unittest
from unittest.mock import patch

import restler.token_provider as provider


class FakeHttp:
    def __init__(self, target, proxy):
        del target, proxy

    def request(self, method, path, *, body=None, expected=None, openapi=True, note=""):
        del body, openapi, note
        if path == "/api/v1/auth/registro":
            status, payload = 201, b'{"correoConfirmacionEnviado":true}'
        elif path == "/api/v1/auth/confirmacion-correo":
            status, payload = 200, b'{"correoConfirmado":true}'
        elif path == "/api/v1/auth/login":
            status, payload = 200, b'{"accessToken":"synthetic-token"}'
        else:
            raise AssertionError(f"Ruta inesperada: {method} {path}")
        if expected is not None and status not in expected:
            raise AssertionError("Estado inesperado")
        return status, payload, {}


class TokenProviderTests(unittest.TestCase):
    def test_format_token_no_expone_metadatos_extra(self):
        value = provider.format_token("synthetic-token")
        self.assertIn("Authorization: Bearer synthetic-token", value)
        self.assertTrue(value.startswith("{u'RESTler': {}}"))

    def test_acquire_token_usa_registro_confirmacion_y_login(self):
        logs = []
        with (
            patch.object(provider, "HttpClient", FakeHttp),
            patch.object(provider, "wait_for_verification_code", return_value="123456"),
        ):
            token = provider.acquire_token({"password": "synthetic-password"}, logs.append)
        self.assertIn("Authorization: Bearer synthetic-token", token)
        self.assertEqual(1, len(logs))
        self.assertNotIn("synthetic-token", logs[0])

    def test_acquire_token_sin_password_falla(self):
        with self.assertRaises(RuntimeError):
            provider.acquire_token({}, lambda _: None)


if __name__ == "__main__":
    unittest.main()
