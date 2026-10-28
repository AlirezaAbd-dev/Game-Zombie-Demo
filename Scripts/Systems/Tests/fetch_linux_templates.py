#!/usr/bin/env python3
"""Fetch only Linux .NET templates from the official archive into an isolated build profile."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import re
import urllib.request
import xml.etree.ElementTree as ET
import zipfile
import zlib

project = Path(__file__).resolve().parents[3]
version = ET.parse(project / "Top Down Demo.csproj").getroot().attrib["Sdk"].rsplit("/", 1)[-1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--profile", type=Path, default=project / "builds/export-profile")
args = parser.parse_args()
if not re.fullmatch(r"\d+\.\d+\.\d+", version):
    parser.error("this helper requires a stable Godot SDK version")
url = f"https://godot-releases.nbg1.your-objectstorage.com/{version}-stable/Godot_v{version}-stable_mono_export_templates.tpz"

class RemoteArchive(io.RawIOBase):
    def __init__(self):
        self.position = 0
        with urllib.request.urlopen(urllib.request.Request(url, method="HEAD"), timeout=30) as response:
            self.length = int(response.headers["Content-Length"])
    def readable(self): return True
    def seekable(self): return True
    def tell(self): return self.position
    def seek(self, offset, whence=0):
        self.position = offset if whence == 0 else self.position + offset if whence == 1 else self.length + offset
        if self.position < 0: raise ValueError("negative archive offset")
        return self.position
    def read(self, size=-1):
        size = min(self.length - self.position, size if size >= 0 else self.length - self.position)
        if size <= 0: return b""
        if size > 64 * 1024 * 1024: raise ValueError("unexpectedly large archive range")
        end = self.position + size - 1
        request = urllib.request.Request(url, headers={"Range": f"bytes={self.position}-{end}"})
        with urllib.request.urlopen(request, timeout=90) as response:
            if response.status != 206 or not response.headers.get("Content-Range", "").startswith(f"bytes {self.position}-"):
                raise ValueError("server did not honor archive byte range")
            data = response.read(size)
        if len(data) != size: raise ValueError("incomplete archive range")
        self.position += size
        return data

profile = args.profile.resolve()
with zipfile.ZipFile(RemoteArchive()) as archive:
    template_version = archive.read("templates/version.txt").decode().strip()
    if template_version != version + ".stable.mono": raise ValueError("archive version mismatch")
    destination = profile / "data/godot/export_templates" / template_version
    destination.mkdir(parents=True, exist_ok=True)
    manifest = {"archive": url, "version": template_version, "files": {}}
    for name in ("version.txt", "linux_release.x86_64", "linux_debug.x86_64"):
        entry = archive.getinfo("templates/" + name)
        if entry.file_size > 256 * 1024 * 1024: raise ValueError("unexpected template size")
        target = destination / name
        data = target.read_bytes() if target.is_file() and target.stat().st_size == entry.file_size else b""
        if len(data) != entry.file_size or zlib.crc32(data) != entry.CRC:
            print("Downloading", name, entry.compress_size, "compressed bytes", flush=True)
            data = archive.read(entry)  # ZipFile also verifies CRC for the transferred bytes.
            temporary = target.with_name(target.name + ".part")
            temporary.write_bytes(data)
            temporary.replace(target)
        if name.startswith("linux_"): target.chmod(0o755)
        manifest["files"][name] = {"bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()}
        print("Verified", target, flush=True)
    (destination / "download-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
print("Use XDG_DATA_HOME=" + str(profile / "data") + " when exporting.")
