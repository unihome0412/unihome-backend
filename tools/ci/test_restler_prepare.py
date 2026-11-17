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
SPEC["paths"]["/api/v1/unidades/{unitId}/fotos"]["post"]["requestBody"] = {
    "required": True,
    "content": {
        "multipart/form-data": {
            "schema": {
                "type": "object",
                "properties": {
                    "Fotos": {
                        "type": "array",
                        "items": {"type": "string", "format": "binary"},
                    },
                },
                "required": ["Fotos"],
            },
            "encoding": {"Fotos": {"style": "form", "explode": True}},
        },
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
    admin_university_id="88888888-8888-8888-8888-888888888888",
    property_id="22222222-2222-2222-2222-222222222222",
    unit_id="33333333-3333-3333-3333-333333333333",
    owner_unit_id="77777777-7777-7777-7777-777777777777",
    photo_ref="photo-ref",
    recovery_email="recovery@uni.edu",
    recovery_request_email="recovery-request@uni.edu",
    reset_token="reset-token",
    reset_password="reset-password",
    detail_expediente_id="44444444-4444-4444-4444-444444444444",
    approve_expediente_id="55555555-5555-5555-5555-555555555555",
    reject_expediente_id="66666666-6666-6666-6666-666666666666",
    document_access_token="document-token-synthetic",
)


class FakeHttp:
    def __init__(self, target, proxy):
        del target, proxy

    def request(
        self, method, path, *, body=None, token=None, expected=None,
        auth=False, openapi=True, note="",
    ):
        del body, auth, openapi, note
        if method == "POST" and path == "/api/v1/auth/login":
            status, payload = 200, json.dumps({
                "accessToken": "admin-token",
                "refreshToken": "admin-refresh",
            }).encode()
        elif method == "GET" and path == "/openapi/v1.json":
            if token != "admin-token":
                raise AssertionError("El contrato debe descargarse con Bearer administrativo.")
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
        self.assertEqual(
            DATA.owner_unit_id,
            dictionaries["owner-unit"]["restler_custom_payload"]["unitId"][0])
        self.assertEqual(
            DATA.admin_university_id,
            dictionaries["admin-update"]["restler_custom_payload"]["id"][0])
        self.assertEqual(
            DATA.admin_university_id,
            dictionaries["admin-status"]["restler_custom_payload"]["id"][0])
        exploration = dictionaries["exploration-query"]["restler_custom_payload"]
        self.assertEqual(DATA.university_id, exploration["universidadId"][0])
        self.assertEqual("Mixto", exploration["paraQuien"][0])
        self.assertEqual("true", exploration["soloIndividual"][0])
        self.assertEqual(
            DATA.detail_expediente_id,
            dictionaries["admin-detail"]["restler_custom_payload"]["id"][0])
        self.assertEqual(
            DATA.approve_expediente_id,
            dictionaries["admin-approve"]["restler_custom_payload"]["id"][0])
        self.assertEqual(
            DATA.reject_expediente_id,
            dictionaries["admin-reject"]["restler_custom_payload"]["id"][0])
        self.assertEqual(
            DATA.document_access_token,
            dictionaries["document-token"]["restler_custom_payload"]["token"][0])

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

    def test_fixture_admin_crea_envio_antes_de_la_unidad_publica(self):
        calls = []
        universities = []

        def fake_university(http, token):
            del http, token
            universities.append("university")
            return "university"

        def fake_prepare(http, token, university_id, *, submit, label):
            del http, university_id
            calls.append(("prepare", label, submit, token))
            return f"unit-{label}", f"exp-{label}"

        def fake_property(http, token, university_id):
            del http, university_id
            calls.append(("property", token))
            return f"property-{token}"

        def fake_unit(http, token, property_id):
            del http, property_id
            calls.append(("unit", token))
            return f"unit-{token}"

        def fake_photo(http, token, unit_id):
            del http, unit_id
            calls.append(("photo", token))
            return f"photo-{token}"

        def fake_publish(unit_id):
            del unit_id
            calls.append(("publish",))

        def fake_owner(http, password):
            del http, password
            calls.append(("owner",))
            return "owner-token"

        def fake_login(http, email, password):
            del http, email, password
            return {"accessToken": "access-token", "refreshToken": "refresh-token"}

        with (
            patch.object(prepare, "_create_university", side_effect=fake_university),
            patch.object(prepare, "_prepare_expediente", side_effect=fake_prepare),
            patch.object(prepare, "_create_property", side_effect=fake_property),
            patch.object(prepare, "_create_unit", side_effect=fake_unit),
            patch.object(prepare, "_upload_photo_and_read_ref", side_effect=fake_photo),
            patch.object(prepare, "_publish_unit_for_exploration", side_effect=fake_publish),
            patch.object(prepare, "_create_owner_session", side_effect=fake_owner),
            patch.object(prepare, "_document_access_token", return_value="document-token"),
            patch.object(prepare, "_register", return_value="123456"),
            patch.object(prepare, "_confirm", return_value=None),
            patch.object(prepare, "_login", side_effect=fake_login),
            patch.object(prepare, "_generate_password", return_value="synthetic-password"),
            patch.object(prepare, "_request_password_reset", return_value="reset-token"),
        ):
            data = prepare._provision_synthetic_data(object(), "admin-token")

        # La unidad pública no puede existir antes del primer expediente del
        # administrador: SubmitFirstPublicationService elige la unidad más
        # antigua y devolvería 409 si ya estuviera Aprobada.
        self.assertEqual(("prepare", "admin detalle", True), calls[0][:3])
        self.assertEqual(("prepare", "admin envío", False), calls[1][:3])
        self.assertEqual(("property", "admin-token"), calls[2])
        self.assertEqual(("owner",), calls[3])
        self.assertEqual(("property", "owner-token"), calls[4])
        self.assertEqual(("unit", "owner-token"), calls[5])
        self.assertEqual(2, len(universities))
        self.assertEqual("property-admin-token", data.property_id)
        self.assertEqual("unit-owner-token", data.unit_id)
        self.assertEqual("unit-admin detalle", data.owner_unit_id)
        self.assertEqual("photo-owner-token", data.photo_ref)
        self.assertEqual("exp-admin detalle", data.detail_expediente_id)
        self.assertEqual("exp-aprobar", data.approve_expediente_id)
        self.assertEqual("exp-rechazar", data.reject_expediente_id)
        self.assertEqual("document-token", data.document_access_token)

    def test_prepare_escribe_estado_privado_y_reporte_publico(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            artifacts = root / "artifacts"
            work = root / "work"
            state = root / "state"
            mailbox = root / "mail"
            admin_state = root / "admin.json"
            admin_state.write_text(
                json.dumps({"email": "admin@uni.edu", "password": "synthetic"}),
                encoding="utf-8")
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
            for endpoint in (
                "/api/v1/unidades/{unitId}/fotos",
                "/api/v1/unidades/{unitId}/servicios",
                "/api/v1/unidades/{unitId}/zona-reglas",
                "/api/v1/unidades/{unitId}/vista-previa",
            ):
                self.assertTrue(
                    test_settings["per_resource_settings"][endpoint][
                        "custom_dictionary"
                    ].endswith("dict-owner-unit.json"),
                    endpoint,
                )

            public = (artifacts / "prepare-summary.json").read_text(encoding="utf-8")
            self.assertNotIn("register-password", public)
            self.assertNotIn("synthetic-refresh", public)
            self.assertNotIn("123456", public)
            self.assertNotIn("reset-token", public)


if __name__ == "__main__":
    unittest.main()
