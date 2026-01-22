#!/usr/bin/env bash
set -euo pipefail

# Clean repository and create a zip archive containing only tracked files.

if ! command -v git >/dev/null 2>&1; then
  echo "git is not available in PATH" >&2
  exit 1
fi

repo_root="$(git rev-parse --show-toplevel 2>/dev/null || true)"
if [[ -z "$repo_root" ]]; then
  echo "Not inside a Git repository" >&2
  exit 1
fi

cd "$repo_root"

repo_name="$(basename "$repo_root")"
zip_path="$(dirname "$repo_root")/$repo_name.zip"

echo "Repository root : $repo_root"
echo "Archive output  : $zip_path"
echo

echo "Cleaning untracked and ignored files..."
git clean -fdx

echo "Creating archive from HEAD..."
git archive \
  --format=zip \
  --prefix="$repo_name/" \
  --output="$zip_path" \
  HEAD

echo
echo "Archive created successfully:"
echo "  $zip_path"
