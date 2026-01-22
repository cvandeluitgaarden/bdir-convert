# Testing

Golden fixtures define the canonical **HTML → BDIR** mapping.

## Golden tests

- **GoldenDeterminismTests**: compare only (runs in CI)
- **GoldenRegenTests**: regenerate expected outputs (manual)

Regeneration must be intentional and reviewed.

## Fixture layout

Each fixture lives under:

```
tests/Bdir.Convert.Html.Tests/Fixtures/<fixture-name>/
  input.html
  options.json
  expected.bdir.json
```

`expected.bdir.json` is stored in RFC-ish wire format (`v/ha/h/b` tuples).

## Running tests

### Normal (CI-safe)

```bash
dotnet test --filter "Category!=Regen"
```

Or via wrapper:

```bash
./scripts/test.sh
```

### Regen (manual)

```bash
dotnet test --filter "Category=Regen"
```

Or:

```bash
./scripts/test.sh --regen
```

### CI enforcement

CI must not run regen tests. The workflow uses:

```bash
dotnet test --filter "Category!=Regen"
```

Optionally, `Directory.Build.targets` can enforce this by failing if CI runs without a filter.

## Diff-friendly failures

Golden comparisons normalize JSON semantically. On mismatch, the test writes:

- `*.expected.json`
- `*.actual.json`

to a temp directory (printed in the failure output) so you can diff locally with your preferred tool.
