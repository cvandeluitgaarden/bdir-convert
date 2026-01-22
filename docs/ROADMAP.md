# Roadmap

This roadmap captures the intended milestones for **bdir-convert**. Items may shift as the BDIR protocol evolves.

## v0: Core + HTML MVP (current)

### Implemented

- Core extraction contracts (`IBlockExtractor`, `IAnchorStrategy`)
- HTML → BDIR block extraction (AngleSharp)
  - headings, paragraphs, list items, pre/code-ish blocks, table support
- RFC-ish wire output helpers in Core (`WireEditPacketV1`, canonical JSON)
- Golden determinism tests (compare-only + explicit regen)
- CLI commands:
  - `convert-html` (HTML → BDIR wire output)
  - `regen-goldens` (fixture regeneration)
- Repo hygiene helpers:
  - archive scripts that zip tracked files only (`scripts/archive.*`)
  - CI test filtering excludes regen tests

### In progress / next

- HTML anchoring strategy (inject + strip)
- `--anchor-html-out` support for `convert-html`
- Stronger block ID stability rules (documented + tested)
- More fixtures (tables, malformed HTML, Unicode NFC edge cases)

## v0.1: HTML apply-back (planned)

- Apply validated BDIR edits back to anchored HTML:
  - strict binding to page hash (`h/ha`) where applicable
  - conservative “inline-aware apply” mode (preserve tags, update text nodes)
- Strip anchors and export updated HTML
- End-to-end tests: input HTML → BDIR → patch → exported HTML

## v1: Markdown (planned)

- Markdown → BDIR block extraction
- Markdown anchoring strategy
- Apply-back pipeline for Markdown (where feasible)

## v2: More formats (exploratory)

- PDF (likely via intermediate text extraction; anchoring may be limited)
- DOCX (structured anchoring possible)
- Plain text (trivial)

## Quality gates (always)

- Determinism: same input + options ⇒ identical output
- Golden fixtures required for behavioral changes
- No network, no clocks, no randomness
- Explicit options (no hidden defaults)
