"""生成无私人素材的本地 AVI 样片，仅用于 Windows 真实播放器验证。需要 Pillow。"""

import io
import struct
import sys
from pathlib import Path

from PIL import Image, ImageDraw


def chunk(tag, data):
    return tag + struct.pack("<I", len(data)) + data + (b"\0" if len(data) % 2 else b"")


def generate(path):
    width, height, fps, seconds = 320, 180, 10, 60
    frames = []
    for index in range(fps * seconds):
        frame = Image.new("RGB", (width, height), (24, 40, 65))
        draw = ImageDraw.Draw(frame)
        draw.text((20, 25), "PausePal local playback test", fill="white")
        draw.text((20, 65), f"{index / fps:05.1f} / {seconds} seconds", fill="white")
        draw.rectangle((20, 110, 20 + int(280 * index / (fps * seconds)), 130), fill=(30, 190, 110))
        buffer = io.BytesIO()
        frame.save(buffer, format="JPEG", quality=80)
        frames.append(buffer.getvalue())
    largest = max(map(len, frames))
    main_header = struct.pack("<14I", 1_000_000 // fps, largest * fps, 0, 0x10, len(frames), 0, 1,
                              largest, width, height, 0, 0, 0, 0)
    stream_header = struct.pack("<4s4sIHHIIIIIIIIhhhh", b"vids", b"MJPG", 0, 0, 0, 0, 1, fps, 0,
                                len(frames), largest, 0xFFFFFFFF, 0, 0, 0, width, height)
    bitmap_header = struct.pack("<IiiHH4sIiiII", 40, width, height, 1, 24, b"MJPG", width * height * 3,
                                0, 0, 0, 0)
    headers = chunk(b"LIST", b"hdrl" + chunk(b"avih", main_header) +
                    chunk(b"LIST", b"strl" + chunk(b"strh", stream_header) + chunk(b"strf", bitmap_header)))
    movie, entries, offset = [], [], 4
    for frame in frames:
        encoded = chunk(b"00dc", frame)
        movie.append(encoded)
        entries.append(struct.pack("<4sIII", b"00dc", 0x10, offset, len(frame)))
        offset += len(encoded)
    data = b"AVI " + headers + chunk(b"LIST", b"movi" + b"".join(movie)) + chunk(b"idx1", b"".join(entries))
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists():
        raise FileExistsError("样片已存在，拒绝覆盖")
    path.write_bytes(b"RIFF" + struct.pack("<I", len(data)) + data)
    print(f"已生成 {len(frames)} 帧、{seconds} 秒本地样片")


if __name__ == "__main__":
    generate(Path(sys.argv[1]))
