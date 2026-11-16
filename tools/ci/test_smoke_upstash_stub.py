"""Pruebas del stub HTTPS de Upstash usado por Container Smoke."""
import json
import threading
import unittest
from http.server import ThreadingHTTPServer
from urllib.error import HTTPError
from urllib.request import Request, urlopen

from smoke_upstash_stub import StubHandler, command_result


class SmokeUpstashCommandTests(unittest.TestCase):
    def test_ping_responde_pong(self):
        self.assertEqual("PONG", command_result(["PING"]))

    def test_eval_responde_resultado_permitido(self):
        self.assertEqual([1, 1, 60], command_result(["EVAL", "script", "1", "key"]))

    def test_comando_desconocido_o_invalido_falla(self):
        with self.assertRaises(ValueError):
            command_result(["GET", "key"])
        with self.assertRaises(ValueError):
            command_result([])
        with self.assertRaises(ValueError):
            command_result("PING")


class SmokeUpstashHttpTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.server = ThreadingHTTPServer(("127.0.0.1", 0), StubHandler)
        cls.thread = threading.Thread(target=cls.server.serve_forever, daemon=True)
        cls.thread.start()
        cls.base = f"http://127.0.0.1:{cls.server.server_port}"

    @classmethod
    def tearDownClass(cls):
        cls.server.shutdown()
        cls.server.server_close()
        cls.thread.join(timeout=5)

    def _post(self, command):
        request = Request(
            self.base,
            data=json.dumps(command).encode("utf-8"),
            headers={"Content-Type": "application/json"},
            method="POST",
        )
        with urlopen(request, timeout=5) as response:
            return response.status, json.loads(response.read())

    def test_get_health_responde_200(self):
        with urlopen(f"{self.base}/health", timeout=5) as response:
            self.assertEqual(200, response.status)
            self.assertEqual(
                {"status": "Healthy"},
                json.loads(response.read()),
            )

    def test_post_ping_y_eval(self):
        self.assertEqual((200, {"result": "PONG"}), self._post(["PING"]))
        self.assertEqual(
            (200, {"result": [1, 1, 60]}),
            self._post(["EVAL", "script", "1", "key"]),
        )

    def test_post_comando_no_soportado_responde_400(self):
        request = Request(
            self.base,
            data=json.dumps(["GET", "key"]).encode("utf-8"),
            headers={"Content-Type": "application/json"},
            method="POST",
        )
        with self.assertRaises(HTTPError) as context:
            urlopen(request, timeout=5)
        self.assertEqual(400, context.exception.code)


if __name__ == "__main__":
    unittest.main()
