# bdir-convert

Converters between common document formats (HTML, Markdown, plain text, …) and **BDIR**.

This repository focuses on:
- Converting source documents into BDIR + an anchored copy of the source
- Creating a BDIR EditPacket from the conversion output
- Applying validated BDIR patches back onto the anchored copy
- Removing anchors and emitting the final document in the original format

## Goals

- **Deterministic** conversion and patch application
- **Round-trip fidelity**: patch the *original* document (via anchors), not a regenerated one
- **Format-agnostic core** with format-specific converters
- **Safety-first**: validate inputs, stable error messages, predictable behavior

## Pipeline

### Convert to BDIR

Input: `document.<ext>`

Output:
- `document.anchored.<ext>` (copy of original with BDIR anchors injected)
- `document.editpacket.json` (BDIR EditPacket derived from the anchored document)
- (optional) `document.anchors.json` (sidecar map for debugging and tooling)

Steps:
1. Read source document
2. Create an anchored copy by injecting begin/end anchors around extracted blocks
3. Extract blocks (canonical text) and build a BDIR EditPacket

### Convert back to original

Input:
- `document.anchored.<ext>`
- `document.patched.bdir.json` (or `editpacket.json` + `patch.json`)

Output:
- `document.<ext>` (final, anchors removed)

Steps:
1. Apply patch to the BDIR blocks (BDIR-level logic)
2. Materialize the block changes into the anchored copy by replacing content within anchors
3. Remove anchors
4. Emit updated document in the original format

## Repository layout (proposed)

- `src/`
  - `Bdir.Convert.Core/` — shared types + canonicalization + validation helpers
  - `Bdir.Convert.Cli/` — CLI entrypoint
  - `Bdir.Convert.Html/` — HTML converter (anchors + extraction + apply-back)
  - `Bdir.Convert.Markdown/` — Markdown converter
- `tests/`
  - `Bdir.Convert.Tests/` — golden tests + interoperability tests
- `docs/`
  - design notes, format specs, anchor conventions

## Development

### Prerequisites
- .NET SDK (recommended: latest LTS)

### Build
```bash
dotnet build
