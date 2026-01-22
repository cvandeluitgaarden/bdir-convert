#!/usr/bin/env bash
set -euo pipefail

NO_COLOR=0
REGEN=0

for arg in "$@"; do
  case "$arg" in
    --no-color)
      NO_COLOR=1
      ;;
    --regen)
      REGEN=1
      ;;
    *)
      ;;
  esac
done

if [[ "$NO_COLOR" -eq 1 ]]; then
  export BDIR_NO_COLOR=1
fi

if [[ "$REGEN" -eq 1 ]]; then
  echo "[test] running golden regen tests"
  dotnet test --filter "Category=Regen"
else
  echo "[test] running tests (excluding regen)"
  dotnet test --filter "Category!=Regen"
fi
