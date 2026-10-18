#!/bin/bash
set -euo pipefail
exec python3 "$(dirname "$0")/validate_ipa.py" "$@"
