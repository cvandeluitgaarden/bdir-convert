using System.Linq;
using AngleSharp;
using AngleSharp.Dom;
using Bdir.Convert.Core.Extraction;
using Bdir.Convert.Core.Models;

namespace Bdir.Convert.Html;

/// <summary>
/// Applies a patched BDIR view back onto HTML by using BDIR anchors as a targeting layer.
/// Note: this is currently "inline-unaware"; it replaces the element's text content.
/// </summary>
public static class HtmlPatchRenderer
{
    private const string AttrBlockId = "data-bdir-block";
    private const string AttrKindCode = "data-bdir-kind";

    public static string ApplyPatchedBlocks(string originalHtml, BlockExtractionOptions options, HtmlBlockExtractor extractor, IReadOnlyList<BdirBlock> originalBlocks, IReadOnlyList<BdirBlock> patchedBlocks, bool stripAnchors)
    {
        ArgumentNullException.ThrowIfNull(originalHtml);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(extractor);
        ArgumentNullException.ThrowIfNull(originalBlocks);
        ArgumentNullException.ThrowIfNull(patchedBlocks);

        // Step 1: Anchor a copy of the original.
        var anchored = extractor.AnchorHtml(originalHtml, options, originalBlocks);

        // Step 2: Load anchored HTML and update blocks by id.
        var context = BrowsingContext.New(Configuration.Default);
        var doc = context.OpenAsync(req => req.Content(anchored)).GetAwaiter().GetResult();
        var root = doc.Body ?? doc.DocumentElement;
        if (root is null)
            return anchored;

        var byId = patchedBlocks.ToDictionary(b => b.BlockId, StringComparer.Ordinal);

        // Update existing anchored elements.
        foreach (var el in doc.All.Where(e => e.HasAttribute(AttrBlockId)))
        {
            var id = el.GetAttribute(AttrBlockId);
            if (id is null) continue;
            if (!byId.TryGetValue(id, out var block)) continue;

            // Inline-unaware: replace text content.
            el.TextContent = block.Text;
            el.SetAttribute(AttrKindCode, block.KindCode.ToString());
        }

        // Insert newly created blocks for insert_after operations.
        // These will not exist in originalBlocks; we insert simple <div> nodes after the referenced element if possible.
        var originalIds = new HashSet<string>(originalBlocks.Select(b => b.BlockId), StringComparer.Ordinal);
        var inserted = patchedBlocks.Where(b => !originalIds.Contains(b.BlockId)).ToList();
        if (inserted.Count > 0)
        {
            // Best-effort: insert in order relative to the patched block list.
            for (int i = 0; i < patchedBlocks.Count; i++)
            {
                var b = patchedBlocks[i];
                if (originalIds.Contains(b.BlockId))
                    continue;

                // Find previous block that exists in DOM to anchor insertion point.
                var prev = i > 0 ? patchedBlocks[i - 1] : null;
                if (prev is null) continue;

                var prevEl = doc.All.FirstOrDefault(e => string.Equals(e.GetAttribute(AttrBlockId), prev.BlockId, StringComparison.Ordinal));
                if (prevEl is null) continue;

                var wrapper = doc.CreateElement("div");
                wrapper.SetAttribute(AttrBlockId, b.BlockId);
                wrapper.SetAttribute(AttrKindCode, b.KindCode.ToString());
                wrapper.TextContent = b.Text;

                prevEl.After(wrapper);
            }
        }

        var htmlOut = doc.DocumentElement?.OuterHtml ?? anchored;
        return stripAnchors ? extractor.StripAnchors(htmlOut) : htmlOut;
    }
}
