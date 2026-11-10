"""Pruebas del doble SMTP de captura, sin abrir puertos."""
import asyncio
import sys
import tempfile
import types
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

import dast_smtp_capture


class CaptureTests(unittest.TestCase):
    def test_handler_escribe_el_mensaje_crudo(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory)
            handler = dast_smtp_capture.CaptureHandler(output)
            envelope = SimpleNamespace(
                original_content=b"Subject: prueba\r\n\r\n123456",
                content="otro",
            )
            result = asyncio.run(handler.handle_DATA(None, None, envelope))
            self.assertTrue(result.startswith("250 "))
            files = list(output.glob("*.eml"))
            self.assertEqual(1, len(files))
            self.assertIn(b"123456", files[0].read_bytes())

    def test_handler_usa_contenido_textual_si_no_hay_crudo(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory)
            handler = dast_smtp_capture.CaptureHandler(output)
            envelope = SimpleNamespace(original_content=None, content="texto")
            asyncio.run(handler.handle_DATA(None, None, envelope))
            self.assertIn(b"texto", next(output.glob("*.eml")).read_bytes())

    def test_authenticator_acepta_credenciales_sinteticas(self):
        fake_smtp = types.ModuleType("aiosmtpd.smtp")

        class AuthResult:
            def __init__(self, success):
                self.success = success

        fake_smtp.AuthResult = AuthResult
        with patch.dict(sys.modules, {
            "aiosmtpd": types.ModuleType("aiosmtpd"),
            "aiosmtpd.smtp": fake_smtp,
        }):
            result = dast_smtp_capture.authenticator(None, None, None, None, None)
        self.assertTrue(result.success)

    def test_build_controller_pasa_tls_y_auth(self):
        captured = {}
        fake_controller_module = types.ModuleType("aiosmtpd.controller")

        class Controller:
            def __init__(self, handler, **kwargs):
                captured["handler"] = handler
                captured.update(kwargs)

        fake_controller_module.Controller = Controller
        context = SimpleNamespace(
            minimum_version=None,
            load_cert_chain=lambda **kwargs: captured.setdefault("cert", kwargs),
        )
        with (
            patch.dict(sys.modules, {
                "aiosmtpd": types.ModuleType("aiosmtpd"),
                "aiosmtpd.controller": fake_controller_module,
            }),
            patch.object(dast_smtp_capture.ssl, "SSLContext", return_value=context),
        ):
            controller = dast_smtp_capture.build_controller(
                Path("mail"), "127.0.0.1", 587, Path("cert.pem"), Path("key.pem"))
        self.assertIsInstance(controller, Controller)
        self.assertTrue(captured["require_starttls"])
        self.assertTrue(captured["auth_required"])
        self.assertTrue(captured["auth_require_tls"])
        self.assertEqual("127.0.0.1", captured["hostname"])
        self.assertEqual(587, captured["port"])

    def test_main_inicia_y_detiene_el_controlador(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory)
            args = SimpleNamespace(
                output=output,
                host="127.0.0.1",
                port=587,
                cert=Path("cert.pem"),
                key=Path("key.pem"),
            )
            controller = SimpleNamespace(
                start=lambda: captured.setdefault("started", True),
                stop=lambda: captured.setdefault("stopped", True),
            )
            captured = {}
            with (
                patch.object(dast_smtp_capture, "parse_args", return_value=args),
                patch.object(dast_smtp_capture, "build_controller", return_value=controller),
                patch.object(dast_smtp_capture.time, "sleep", side_effect=KeyboardInterrupt),
            ):
                result = dast_smtp_capture.main()
            self.assertEqual(0, result)
            self.assertTrue(captured["started"])
            self.assertTrue(captured["stopped"])
            self.assertTrue((output / ".ready").exists())

    def test_parse_args_lee_argumentos(self):
        with patch.object(sys, "argv", [
            "dast_smtp_capture.py",
            "--host", "127.0.0.1",
            "--port", "587",
            "--cert", "cert.pem",
            "--key", "key.pem",
            "--output", "mail",
        ]):
            args = dast_smtp_capture.parse_args()
        self.assertEqual(587, args.port)
        self.assertEqual("mail", args.output.name)


if __name__ == "__main__":
    unittest.main()
