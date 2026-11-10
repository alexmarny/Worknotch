"""Dibuja el icono de Worknotch (build/icono/icono.ico y icono.png).

Un cuadrado negro de esquinas redondas, como el panel, con una rejilla de puntos: apagados salvo
una marca de «hecho» encendida en ámbar. Colores de docs/design-system/tokens.json.
  python3 build/make_ico.py
"""
import json
from pathlib import Path
from PIL import Image, ImageDraw

RAIZ = Path(__file__).resolve().parent.parent
T = json.loads((RAIZ / "docs/design-system/tokens.json").read_text(encoding="utf-8"))["colores"]
FONDO, LED, APAGADO = T["wn-pantalla"]["noche"], T["wn-led"]["noche"], T["wn-led-apagado"]["noche"]

# Una marca de verificación en una rejilla de 7 × 7
MARCA = [
    ".......",
    "......#",
    ".....#.",
    "#...#..",
    ".#.#...",
    "..#....",
    ".......",
]


def dibuja(lado: int) -> Image.Image:
    escala = 4  # se dibuja grande y se reduce, para suavizar los bordes
    n = lado * escala
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, 0, n - 1, n - 1], radius=int(n * 0.22), fill=FONDO)
    margen = n * 0.17
    paso = (n - 2 * margen) / 7
    radio = paso * (0.36 if lado >= 32 else 0.46)
    for f, fila in enumerate(MARCA):
        for c, celda in enumerate(fila):
            if lado < 32 and celda != "#":
                continue  # en pequeño, solo la marca
            x = margen + paso * (c + 0.5)
            y = margen + paso * (f + 0.5)
            d.ellipse([x - radio, y - radio, x + radio, y + radio], fill=LED if celda == "#" else APAGADO)
    return img.resize((lado, lado), Image.LANCZOS)


if __name__ == "__main__":
    destino = RAIZ / "build/icono"
    destino.mkdir(parents=True, exist_ok=True)
    grande = dibuja(256)
    grande.save(destino / "icono.png")
    grande.save(destino / "icono.ico", sizes=[(s, s) for s in (16, 20, 24, 32, 48, 64, 128, 256)],
                append_images=[dibuja(s) for s in (16, 20, 24, 32, 48, 64, 128)])
    print("Listo: build/icono/icono.ico y icono.png")
