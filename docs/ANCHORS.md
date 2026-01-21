# BDIR Anchors

This document describes how bdir-convert injects and removes anchors in source formats.

## Purpose

Anchors allow us to:
- Preserve original document formatting and structure
- Apply BDIR block updates directly onto a copy of the original source
- Remove anchors to produce a clean output document

## General rules

- Anchors exist **only** in the anchored copy (never in final output).
- Each block has exactly one begin/end anchor pair.
- Anchors must be unique by `block_id`.
- Anchor placement must be deterministic.

## Anchor forms

### HTML
Injected as comments:

- `<!-- bdir:begin id=<block_id> -->`
- `<!-- bdir:end id=<block_id> -->`

### Markdown
Use HTML comments for broad compatibility:

- `<!-- bdir:begin id=<block_id> -->`
- `<!-- bdir:end id=<block_id> -->`

### Plain text
Sentinel lines:

- `[[BDIR_BEGIN <block_id>]]`
- `[[BDIR_END <block_id>]]`

## Replacement semantics

When applying a patched BDIR document back to the anchored copy:
- The content between begin/end anchors is replaced with the updated block text.
- After all replacements, all anchors are removed.

Format-specific encoders may escape content as needed (e.g., HTML text nodes).
