"""Pruebas del preparador de datos sintéticos de RESTler."""
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import restler.restler_prepare as prepare

OPERATIONS = [
    ("get", "/api/v1/admin/universidades"),
    ("post", "/api/v1/admin/universidades"),
    ("put", "/api/v1/admin/universidades/{id}"),
    ("put", "/api/v1/admin/universidades/{id}/estado"),
    ("post", "/api/v1/auth/confirmacion-correo"),
    ("post", "/api/v1/auth/confirmacion-correo/reenvio"),
    ("post", "/api/v1/auth/login"),
    ("post", "/api/v1/auth/logout"),
    ("post", "/api/v1/auth/recuperacion/confirmacion"),
    ("post", "/api/v1/auth/recuperacion/solicitud"),
    ("post", "/api/v1/auth/refresh"),
    ("post", "/api/v1/auth/registro"),
    ("delete", "/api/v1/cuentas/mi-cuenta"),
    ("get", "/api/v1/fotos/{photoRef}"),
    ("get", "/api/v1/perfil/mi-perfil"),
    ("post", "/api/v1/propiedades"),
    ("post", "/api/v1/propiedades/{propertyId}/unidades"),
    ("post", "/api/v1/publicaciones/primera/terminos"),
    ("post", "/api/v1/unidades/{unitId}/fotos"),
    ("put", "/api/v1/unidades/{unitId}/servicios"),
    ("get", "/api/v1/unidades/{unitId}/vista-previa"),
    ("put", "/api/v1/unidades/{unitId}/zona-reglas"),
    ("get", "/api/v1/universidades"),
]

SPEC = {
    "openapi": "3.1.1",
    "paths": {
        path: {method: {} for method, candidate in OPERATIONS if candidate == path}
        for _, path in OPERATIONS
    },
}

DATA = prepare.SyntheticData(
    register_email="reg@uni.edu",
    register_password="register-password",
    confirm_email="confirm@uni.edu",
    confirmation_code="123456",
    login_email="login@uni.edu",
    login_password="login-password",
    refresh_token="refresh-synthetic",
    delete_password="delete-password",
    university_id="11111111-1111-1111-1111-111111111111",
    property_id="22222222-2222-2222-2222-222222222222",
    unit_id="33333333-3333-3333-3333-333333333333",
    photo_ref="photo-ref",
    recovery_email="recovery@uni.edu",
    recovery_request_email="recovery-request@uni.edu",
    reset_token="reset-token",
    reset_password="reset-password",
)


class FakeHttp:
    def __init__(self, target, proxy):
        del target, proxy

    def request(self, method, path, *, body=None, expected=None, openapi=True, note=""):
        del body, openapi, note
        if method == "GET" and path == "/openapi/v1.json":
            status, payload = 200, json.dumps(SPEC).encode()
        else:
            raise AssertionError(f"Ruta simulada inesperada: {method} {path}")
        if expected is not None and status not in expected:
            raise AssertionError(f"Estado {status} no esperado para {path}")
        return status, payload, {}


class RestlerPrepareTests(unittest.TestCase):
    def test_diccionarios_evitan_doble_quoting_y_fijan_ids(self):
        dictionaries = prepare.build_private_dictionaries(DATA)
        registro = dictionaries["registro"]["restler_custom_payload"]
        self.assertEqual("reg@uni.edu", registro["correo"][0])
        self.assertEqual("Mujer", registro["genero"][0])
        self.assertEqual("true", registro["aceptoAvisoPrivacidad"][0])
        self.assertEqual(
            "123456",
            dictionaries["confirmacion"]["restler_custom_payload"]["codigo"][0])
        self.assertEqual(
            "refresh-synthetic",
            dictionaries["refresh"]["restler_custom_payload"]["refreshToken"][0])
        self.assertEqual(
            "photo-ref",
            dictionaries["photo"]["restler_custom_payload"]["photoRef"][0])
        self.assertEqual(
            DATA.university_id,
            dictionaries["property"]["restler_custom_payload"]["universidadId"][0])
        self.assertEqual(
            "19.4326",
            dictionaries["property"]["restler_custom_payload"]["latitud"][0])
        self.assertEqual(
            DATA.property_id,
            dictionaries["unit"]["restler_custom_payload"]["propertyId"][0])
        self.assertEqual(
            DATA.unit_id,
            dictionaries["services"]["restler_custom_payload"]["unitId"][0])

    def test_compile_dictionary_fija_ids_y_no_duplica_quotes(self):
        dictionary = prepare.build_compile_dictionary(DATA)
        payload = dictionary["restler_custom_payload"]
        self.assertEqual(DATA.property_id, payload["propertyId"][0])
        self.assertEqual(DATA.unit_id, payload["unitId"][0])
        self.assertEqual(DATA.university_id, payload["id"][0])
        self.assertEqual("placeholder@uni.edu", payload["correo"][0])
        self.assertEqual("RESTler Universidad", payload["nombreYCampus"][0])
        self.assertNotIn("nombre", payload)
        self.assertNotIn('"', "".join(payload["codigo"]))

    def test_settings_contiene_limites_autenticacion_y_aislamiento(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            settings = prepare.build_engine_settings(
                token_data={"mode": "admin", "state_file": "state.json"},
                per_resource={"/api/v1/auth/login": root / "login.json"},
                max_sequence_length=5,
                max_combinations=3,
                time_budget=1,
                exclude_requests=[{"endpoint": "/api/v1/auth/logout"}],
            )
        self.assertEqual(3, settings["max_combinations"])
        self.assertEqual(5, settings["max_sequence_length"])
        self.assertEqual(20, settings["max_request_execution_time"])
        self.assertTrue(settings["ignore_decoding_failures"])
        self.assertTrue(settings["save_results_in_fixed_dirname"])
        self.assertEqual(
            "acquire_token",
            settings["authentication"]["token"]["module"]["function"])
        self.assertEqual("admin",
                         settings["authentication"]["token"]["module"]["data"]["mode"])
        self.assertIn("/api/v1/auth/login", settings["per_resource_settings"])
        self.assertIn("exclude_requests", settings)

    def test_prepare_escribe_estado_privado_y_reporte_publico(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            artifacts = root / "artifacts"
            work = root / "work"
            state = root / "state"
            mailbox = root / "mail"
            admin_state = root / "admin.json"
            with (
                patch.object(prepare, "ARTIFACTS_DIR", artifacts),
                patch.object(prepare, "WORK_DIR", work),
                patch.object(prepare, "STATE_DIR", state),
                patch.object(prepare, "MAILBOX_DIR", mailbox),
                patch.object(prepare, "TOKEN_PROVIDER", root / "token_provider.py"),
                patch.object(prepare, "HttpClient", FakeHttp),
                patch.object(prepare, "_provision_synthetic_data", return_value=DATA),
                patch.dict("os.environ", {"UNIHOME_ADMIN_STATE": str(admin_state)}),
            ):
                summary = prepare.prepare()

            self.assertEqual(23, summary["operations"])
            self.assertTrue(summary["destructive_operations_isolated"])
            self.assertTrue(summary["admin_token_provider_configured"])
            self.assertTrue((artifacts / "openapi-original.json").exists())
            self.assertTrue((artifacts / "openapi-restler-30.json").exists())
            self.assertTrue((artifacts / "openapi-transformation.json").exists())
            self.assertTrue((state / "test-settings.json").exists())
            self.assertTrue((state / "fuzz-settings.json").exists())
            self.assertTrue((state / "logout-settings.json").exists())
            self.assertTrue((state / "delete-settings.json").exists())
            self.assertTrue((state / "dict-registro.json").exists())
            self.assertTrue((state / "compile-config.json").exists())

            test_settings = json.loads(
                (state / "test-settings.json").read_text(encoding="utf-8"))
            delete_settings = json.loads(
                (state / "delete-settings.json").read_text(encoding="utf-8"))
            self.assertTrue(test_settings["exclude_requests"])
            self.assertEqual(
                "disposable",
                delete_settings["authentication"]["token"]["module"]["data"]["mode"])
            self.assertEqual(0.08, json.loads(
                (state / "fuzz-settings.json").read_text(encoding="utf-8"))["time_budget"])
            for name in ("test", "fuzz", "logout", "delete"):
                settings = json.loads(
                    (state / f"{name}-settings.json").read_text(encoding="utf-8"))
                self.assertTrue(settings["ignore_decoding_failures"], name)
            for endpoint in (
                "/api/v1/propiedades",
                "/api/v1/propiedades/{propertyId}/unidades",
                "/api/v1/admin/universidades",
            ):
                self.assertNotIn(
                    "create_once",
                    test_settings["per_resource_settings"].get(endpoint, {}))

            public = (artifacts / "prepare-summary.json").read_text(encoding="utf-8")
            self.assertNotIn("register-password", public)
            self.assertNotIn("synthetic-refresh", public)
            self.assertNotIn("123456", public)
            self.assertNotIn("reset-token", public)


if __name__ == "__main__":
    unittest.main()
