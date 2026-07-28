from pathlib import Path
import sys

from PIL import Image


def main() -> None:
    if len(sys.argv) != 3:
        raise SystemExit("usage: build_app_icon.py <source.png> <output-base>")

    source = Path(sys.argv[1])
    output_base = Path(sys.argv[2])
    output_base.parent.mkdir(parents=True, exist_ok=True)

    png_path = output_base.with_suffix(".png")
    ico_path = output_base.with_suffix(".ico")
    png_path.write_bytes(source.read_bytes())

    image = Image.open(png_path).convert("RGBA")
    image.save(
        ico_path,
        format="ICO",
        sizes=[
            (16, 16),
            (20, 20),
            (24, 24),
            (32, 32),
            (40, 40),
            (48, 48),
            (64, 64),
            (96, 96),
            (128, 128),
            (256, 256),
        ],
    )


if __name__ == "__main__":
    main()
