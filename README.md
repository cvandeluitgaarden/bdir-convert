# bdir-convert

**bdir-convert** is a deterministic document conversion toolkit for producing
**BDIR (Block-based Document Intermediate Representation)** from source formats
such as HTML.

This repository focuses on **correctness, determinism, and auditability** rather
than convenience rewriting. It is designed to support AI-assisted review
workflows where all proposed changes are explicit, reviewable, and safely
applicable.

---

## Goals

- Deterministic conversion from source documents to BDIR
- Stable block identifiers and hashes
- RFC-aligned wire output (Edit Packet format)
- Human- and machine-reviewable diffs
- Safe regeneration of expected outputs via golden fixtures

Non-goals:

- WYSIWYG editing
- Browser-like rendering
- JavaScript execution
- Implicit or heuristic rewriting

---

## Project structure

```
bdir-convert/
├─ src/
│  ├─ Bdir.Convert.Core/        # Core models, extraction contracts, wire helpers
│  ├─ Bdir.Convert.Html/        # HTML → BDIR extractor (AngleSharp)
│  └─ Bdir.Convert.Cli/         # Command-line interface
│
├─ tests/
│  └─ Bdir.Convert.Html.Tests/
│     ├─ Fixtures/             # Golden test inputs + expected outputs
│     └─ TestSupport/           # JSON asserts, helpers
│
├─ docs/
│  ├─ ANCHORS.md
│  └─ ROADMAP.md
│
├─ scripts/
│  └─ test.sh                  # Test wrapper (excludes regen by default)
│
└─ bdir-convert.sln
```

---

## Conversion model (high level)

1. Parse source document (e.g. HTML) using a deterministic parser
2. Extract semantic blocks in document order
3. Canonicalize text (Unicode NFC, whitespace rules)
4. Assign stable block identifiers
5. Compute block-level and page-level hashes
6. Emit BDIR and RFC-style Edit Packet wire output

No network access, no clocks, no randomness.

---

## RFC-style wire output

Golden fixtures and CLI output use an RFC-aligned **Edit Packet** shape:

```json
{
  "v": 1,
  "h": "page_hash",
  "ha": "sha256",
  "b": [
    ["block_id", kind_code, "text_hash", "text"]
  ]
}
```

- `v`  : protocol version
- `h`  : page-level content hash
- `ha` : hash algorithm
- `b`  : ordered block tuples

This mirrors the BDIR Patch Protocol wire format and enables
safe downstream validation and patching.

---

## Testing strategy

This repository uses **golden fixtures** to enforce determinism.

### Test categories

- **GoldenDeterminismTests**
  - Compare extractor output against committed golden files
  - Run in CI
  - Never write files

- **GoldenRegenTests**
  - Regenerate `expected.bdir.json` files
  - Run manually only
  - Explicitly excluded from CI

### Running tests

```bash
# Normal tests (CI-safe)
./scripts/test.sh

# Regenerate golden fixtures
./scripts/test.sh --regen
```

CI enforces `Category!=Regen` by default.

---

## CLI usage

Regenerate golden fixtures explicitly:

```bash
dotnet run --project src/Bdir.Convert.Cli --   regen-goldens tests/Bdir.Convert.Html.Tests/Fixtures
```

The CLI always writes expected outputs into the project directory,
never into build output folders.

---

## Determinism guarantees

The following are treated as invariants:

- Same input + same options ⇒ identical output
- Stable block ordering
- Stable block identifiers
- Hash truncation ≥ 8 hex characters
- No hidden defaults or environment-dependent behavior

Any change that affects output **must** update golden fixtures and be reviewed.

---

## License

This project is licensed under the **MIT License**.
See [LICENSE](LICENSE) for details.

---

## Status

This project is under active development.
The public APIs should be considered **unstable** until a 1.0 release.

Feedback and contributions are welcome, especially around:
- additional converters
- edge-case fixtures
- RFC alignment
