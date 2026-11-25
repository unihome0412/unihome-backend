"""
Script para poblar 'unihome_verificacion' (la base de datos a la que está conectado
el backend desplegado en Azure Container Apps).

Popula:
  1. Universidad Tecnológica de Tula-Tepeji (UTTT) con ubicación GeoJSON.
  2. Términos de publicación para unihome0412@gmail.com (ID: 154dd2d0c9f94a16a6e35319aa350ecd).
  3. 3 Propiedades en Tula de Allende:
     - Residencial Los Arcos (Casa - Col. El Carmen)
     - Departamentos Universitaria (Edificio - Col. Jalpa)
     - Estudios San Marcos (Estudio - Col. San Marcos)
  4. 3 Unidades en renta (Habitación, Departamento, Estudio) en estado 'Aprobada' y 'Disponible'.
"""
import unicodedata
import uuid
from datetime import datetime, timezone
from bson.decimal128 import Decimal128
from pymongo import MongoClient

CONN = (
    "mongodb+srv://unihome0412_db_user:0d18wEzlRHuDkPor"
    "@unihome.gelwqtb.mongodb.net/"
    "?retryWrites=true&w=majority"
)
DB_NAME = "unihome_verificacion"

UTTT_LNG = -99.3400
UTTT_LAT = 20.0135


def normalize(value: str) -> str:
    trimmed = value.strip()
    decomposed = unicodedata.normalize("NFD", trimmed)
    chars = [ch.lower() for ch in decomposed if unicodedata.category(ch) != "Mn"]
    return " ".join("".join(chars).split())


def new_id() -> str:
    return uuid.uuid4().hex


def seed():
    print(f"Conectando a {DB_NAME}...")
    client = MongoClient(CONN, serverSelectionTimeoutMS=10000)
    db = client[DB_NAME]
    now = datetime.now(timezone.utc)

    # 1. Universidad UTTT
    col_univ = db["universidades"]
    uttt_norm = normalize("Universidad Tecnológica de Tula-Tepeji")
    existing_uttt = col_univ.find_one({"nombreNormalizado": uttt_norm})

    if existing_uttt:
        uttt_id = existing_uttt["_id"]
        col_univ.update_one(
            {"_id": uttt_id},
            {"$set": {
                "activa": True,
                "location": {
                    "type": "Point",
                    "coordinates": [UTTT_LNG, UTTT_LAT]
                }
            }}
        )
        print(f"✓ UTTT actualizada en {DB_NAME}: ID={uttt_id}")
    else:
        uttt_id = new_id()
        col_univ.insert_one({
            "_id": uttt_id,
            "nombreYCampus": "Universidad Tecnológica de Tula-Tepeji",
            "nombreNormalizado": uttt_norm,
            "activa": True,
            "createdAt": now,
            "location": {
                "type": "Point",
                "coordinates": [UTTT_LNG, UTTT_LAT]
            }
        })
        print(f"✓ UTTT creada en {DB_NAME}: ID={uttt_id}")

    # 2. Dueño de las propiedades (la cuenta del usuario: unihome0412@gmail.com)
    owner = db["cuentas"].find_one({"correoNormalizado": "unihome0412@gmail.com"})
    if not owner:
        raise RuntimeError("No se encontró la cuenta unihome0412@gmail.com en unihome_verificacion")
    owner_id = owner["_id"]
    print(f"✓ Dueño asignado: {owner.get('nombreCompleto')} ({owner.get('correoNormalizado')}) - ID={owner_id}")

    # Términos aceptados
    db["aceptaciones_terminos"].update_one(
        {"accountId": owner_id, "versionTerminos": "1"},
        {"$setOnInsert": {
            "_id": new_id(),
            "accountId": owner_id,
            "versionTerminos": "1",
            "acceptedAt": now
        }},
        upsert=True
    )
    print("✓ Términos de publicación registrados")

    # 3. Propiedades y Unidades
    col_prop = db["propiedades"]
    col_unit = db["unidades"]

    propiedades_data = [
        {
            "nombre": "Residencial Los Arcos",
            "direccion": "Calle Los Fresnos 120",
            "colonia": "El Carmen",
            "tipoPropiedad": "Casa",
            "piso": "1",
            "numeroUnidad": "A",
            "lat": 20.0150,
            "lng": -99.3380,
            "unidades": [
                {
                    "tipoUnidad": "Habitacion",
                    "ocupacion": "Individual",
                    "lugares": 1,
                    "precio": Decimal128("2200.00"),
                    "tamano": Decimal128("16.00"),
                    "paraQuien": "Mixto",
                    "bano": "Privado",
                    "mobiliario": "Amueblado",
                    "muebles": ["Cama individual", "Closet amplio", "Escritorio para estudio", "Silla ergonómica"],
                    "plantillaBase": "HabitacionIndividual",
                    "zona": "A solo 5 minutos a pie de la entrada principal de la UTTT. Zona con tiendas, papelerías y transporte.",
                    "reglas": "No fumar dentro de la habitación. Horario de visitas hasta las 21:00 hrs. Cuidado de áreas compartidas.",
                    "servicios": [
                        {"nombre": "Internet", "modalidad": "Incluido", "montoMensualMxn": None},
                        {"nombre": "Agua", "modalidad": "Incluido", "montoMensualMxn": None},
                        {"nombre": "Luz", "modalidad": "Incluido", "montoMensualMxn": None},
                        {"nombre": "LimpiezaAreasComunes", "modalidad": "Incluido", "montoMensualMxn": None}
                    ]
                }
            ]
        },
        {
            "nombre": "Departamentos Universitaria",
            "direccion": "Av. Universidad 45",
            "colonia": "Jalpa",
            "tipoPropiedad": "Edificio",
            "piso": "2",
            "numeroUnidad": "203",
            "lat": 20.0115,
            "lng": -99.3420,
            "unidades": [
                {
                    "tipoUnidad": "Departamento",
                    "ocupacion": "Compartida",
                    "lugares": 2,
                    "precio": Decimal128("4500.00"),
                    "tamano": Decimal128("45.00"),
                    "paraQuien": "Mixto",
                    "bano": "Privado",
                    "mobiliario": "Amueblado",
                    "muebles": ["2 Camas individuales", "Cocina integral", "Refrigerador", "Comedor 4 sillas", "Closets"],
                    "plantillaBase": "DepartamentoCompleto",
                    "zona": "Excelente ubicación cerca de tiendas Oxxo, paradas de combis y farmacias a 10 min de la UTTT.",
                    "reglas": "Respeto mutuo entre compañeros de departamento. No fiestas entre semana. Mantener limpia la cocina.",
                    "servicios": [
                        {"nombre": "Internet", "modalidad": "Incluido", "montoMensualMxn": None},
                        {"nombre": "Agua", "modalidad": "Incluido", "montoMensualMxn": None},
                        {"nombre": "Gas", "modalidad": "Incluido", "montoMensualMxn": None},
                        {"nombre": "Estacionamiento", "modalidad": "Incluido", "montoMensualMxn": None}
                    ]
                }
            ]
        },
        {
            "nombre": "Estudios San Marcos",
            "direccion": "Privada San Marcos 8",
            "colonia": "San Marcos",
            "tipoPropiedad": "Departamento",
            "piso": "1",
            "numeroUnidad": "1",
            "lat": 20.0170,
            "lng": -99.3410,
            "unidades": [
                {
                    "tipoUnidad": "Estudio",
                    "ocupacion": "Individual",
                    "lugares": 1,
                    "precio": Decimal128("3200.00"),
                    "tamano": Decimal128("28.00"),
                    "paraQuien": "Mixto",
                    "bano": "Privado",
                    "mobiliario": "Amueblado",
                    "muebles": ["Cama matrimonial", "Barra desayunadora con bancos", "Frigobar", "Microondas", "Escritorio"],
                    "plantillaBase": "Estudio",
                    "zona": "Barrio tranquilo y seguro, ideal para concentración y estudio. A 12 minutos a pie de la UTTT.",
                    "reglas": "Espacio 100% libre de humo. Ambiente silencioso y de estudio. No mascotas.",
                    "servicios": [
                        {"nombre": "Internet", "modalidad": "Incluido", "montoMensualMxn": None},
                        {"nombre": "Agua", "modalidad": "Incluido", "montoMensualMxn": None},
                        {"nombre": "Luz", "modalidad": "Incluido", "montoMensualMxn": None}
                    ]
                }
            ]
        }
    ]

    for prop in propiedades_data:
        nombre_norm = normalize(prop["nombre"])
        dir_norm = normalize(prop["direccion"])
        col_norm = normalize(prop["colonia"])

        existing_prop = col_prop.find_one({
            "ownerAccountId": owner_id,
            "direccionNormalizada": dir_norm,
            "coloniaNormalizada": col_norm,
            "nombreNormalizado": nombre_norm
        })

        if existing_prop:
            prop_id = existing_prop["_id"]
            print(f"✓ Propiedad ya existe: {prop['nombre']} (ID: {prop_id})")
        else:
            prop_id = new_id()
            doc_prop = {
                "_id": prop_id,
                "ownerAccountId": owner_id,
                "nombre": prop["nombre"],
                "nombreNormalizado": nombre_norm,
                "direccion": prop["direccion"],
                "direccionNormalizada": dir_norm,
                "colonia": prop["colonia"],
                "coloniaNormalizada": col_norm,
                "tipoPropiedad": prop["tipoPropiedad"],
                "universidadId": uttt_id,
                "location": {
                    "type": "Point",
                    "coordinates": [prop["lng"], prop["lat"]]
                },
                "piso": prop["piso"],
                "numeroUnidad": prop["numeroUnidad"],
                "estadoPublicacion": "Aprobada",
                "firstApprovalAt": now,
                "createdAt": now
            }
            col_prop.insert_one(doc_prop)
            print(f"✓ Propiedad creada: {prop['nombre']} (ID: {prop_id})")

        for u in prop["unidades"]:
            existing_unit = col_unit.find_one({
                "propertyId": prop_id,
                "tipoUnidad": u["tipoUnidad"],
                "ocupacion": u["ocupacion"]
            })

            if existing_unit:
                unit_id = existing_unit["_id"]
                col_unit.update_one(
                    {"_id": unit_id},
                    {"$set": {
                        "estadoPublicacion": "Aprobada",
                        "disponibilidad": "Disponible",
                        "universidadId": uttt_id
                    }}
                )
                print(f"  ✓ Unidad actualizada: {u['tipoUnidad']} {u['ocupacion']} (ID: {unit_id})")
            else:
                unit_id = new_id()
                doc_unit = {
                    "_id": unit_id,
                    "propertyId": prop_id,
                    "ownerAccountId": owner_id,
                    "universidadId": uttt_id,
                    "tipoUnidad": u["tipoUnidad"],
                    "ocupacion": u["ocupacion"],
                    "lugares": u["lugares"],
                    "precioMensualPorLugarMxn": u["precio"],
                    "tamanoM2": u["tamano"],
                    "paraQuien": u["paraQuien"],
                    "bano": u["bano"],
                    "mobiliario": u["mobiliario"],
                    "muebles": u["muebles"],
                    "plantillaBase": u["plantillaBase"],
                    "informacionZona": u["zona"],
                    "reglasConvivencia": u["reglas"],
                    "servicios": u["servicios"],
                    "fotos": [],
                    "estadoPublicacion": "Aprobada",
                    "disponibilidad": "Disponible",
                    "createdAt": now
                }
                col_unit.insert_one(doc_unit)
                print(f"  ✓ Unidad creada: {u['tipoUnidad']} {u['ocupacion']} (ID: {unit_id}) - ${u['precio']} MXN")

    print(f"\n¡Éxito! Base de datos '{DB_NAME}' poblada correctamente.")

if __name__ == "__main__":
    seed()
