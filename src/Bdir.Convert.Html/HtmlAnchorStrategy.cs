using System.Security.Cryptography;
using System.Text;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Bdir.Convert.Core.Anchoring;
using Bdir.Convert.Core.Models;

namespace Bdir.Convert.Html;

/// <summary>
/// Deterministic anchoring for HTML sources.
/// Inserts reversible data-attributes on elements that correspond to extracted BDIR blocks.
/// </summary>
public sealed class HtmlAnchorStrategy : IAnchorStrategy
{
    public const string AttrBlockId = "data-bdir-block";
    public const string AttrKindCode = "data-bdir-kind";

    private readonly HtmlParser _parser;

    public HtmlAnchorStrategy()
    {
        var config = Configuration.Default;
        var context = BrowsingContext.New(config);
        _parser = new HtmlParser(new HtmlParserOptions
        {
            IsKeepingSourceReferences = false,
            IsScripting = false
        }, context);
    }

    public AnchoredSource Anchor(string originalSource, IReadOnlyList<BdirBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(originalSource);
        ArgumentNullException.ThrowIfNull(blocks);

        // Map block_id -> kind_code (authoritative from extracted blocks)
        var byId = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var b in blocks)
            byId[b.BlockId] = b.KindCode;

        var doc = _parser.ParseDocument(originalSource);
        NormalizeDomInPlace(doc);

        var root = (IElement?)doc.Body ?? doc.DocumentElement ?? doc.QuerySelector("html") ?? doc.QuerySelector("body");
        if (root is not null)
        {
            foreach (var el in EnumerateElementsPreOrder(root))
            {
                // Only attempt to anchor elements that could have produced blocks.
                // We intentionally keep this broad; final inclusion is driven by byId.
                if (!IsAnchorCandidateTag(el.TagName))
                    continue;

                // When tables are split into rows, blocks will exist for <tr>, not for <table>.
                if (el.TagName.Equals("table", StringComparison.OrdinalIgnoreCase))
                {
                    // Anchor rows if present in byId
                    foreach (var tr in el.QuerySelectorAll("tr").OfType<IElement>())
                        TryAnchorElement(tr, byId);

                    // Also attempt to anchor the table itself (when not splitting rows)
                    TryAnchorElement(el, byId);
                    continue;
                }

                // Regular element anchoring
                TryAnchorElement(el, byId);
            }
        }

        // Serialize
        var html = doc.DocumentElement?.OuterHtml ?? doc.ToHtml();

        return new AnchoredSource(
            OriginalSource: originalSource,
            Content: html
        );
    }

    public string StripAnchors(string anchoredSource)
    {
        ArgumentNullException.ThrowIfNull(anchoredSource);

        var doc = _parser.ParseDocument(anchoredSource);
        NormalizeDomInPlace(doc);

        var root = (IElement?)doc.Body ?? doc.DocumentElement ?? doc.QuerySelector("html") ?? doc.QuerySelector("body");
        if (root is not null)
        {
            foreach (var el in EnumerateElementsPreOrder(root))
            {
                if (el.HasAttribute(AttrBlockId))
                    el.RemoveAttribute(AttrBlockId);
                if (el.HasAttribute(AttrKindCode))
                    el.RemoveAttribute(AttrKindCode);
            }
        }

        return doc.DocumentElement?.OuterHtml ?? doc.ToHtml();
    }

    private static void TryAnchorElement(IElement el, Dictionary<string, int> byId)
    {
        var blockId = ComputeStableBlockId(el);
        if (!byId.TryGetValue(blockId, out var kind))
            return;

        el.SetAttribute(AttrBlockId, blockId);
        el.SetAttribute(AttrKindCode, kind.ToString());
    }

    private static bool IsAnchorCandidateTag(string tag)
    {
        // Superset of HtmlBlockExtractor.BlockTags + list containers and table row.
        return tag.Equals("h1", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("h2", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("h3", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("h4", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("h5", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("h6", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("p", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("blockquote", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("pre", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("li", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("table", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("tr", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("figcaption", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("ul", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("ol", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<IElement> EnumerateElementsPreOrder(IElement root)
    {
        var stack = new Stack<IElement>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            var children = current.Children;
            for (int i = children.Length - 1; i >= 0; i--)
                stack.Push(children[i]);
        }
    }

    private static void NormalizeDomInPlace(IDocument doc)
    {
        // Drop comments deterministically (align with extractor)
        foreach (var comment in doc.All.Where(n => n.NodeType == NodeType.Comment).ToArray())
            comment.Remove();

        // Drop always-drop tags (align with extractor)
        foreach (var tag in new[] { "script", "style", "noscript", "template" })
        {
            foreach (var el in doc.QuerySelectorAll(tag).ToArray())
                el.Remove();
        }
    }

    private static string ComputeStableBlockId(IElement el)
    {
        // Must match HtmlBlockExtractor.ComputeStableBlockId (tag + sha256(domPath)[0..12])
        var tag = el.TagName.ToLowerInvariant();
        var domPath = ComputeDomPath(el);

        var fp = Sha256Hex(Encoding.UTF8.GetBytes(domPath));
        return $"{tag}_{fp[..12]}";
    }

    private static string ComputeDomPath(IElement el)
    {
        var segments = new List<string>();
        IElement? cur = el;

        while (cur is not null)
        {
            var tag = cur.TagName.ToLowerInvariant();
            var index = 1;

            var parent = cur.ParentElement;
            if (parent is not null)
            {
                index = 0;
                foreach (var sibling in parent.Children)
                {
                    if (!string.Equals(sibling.TagName, cur.TagName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    index++;
                    if (ReferenceEquals(sibling, cur))
                        break;
                }

                if (index == 0) index = 1;
            }

            segments.Add($"{tag}[{index}]");
            cur = parent;
        }

        segments.Reverse();
        return "/" + string.Join("/", segments);
    }

    private static string Sha256Hex(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return System.Convert.ToHexString(hash).ToLowerInvariant();
    }
}
