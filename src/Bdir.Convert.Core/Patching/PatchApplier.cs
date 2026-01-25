using Bdir.Convert.Core.Hashing;
using Bdir.Convert.Core.Models;
using Bdir.Convert.Core.Wire;

namespace Bdir.Convert.Core.Patching;

public static class PatchApplier
{
    public static BdirDocument Apply(BdirDocument doc, WirePatchV1 patch, bool normalizeNfc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(patch);

        if (patch.Version != 1)
            throw new InvalidOperationException($"Unsupported patch version: {patch.Version}");

        var patchHa = string.IsNullOrWhiteSpace(patch.HashAlgorithm) ? "sha256" : patch.HashAlgorithm!;
        if (!string.Equals(patchHa, doc.HashAlgorithm, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"patch hash algorithm mismatch (patch.ha='{patchHa}', doc.hash_algorithm='{doc.HashAlgorithm}')");

        if (!string.Equals(patch.PageHash, doc.PageHash, StringComparison.Ordinal))
            throw new InvalidOperationException("patch page hash mismatch");

        var blocks = doc.Blocks.Select(b => b with { }).ToList();
        var byId = blocks.ToDictionary(b => b.BlockId, StringComparer.Ordinal);

        foreach (var op in patch.Ops)
        {
            switch (op)
            {
                case WireReplaceOp r:
                    ApplyReplace(byId, doc.HashAlgorithm, normalizeNfc, r);
                    break;

                case WireDeleteOp d:
                    ApplyDelete(byId, doc.HashAlgorithm, normalizeNfc, d);
                    break;

                case WireInsertAfterOp ins:
                    ApplyInsertAfter(blocks, byId, doc.HashAlgorithm, normalizeNfc, ins);
                    break;

                case WireSuggestOp:
                    // Advisory only; safe to ignore.
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported op type: {op.GetType().Name}");
            }
        }

        // Re-materialize blocks in their current order.
        var finalBlocks = blocks.Select(b => byId[b.BlockId]).ToList();

        // Recompute page hash from canonical block text.
        var pageText = string.Join("\n\n", finalBlocks.Select(b => b.Text));
        var pageHash = TextHash.HashUtf8Hex(doc.HashAlgorithm, pageText, normalizeNfc);

        return doc with { PageHash = pageHash, Blocks = finalBlocks };
    }

    private static void ApplyReplace(Dictionary<string, BdirBlock> byId, string hashAlgorithm, bool normalizeNfc, WireReplaceOp op)
    {
        var block = GetBlock(byId, op.BlockId);
        var before = TextHash.NormalizeUnicode(op.Before, normalizeNfc);
        var after = TextHash.NormalizeUnicode(op.After, normalizeNfc);
        var current = TextHash.NormalizeUnicode(block.Text, normalizeNfc);

        var (matchCount, positions) = FindNonOverlappingMatches(current, before);
        if (matchCount == 0)
            throw new InvalidOperationException("before substring not found");
        if (matchCount > 1 && op.Occurrence is null)
            throw new InvalidOperationException("ambiguous replace: before matches more than once; provide occurrence");

        var index = SelectOccurrence(matchCount, op.Occurrence);
        var pos = positions[index];

        var updated = current.Substring(0, pos) + after + current.Substring(pos + before.Length);
        var updatedHash = TextHash.HashUtf8Hex(hashAlgorithm, updated, normalizeNfc);

        byId[block.BlockId] = block with { Text = updated, TextHash = updatedHash };
    }

    private static void ApplyDelete(Dictionary<string, BdirBlock> byId, string hashAlgorithm, bool normalizeNfc, WireDeleteOp op)
    {
        var block = GetBlock(byId, op.BlockId);
        var before = TextHash.NormalizeUnicode(op.Before, normalizeNfc);
        var current = TextHash.NormalizeUnicode(block.Text, normalizeNfc);

        var (matchCount, positions) = FindNonOverlappingMatches(current, before);
        if (matchCount == 0)
            throw new InvalidOperationException("before substring not found");
        if (matchCount > 1 && op.Occurrence is null)
            throw new InvalidOperationException("ambiguous delete: before matches more than once; provide occurrence");

        var index = SelectOccurrence(matchCount, op.Occurrence);
        var pos = positions[index];

        var updated = current.Remove(pos, before.Length);
        var updatedHash = TextHash.HashUtf8Hex(hashAlgorithm, updated, normalizeNfc);

        byId[block.BlockId] = block with { Text = updated, TextHash = updatedHash };
    }

    private static void ApplyInsertAfter(List<BdirBlock> order, Dictionary<string, BdirBlock> byId, string hashAlgorithm, bool normalizeNfc, WireInsertAfterOp op)
    {
        // Validate target exists.
        _ = GetBlock(byId, op.BlockId);

        if (string.IsNullOrWhiteSpace(op.NewBlockId))
            throw new InvalidOperationException("insert_after requires new_block_id");
        if (byId.ContainsKey(op.NewBlockId))
            throw new InvalidOperationException($"new_block_id conflicts with existing block_id: {op.NewBlockId}");

        var text = TextHash.NormalizeUnicode(op.Text, normalizeNfc);
        var textHash = TextHash.HashUtf8Hex(hashAlgorithm, text, normalizeNfc);
        var newBlock = new BdirBlock(op.NewBlockId, op.KindCode, text, textHash);

        // Insert into order immediately after the referenced block.
        var idx = order.FindIndex(b => string.Equals(b.BlockId, op.BlockId, StringComparison.Ordinal));
        if (idx < 0)
            throw new InvalidOperationException("insert_after target block_id not found");

        order.Insert(idx + 1, newBlock);
        byId[newBlock.BlockId] = newBlock;
    }

    private static BdirBlock GetBlock(Dictionary<string, BdirBlock> byId, string blockId)
    {
        if (!byId.TryGetValue(blockId, out var block))
            throw new InvalidOperationException($"unknown block_id: {blockId}");
        return block;
    }

    private static int SelectOccurrence(int matchCount, int? occurrence)
    {
        if (occurrence is null)
            return 0;

        if (occurrence <= 0)
            throw new InvalidOperationException("occurrence must be >= 1");

        if (occurrence > matchCount)
            throw new InvalidOperationException("occurrence exceeds match count");

        return occurrence.Value - 1; // 1-indexed in RFC
    }

    private static (int matchCount, List<int> positions) FindNonOverlappingMatches(string text, string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            throw new InvalidOperationException("before substring must be non-empty");

        var positions = new List<int>();
        int i = 0;
        while (i <= text.Length - pattern.Length)
        {
            var idx = text.IndexOf(pattern, i, StringComparison.Ordinal);
            if (idx < 0)
                break;
            positions.Add(idx);
            i = idx + pattern.Length; // non-overlapping
        }
        return (positions.Count, positions);
    }
}
