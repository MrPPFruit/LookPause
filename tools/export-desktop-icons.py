"""将原生绘图回归导出的PNG打包为Windows多尺寸ICO；只处理项目生成素材。"""
from pathlib import Path
import struct
import sys

source, destination = map(Path, sys.argv[1:3])
sizes = [16, 20, 24, 32, 40, 48, 64]
for row, theme in enumerate(["light", "dark"]):
    folder = destination / theme
    folder.mkdir(parents=True, exist_ok=True)
    for state in ["disconnected", "waiting", "attending", "away"]:
        payloads = [(source / f"{row}-{state}-{size}.png").read_bytes() for size in sizes]
        offset = 6 + 16 * len(sizes)
        entries = []
        for size, png in zip(sizes, payloads):
            assert png[:8] == b"\x89PNG\r\n\x1a\n"
            assert struct.unpack(">II", png[16:24]) == (size, size)
            entries.append(struct.pack("<BBBBHHII", size, size, 0, 0, 1, 32, len(png), offset))
            offset += len(png)
        (folder / f"{state}.ico").write_bytes(struct.pack("<HHH", 0, 1, len(sizes)) + b"".join(entries + payloads))
