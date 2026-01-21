# Roadmap

## v0: Core + HTML MVP
- Core types + canonicalization helpers
- Anchor injector + remover for HTML
- Extract blocks from HTML (headings, paragraphs, list items, code blocks)
- Create EditPacket
- Apply patched BDIR back onto anchored HTML
- Golden tests

## v1: Markdown
- Anchor injector + remover for Markdown
- Block extraction (headings, paragraphs, list items, code fences)
- Apply-back with minimal formatting disruption

## v2: More formats + libraries
- Plain text converter
- PDF (likely “extract text only” unless you do a more advanced model)
- Public NuGet packages for `Core` and common converters
- Stable CLI interface
