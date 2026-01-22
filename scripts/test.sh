#!/usr/bin/env bash
set -euo pipefail

if [[ "${1:-}" == "--regen" ]]; then
  dotnet test --filter "Category=Regen"
else
  dotnet test --filter "Category!=Regen"
fi
