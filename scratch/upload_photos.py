"""
Genera y sube fotografías de muestra a las 3 unidades de unihome_verificacion
a través de la API oficial en Azure Container Apps para que Cloudinary
las procese y el frontend muestre portadas reales.
"""
import io
import json
import urllib.request
from PIL import Image, ImageDraw

API_BASE = "https://unihome-api.agreeablesky-4cd63e24.eastus.azurecontainerapps.io"
EMAIL = "unihome0412@gmail.com"
PASSWORD = "uniHome0412"

UNIDADES = [
    ("8f48b5c1-dae3-47bf-b8ea-c382a79433d6", "Habitación Individual Los Arcos", (52, 152, 219)),
    ("9ca20531-04e2-4512-956e-37bc010f521e", "Departamento Universitaria", (46, 204, 113)),
    ("85881282-22c8-4ab5-843e-7c9205f1867e", "Estudio San Marcos", (155, 89, 182))
]

def get_token():
    url = f"{API_BASE}/api/v1/auth/login"
    body = json.dumps({"correo": EMAIL, "contrasena": PASSWORD}).encode("utf-8")
    req = urllib.request.Request(url, data=body, headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req) as resp:
        return json.loads(resp.read().decode())["accessToken"]

def make_sample_image(label, bg_color):
    img = Image.new("RGB", (800, 600), color=bg_color)
    draw = ImageDraw.Draw(img)
    # Dibujar elementos decorativos
    draw.rectangle([40, 40, 760, 560], outline=(255, 255, 255), width=4)
    draw.text((80, 280), label, fill=(255, 255, 255))
    buf = io.BytesIO()
    img.save(buf, format="JPEG", quality=90)
    return buf.getvalue()

def upload_photo(token, unit_id, image_bytes, filename="foto.jpg"):
    url = f"{API_BASE}/api/v1/unidades/{unit_id}/fotos"
    boundary = "----WebKitFormBoundary7MA4YWxkTrZu0gW"
    
    body = bytearray()
    body.extend(f"--{boundary}\r\n".encode("utf-8"))
    body.extend(f'Content-Disposition: form-data; name="fotos"; filename="{filename}"\r\n'.encode("utf-8"))
    body.extend(b"Content-Type: image/jpeg\r\n\r\n")
    body.extend(image_bytes)
    body.extend(f"\r\n--{boundary}--\r\n".encode("utf-8"))

    req = urllib.request.Request(
        url,
        data=bytes(body),
        headers={
            "Authorization": f"Bearer {token}",
            "Content-Type": f"multipart/form-data; boundary={boundary}"
        }
    )
    try:
        with urllib.request.urlopen(req) as resp:
            print(f"✓ Foto subida para {unit_id}: HTTP {resp.status}")
    except urllib.error.HTTPError as e:
        print(f"✗ Error subiendo foto a {unit_id}: {e.code} - {e.read().decode()}")

def main():
    print("Iniciando sesión en la API...")
    token = get_token()
    print("✓ Sesión iniciada con éxito")

    for unit_id, label, color in UNIDADES:
        print(f"Subiendo foto para: {label} ({unit_id})...")
        img_bytes = make_sample_image(label, color)
        upload_photo(token, unit_id, img_bytes, f"{label.replace(' ', '_').lower()}.jpg")

if __name__ == "__main__":
    main()
