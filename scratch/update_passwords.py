import base64
import hashlib
import os
import struct
from pymongo import MongoClient

CONN = (
    "mongodb+srv://unihome0412_db_user:0d18wEzlRHuDkPor"
    "@unihome.gelwqtb.mongodb.net/"
    "?retryWrites=true&w=majority"
)
DB_NAME = "unihome_desarrollo"

def make_aspnet_hash(password: str) -> str:
    salt = os.urandom(16)
    iterations = 100_000
    subkey = hashlib.pbkdf2_hmac("sha512", password.encode("utf-8"), salt, iterations, dklen=32)
    payload = bytearray([0x01])
    payload.extend(struct.pack(">I", 2))           # KeyDerivationPrf.HMACSHA512
    payload.extend(struct.pack(">I", iterations))  # 100,000
    payload.extend(struct.pack(">I", 16))          # salt length
    payload.extend(salt)
    payload.extend(subkey)
    return base64.b64encode(payload).decode("ascii")

def main():
    client = MongoClient(CONN, serverSelectionTimeoutMS=10000)
    db = client[DB_NAME]
    for email in ["arrendador.tula@unihome.test", "estudiante.uttt@unihome.test", "admin.unihome@unihome.test"]:
        h = make_aspnet_hash("Password123!")
        res = db["cuentas"].update_one({"correoNormalizado": email}, {"$set": {"hashContrasena": h}})
        print(f"Actualizado {email}: modificado={res.modified_count}")

if __name__ == "__main__":
    main()
