# BDIR Anchors

This document describes how **bdir-convert** injects and removes anchors in source formats (starting with HTML).

Anchors are a *conversion-time* mechanism that lets you safely apply user-approved BDIR edits back onto a copy of the original source, while preserving formatting and structure.

## Purpose

Anchors allow us to:

- Preserve original document formatting and structure
- Apply BDIR block updates directly onto a copy of the original source
- Remove anchors to produce a clean output document
- Keep extraction + application deterministic and reviewable

## Terminology

- **Original source**: The input document as provided by the user (e.g. `input.html`).
- **Anchored source**: A copy of the original source with machine-inserted anchor markers (not intended for end users).
- **Anchor marker**: A minimal, unambiguous marker that binds a region of source to a `block_id`.

## General rules

- Anchors exist **only** in the anchored copy (never in final output).
- Anchors must be **stable** for identical input + options.
- Anchors must be **reversible**:

  - `StripAnchors(Anchor(original)) == original` (byte-for-byte preferred)

- Anchors must be **non-rendering / non-visible** when possible.
- Anchors must uniquely map to a single `block_id` (no ambiguity).

## Pipeline overview

### HTML → BDIR conversion

1. Parse HTML deterministically (no JS execution, no network).
2. Extract canonical blocks (document order).
3. Compute `text_hash` per block and `h` (page hash).
4. Emit RFC-ish edit packet wire output: `v/ha/h/b`.

### Applying edits back to HTML (planned)

1. Rebuild the same anchored HTML from the same original HTML (deterministic).
2. Apply validated edits by locating anchor markers and updating text regions.
3. Strip anchors from the updated anchored copy.
4. Output clean HTML.

This is intentionally **two-phase** (anchor then apply) to keep the transformation auditable.

## HTML anchor strategy (planned)

For HTML, anchors should be implemented in a way that:

- Preserves the DOM structure (no large wrapper spans unless unavoidable)
- Survives pretty-printing/reformatting where possible
- Can be removed reliably

Recommended approach (initial):

- Insert a short, unique `data-bdir` attribute on the *nearest stable element* that corresponds to the block boundary:

  - Example: `<p data-bdir="p_abc123...">Hello</p>`

Where element anchoring is not possible, fall back to a comment-based anchor:

- `<!-- bdir:block_id=p_abc123... -->`

### Safety notes

- Anchor insertion must not change user-visible text.
- Anchor removal must be strict: remove only anchors created by this tool.
- Anchor application must be conservative in “inline-aware” mode:
  - update text nodes while preserving inline tags (`<strong>`, `<em>`, links, etc.)

## CLI integration

Planned/target flags:

- `convert-html <input.html> --anchor-html-out <file>`  
  Writes an anchored copy of the input HTML.

- `export-html-out <file>` (future)  
  Applies a validated patch to anchored HTML, strips anchors, and writes clean HTML.

## Status

- Block extraction + RFC-ish wire output: **implemented**
- HTML anchoring (`HtmlAnchorStrategy`): **planned / in progress**
- Apply-to-HTML + strip anchors: **planned**
