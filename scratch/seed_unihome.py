"""
Script para poblar la base de datos 'unihome_desarrollo' en MongoDB Atlas
con datos realistas para la Universidad Tecnológica de Tula-Tepeji (UTTT).

Incluye:
  1. Actualización de coordenadas GeoJSON para la UTTT (Tula de Allende, Hidalgo).
  2. Cuentas de prueba con hash válido de ASP.NET Core Identity (PBKDF2-HMAC-SHA256):
     - Arrendador:  arrendador.tula@unihome.test  / Password123!
     - Estudiante:  estudiante.uttt@unihome.test  / Password123!
     - Admin:       admin.unihome@unihome.test    / Password123!
  3. Aceptación de términos para el arrendador.
  4. 3 Propiedades cercanas a la UTTT:
     - Residencial Los Arcos (Casa - El Carmen)
     - Departamentos Universitaria (Edificio - Jalpa)
     - Estudios San Marcos (Estudio - San Marcos)
  5. Unidades / habitaciones en renta:
     - Habitación individual amueblada ($2,200 MXN/mes)
     - Departamento compartido 2 lugares ($4,500 MXN/mes)
     - Estudio independiente amueblado ($3,200 MXN/mes)
     Todas en estado 'Aprobada' y 'Disponible' para que aparezcan en el mapa y lista pública.
"""
import base64
import hashlib
import os
import struct
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
DB_NAME = "unihome_desarrollo"

# Coordenadas reales de la UTTT (Tula de Allende, Hidalgo):
UTTT_LNG = -99.3400
UTTT_LAT = 20.0135


def normalize(value: str) -> str:
    """Réplica exacta de UniHome.Domain.Common.TextNormalization.Normalize."""
    trimmed = value.strip()
    decomposed = unicodedata.normalize("NFD", trimmed)
    chars = [ch.lower() for ch in decomposed if unicodedata.category(ch) != "Mn"]
    return " ".join("".join(chars).split())


def new_id() -> str:
    """Guid en formato 'N' (32 caracteres hexadecimales en minúsculas)."""
    return uuid.uuid4().hex


def hash_password_aspnet_core(password: str) -> str:
    """
    Genera un hash compatible al 100% con ASP.NET Core Identity PasswordHasher (Format V3):
      - Header: 0x01 (Version 3)
      - PRF: KeyDerivationPrf.HMACSHA256 = 1 (uint32 big-endian)
      - Iteraciones: 100,000 (uint32 big-endian)
      - Longitud de sal: 16 bytes (uint32 big-endian)
      - Sal: 16 bytes aleatorios
      - Subclave: 32 bytes derivados de PBKDF2-HMAC-SHA256
    """
    salt = os.urandom(16)
    iterations = 100_000
    subkey = hashlib.pbkdf2_hmac("sha256", password.encode("utf-8"), salt, iterations, dklen=32)
    payload = bytearray()
    payload.append(0x01)
    payload.extend(struct.pack(">I", 1))            # HMACSHA256
    payload.extend(struct.pack(">I", iterations))   # 100,000
    payload.extend(struct.pack(">I", 16))           # salt length
    payload.extend(salt)
    payload.extend(subkey)
    return base64.b64encode(payload).decode("ascii")


def seed():
    print(f"Conectando a MongoDB: {DB_NAME}...")
    client = MongoClient(CONN, serverSelectionTimeoutMS=10_000)
    db = client[DB_NAME]
    now = datetime.now(timezone.utc)

    # ─────────────────────────────────────────────────────────────
    # 1. Universidad: UTTT
    # ─────────────────────────────────────────────────────────────
    col_univ = db["universidades"]
    uttt_norm = normalize("Universidad Tecnológica de Tula-Tepeji")
    uttt = col_univ.find_one({"nombreNormalizado": uttt_norm})

    if not uttt:
        uttt_id = new_id()
        uttt_doc = {
            "_id": uttt_id,
            "nombreYCampus": "Universidad Tecnológica de Tula-Tepeji",
            "nombreNormalizado": uttt_norm,
            "activa": True,
            "createdAt": now,
            "location": {
                "type": "Point",
                "coordinates": [UTTT_LNG, UTTT_LAT]
            }
        }
        col_univ.insert_one(uttt_doc)
        print(f"✓ UTTT creada con ID: {uttt_id}")
    else:
        uttt_id = uttt["_id"]
        # Asegurar coordenadas para cálculo de distancia y mapa
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
        print(f"✓ UTTT actualizada con coordenadas en ID: {uttt_id}")

    # ─────────────────────────────────────────────────────────────
    # 2. Cuentas de prueba (Arrendador, Estudiante, Admin)
    # ─────────────────────────────────────────────────────────────
    col_cuentas = db["cuentas"]
    pwd_hash = hash_password_aspnet_core("Password123!")

    cuentas_data = [
        {
            "nombreCompleto": "Carlos Mendoza Arrendador",
            "correo": "arrendador.tula@unihome.test",
            "fechaNacimiento": "1985-06-15",
            "genero": "Masculino",
        },
        {
            "nombreCompleto": "Ana Sofia Estudiante",
            "correo": "estudiante.uttt@unihome.test",
            "fechaNacimiento": "2003-09-21",
            "genero": "Femenino",
        },
        {
            "nombreCompleto": "Administrador UniHome",
            "correo": "admin.unihome@unihome.test",
            "fechaNacimiento": "1990-01-01",
            "genero": "Otro",
        }
    ]

    arrendador_id = None
    for item in cuentas_data:
        norm_email = item["correo"].strip().lower()
        existing = col_cuentas.find_one({"correoNormalizado": norm_email})
        if existing:
            account_id = existing["_id"]
            print(f"✓ Cuenta ya existe: {item['correo']} (ID: {account_id})")
        else:
            account_id = new_id()
            doc_cuenta = {
                "_id": account_id,
                "nombreCompleto": item["nombreCompleto"],
                "correoNormalizado": norm_email,
                "fechaNacimiento": item["fechaNacimiento"],
                "genero": item["genero"],
                "hashContrasena": pwd_hash,
                "aceptacionPrivacidad": {
                    "version": "1.0",
                    "aceptadaEnUtc": now
                },
                "estado": "Active",
                "correoConfirmado": True,
                "creadoEnUtc": now,
                "correoConfirmadoEnUtc": now,
                "verificacionCorreo": None,
                "intentosReenvioUtc": [],
                "eliminadaEnUtc": None
            }
            col_cuentas.insert_one(doc_cuenta)
            print(f"✓ Cuenta creada: {item['correo']} (ID: {account_id})")

        if item["correo"] == "arrendador.tula@unihome.test":
            arrendador_id = account_id

    # Términos de publicación para el arrendador
    col_terminos = db["aceptaciones_terminos"]
    col_terminos.update_one(
        {"accountId": arrendador_id, "versionTerminos": "1.0"},
        {"$setOnInsert": {
            "_id": new_id(),
            "accountId": arrendador_id,
            "versionTerminos": "1.0",
            "acceptedAt": now
        }},
        upsert=True
    )

    # ─────────────────────────────────────────────────────────────
    # 3. Propiedades y Unidades (cerca de la UTTT)
    # ─────────────────────────────────────────────────────────────
    col_propiedades = db["propiedades"]
    col_unidades = db["unidades"]

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

        existing_prop = col_propiedades.find_one({
            "ownerAccountId": arrendador_id,
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
                "ownerAccountId": arrendador_id,
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
            col_propiedades.insert_one(doc_prop)
            print(f"✓ Propiedad creada: {prop['nombre']} (ID: {prop_id})")

        # Crear unidades asociadas
        for u in prop["unidades"]:
            existing_unit = col_unidades.find_one({
                "propertyId": prop_id,
                "tipoUnidad": u["tipoUnidad"],
                "ocupacion": u["ocupacion"]
            })

            if existing_unit:
                # Asegurar estado Aprobada y Disponible
                col_unidades.update_one(
                    {"_id": existing_unit["_id"]},
                    {"$set": {
                        "estadoPublicacion": "Aprobada",
                        "disponibilidad": "Disponible",
                        "universidadId": uttt_id
                    }}
                )
                print(f"  ✓ Unidad actualizada: {u['tipoUnidad']} {u['ocupacion']} (ID: {existing_unit['_id']})")
            else:
                unit_id = new_id()
                doc_unit = {
                    "_id": unit_id,
                    "propertyId": prop_id,
                    "ownerAccountId": arrendador_id,
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
                col_unidades.insert_one(doc_unit)
                print(f"  ✓ Unidad creada: {u['tipoUnidad']} {u['ocupacion']} (ID: {unit_id}) - ${u['precio']} MXN")

    print("\n" + "="*60)
    print("¡Poblado completado con éxito!")
    print(f"Base de datos: {DB_NAME}")
    print(f"Cuentas generadas (Contraseña para todas: Password123!):")
    print("  - Arrendador:  arrendador.tula@unihome.test")
    print("  - Estudiante:  estudiante.uttt@unihome.test")
    print("  - Admin:       admin.unihome@unihome.test")
    print("="*60)


if __name__ == "__main__":
    seed()
