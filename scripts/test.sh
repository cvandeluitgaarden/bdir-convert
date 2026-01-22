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

DOTNET_TEST_ARGS=(--logger "console;verbosity=normal")

if [[ "${CI:-}" == "true" ]]; then
  DOTNET_TEST_ARGS=(--logger "console;verbosity=detailed")
fi

if [[ "$REGEN" -eq 1 ]]; then
  echo "[test] running golden regen tests"
  dotnet test --filter "Category=Regen" "${DOTNET_TEST_ARGS[@]}"
else
  echo "[test] running tests (excluding regen)"
  dotnet test --filter "Category!=Regen" "${DOTNET_TEST_ARGS[@]}"
fi