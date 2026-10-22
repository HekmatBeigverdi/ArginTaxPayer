#!/usr/bin/env python3
"""Opt in to encrypted PostgreSQL signing custody for LOCAL TEST only.
No database access, migration, API startup, or key rotation is performed.
"""
from __future__ import annotations
import base64
import os
from pathlib import Path
import re
import secrets
import tempfile

root = Path(__file__).resolve().parent.parent
file = root / ".env.dev.local"
if not file.is_file():
    raise SystemExit("Missing .env.dev.local; configure the test database first.")
original = file.read_text(encoding="utf-8")
pattern = re.compile(r"^SigningKeyProtection__DatabaseMasterKey=(.*)$", re.MULTILINE)
found = pattern.search(original)
if found:
    existing = found.group(1).strip().strip("'").strip('"')
    try:
        valid = len(base64.b64decode(existing, validate=True)) == 32
    except (ValueError, base64.binascii.Error):
        valid = False
    if not valid:
        raise SystemExit("Existing DatabaseMasterKey is invalid. Refusing to rotate or replace it.")
    master = existing
else:
    master = base64.b64encode(secrets.token_bytes(32)).decode("ascii")

values = {
    "SigningKeyProtection__Provider": "Database",
    "SigningKeyProtection__Enabled": "true",
    "SigningKeyImport__Enabled": "true",
    "SigningKeyGeneration__Enabled": "true",
    "SigningConnectionTest__Enabled": "true",
    "SigningKeyProtection__DatabaseMasterKey": "'" + master + "'",
}
updated = original
for name, value in values.items():
    expr = re.compile(r"^" + re.escape(name) + r"=.*$", re.MULTILINE)
    if expr.search(updated):
        updated = expr.sub(lambda _: name + "=" + value, updated)
    else:
        updated += ("\n" if not updated.endswith("\n") else "") + name + "=" + value + "\n"

temp_path = None
try:
    fd, temp_path = tempfile.mkstemp(prefix=".env.dev.local.", dir=root)
    with os.fdopen(fd, "w", encoding="utf-8") as out:
        out.write(updated)
        out.flush()
        os.fsync(out.fileno())
    os.chmod(temp_path, 0o600)
    os.replace(temp_path, file)
finally:
    if temp_path and Path(temp_path).exists():
        Path(temp_path).unlink()
print("Encrypted PostgreSQL key custody configured in private .env.dev.local.")
print("A persistent master key was retained or newly generated (never printed).")
print("IMPORTANT: preserve a separate offline backup of this private master key.")
print("NO database migration has been applied. Do not paste .env.dev.local in chat.")
