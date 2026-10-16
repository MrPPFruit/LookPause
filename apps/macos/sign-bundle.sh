#!/bin/bash
set -euo pipefail
bundle="$1"
identifier="$2"
signer="${LOOKPAUSE_SIGN_IDENTITY:-}"
if [ -z "$signer" ]; then
  signer="$(python3 - <<'PY'
import re, subprocess
text=subprocess.check_output(['security','find-identity','-v','-p','codesigning'],text=True)
matches=[m.group(1) for line in text.splitlines() if 'Apple Development:' in line and (m:=re.search(r'\b([0-9A-F]{40})\b',line))]
print(matches[0] if len(matches)==1 else '-')
PY
)"
fi
codesign --force --sign "$signer" --timestamp=none --identifier "$identifier" "$bundle"
codesign --verify --strict "$bundle"
