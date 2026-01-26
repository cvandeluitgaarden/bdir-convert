using System.Collections.Generic;
using System.Globalization;
using AngleSharp.Dom;
using Bdir.Convert.Core.Extraction;
using Bdir.Convert.Core.Models;

namespace Bdir.Convert.Html;

public sealed partial class HtmlBlockExtractor
{
    private const string AttrBlockId = "data-bdir-block";
    private const string AttrKindCode = "data-bdir-kind";

    /// <summary>
    /// Produces an anchored copy of the input HTML by injecting deterministic BDIR anchor attributes.
    /// This is non-mutating with respect to visible text content.
    /// </summary>
    public string AnchorHtml(string source, BlockExtractionOptions options, IReadOnlyList<BdirBlock> blocks)
    => AnchorHtml(source, options, blocks, warnings: null);

    /// <summary>
    /// Produces an anchored copy of the input HTML and optionally collects warnings for anchors that
    /// contain child elements (which would be clobbered by naive TextContent-based apply).
    /// </summary>
    public string AnchorHtml(string source, BlockExtractionOptions options, IReadOnlyList<BdirBlock> blocks, IList<HtmlAnchorWarning>? warnings)
    {
        ArgumentNullException.ThrowIfNull(source);
        options ??= new BlockExtractionOptions();
        ArgumentNullException.ThrowIfNull(blocks);

        // Map the extracted blocks by id so we only anchor blocks we actually produced.
        var byId = blocks
            .GroupBy(b => b.BlockId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var doc = _parser.ParseDocument(source);
        NormalizeDomInPlace(doc, options);

        var root = (IElement?)doc.Body ?? doc.DocumentElement ?? doc.QuerySelector("html") ?? doc.QuerySelector("body");
        if (root is null)
            return source;

        foreach (var el in EnumerateElementsPreOrder(root))
        {
            if (!IsCandidate(el, options))
                continue;

            // Mirror the extractor's split policies
            if (IsListContainer(el) && options.SplitListItems)
                continue;

            if (IsTable(el) && options.SplitTableRows)
            {
                AnchorTableRows(el, options, byId, warnings);
                continue;
            }

            var text = CanonicalizeElementText(el, options);
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var ctx = ComputeHeadingContext(el);
            var blockId = ComputeStableBlockId(el, text, ctx);

            if (!byId.TryGetValue(blockId, out var block))
                continue;

            SetAnchorAttrs(el, block);
            MaybeWarnAnchorClobberRisk(el, block, warnings);
        }

        // Serialize with anchors. AngleSharp keeps deterministic DOM order.
        // Use OuterHtml to avoid requiring a TextWriter-based formatter overload.
        return doc.DocumentElement?.OuterHtml ?? source;
    }

    /// <summary>
    /// Removes BDIR anchor attributes from an anchored HTML document.
    /// </summary>
    public string StripAnchors(string anchoredHtml)
    {
        ArgumentNullException.ThrowIfNull(anchoredHtml);

        var doc = _parser.ParseDocument(anchoredHtml);

        foreach (var el in doc.All)
        {
            if (el.HasAttribute(AttrBlockId))
                el.RemoveAttribute(AttrBlockId);
            if (el.HasAttribute(AttrKindCode))
                el.RemoveAttribute(AttrKindCode);
        }

        return doc.DocumentElement?.OuterHtml ?? anchoredHtml;
    }

    private static void AnchorTableRows(IElement table, BlockExtractionOptions options, Dictionary<string, BdirBlock> byId, IList<HtmlAnchorWarning>? warnings)
    {
        var rows = table.QuerySelectorAll("tr");
        if (rows.Length == 0)
            return;

        int rowIndex = 0;
        foreach (var tr in rows)
        {
            rowIndex++;

            var text = CanonicalizeElementText(tr, options);
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var ctx = ComputeHeadingContext(table);
            var blockId = ComputeStableBlockId(tr, text, ctx + $"|row:{rowIndex}");

            if (!byId.TryGetValue(blockId, out var block))
                continue;

            SetAnchorAttrs(tr, block);
            MaybeWarnAnchorClobberRisk(tr, block, warnings);
        }
    }

    private static void MaybeWarnAnchorClobberRisk(IElement el, BdirBlock block, IList<HtmlAnchorWarning>? warnings)
    {
        if (warnings is null) return;

        // If the anchor contains element children, a naive "el.TextContent = ..." apply would remove them.
        if (el.Children.Length == 0) return;

        var childTags = new List<string>(capacity: el.Children.Length);
        foreach (var c in el.Children)
            childTags.Add(c.TagName.ToLowerInvariant());

        warnings.Add(new HtmlAnchorWarning(
            BlockId: block.BlockId,
            KindCode: block.KindCode,
            TagName: el.TagName.ToLowerInvariant(),
            ChildElementTags: childTags,
            Message: "Anchored element contains child elements; TextContent-based patch-back would clobber inline markup."
        ));
    }

    private static void SetAnchorAttrs(IElement el, BdirBlock block)
    {
        el.SetAttribute(AttrBlockId, block.BlockId);
        el.SetAttribute(AttrKindCode, block.KindCode.ToString(CultureInfo.InvariantCulture));
    }
}