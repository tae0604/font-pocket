"""Export public preview bytes for review clients without artifact download access.

Only the isolated preview branch runs this step. No environment, user files or
credentials are included. Hashes allow the receiving client to verify every file.
"""
import base64
import hashlib
from pathlib import Path

root = Path(__file__).resolve().parent / "dist"
paths = [root / "FontPocket-macOS-0.2.1-universal.zip"]
paths += [root / "screenshots" / name for name in (
    "manager-light.png", "manager-dark.png", "manager-accessible.png", "install-ko.png", "characters-ko.png", "collections-ko.png", "characters-dark-ko.png", "collections-dark-ko.png"
)]
for path in paths:
    data = path.read_bytes()
    encoded = base64.b64encode(data).decode("ascii")
    print("FP_EXPORT_BEGIN:" + path.name + ":" + hashlib.sha256(data).hexdigest())
    for offset in range(0, len(encoded), 16000):
        print("FP_EXPORT_DATA:" + encoded[offset:offset + 16000])
    print("FP_EXPORT_END:" + path.name)
