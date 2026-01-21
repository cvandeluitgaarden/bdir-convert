# Contributing

Thanks for helping improve bdir-convert.

## How to contribute

1. Fork the repo
2. Create a branch: `git checkout -b feat/my-change`
3. Make changes with tests
4. Run:
   - `dotnet format` (if configured)
   - `dotnet test`
5. Open a PR

## Coding guidelines

- Prefer small, focused PRs
- Keep behavior deterministic
- Keep stable, user-facing error messages
- Add tests for:
  - parsing/extraction
  - anchor injection/removal
  - apply-back behavior
  - edge cases (missing anchors, duplicates, malformed input)

## Commit message style

Use conventional-ish prefixes:

- `feat(converter): ...`
- `fix(core): ...`
- `docs: ...`
- `test: ...`
- `refactor: ...`
