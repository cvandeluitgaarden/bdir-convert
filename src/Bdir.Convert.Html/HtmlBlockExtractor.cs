using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Bdir.Convert.Core.Extraction;
using Bdir.Convert.Core.Models;

namespace Bdir.Convert.Html;

/// <summary>
/// Deterministic HTML -> BDIR block extractor using AngleSharp (HTML5 parser).
/// - Stateless: no I/O, no network
/// - Deterministic: same input + options => same blocks, ids, hashes
/// </summary>
public sealed partial class HtmlBlockExtractor : IBlockExtractor
{
    // Replace Regex.Replace usage with precompiled regexes using [GeneratedRegex]
    [GeneratedRegex(@"[ \t\f\v]+", RegexOptions.Compiled)]
    private static partial Regex CollapseWhitespaceRegex();

    [GeneratedRegex(@"\n{3,}", RegexOptions.Compiled)]
    private static partial Regex CollapseBlankLinesRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex CollapseAllWhitespaceRegex();

    // Block-like elements we treat as candidates.
    // Keep this list small and explicit to minimize surprises.
    private static readonly HashSet<string> BlockTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "h1","h2","h3","h4","h5","h6",
        "p",
        "blockquote",
        "pre",
        "li",      // only used when options.SplitListItems == true
        "table",   // split policy controlled by options.SplitTableRows
        "figcaption"
    };

    // Containers that often represent chrome/boilerplate; used for kind_code or skipping.
    private static readonly HashSet<string> BoilerplateContainers = new(StringComparer.OrdinalIgnoreCase)
    {
        "nav","footer","header","aside"
    };

    // Elements to always drop (non-content).
    private static readonly HashSet<string> AlwaysDrop = new(StringComparer.OrdinalIgnoreCase)
    {
        "script","style","noscript","template"
    };

    private readonly HtmlParser _parser;

    public HtmlBlockExtractor()
    {
        // Deterministic parsing: AngleSharp does not execute JS by default.
        var config = Configuration.Default;
        var context = BrowsingContext.New(config);
        _parser = new HtmlParser(new HtmlParserOptions
        {
            IsKeepingSourceReferences = false, // keep deterministic, no source offsets needed yet
            IsScripting = false
        }, context);
    }

    public BdirDocument Extract(string source, BlockExtractionOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        options ??= new BlockExtractionOptions();

        // Parse
        var doc = _parser.ParseDocument(source);

        // Normalize DOM view (drop known non-content nodes)
        NormalizeDomInPlace(doc, options);

        // Extract blocks in document order
        var blocks = ExtractBlocks(doc, options);

        // Page hash binds to canonical block text only (not raw HTML)
        var pageText = string.Join("\n\n", blocks.Select(b => b.Text));
        var pageHash = HashUtf8Hex(options.HashAlgorithm, pageText, options.NormalizeUnicodeNfc);

        return new BdirDocument(
            Version: 1,
            HashAlgorithm: options.HashAlgorithm,
            PageHash: pageHash,
            Blocks: blocks
        );
    }

    private static void NormalizeDomInPlace(IDocument doc, BlockExtractionOptions options)
    {
        // Drop comments deterministically
        foreach (var comment in doc.All.Where(n => n.NodeType == NodeType.Comment).ToArray())
            comment.Remove();

        // Drop always-drop tags
        foreach (var tag in AlwaysDrop)
        {
            foreach (var el in doc.QuerySelectorAll(tag).ToArray())
                el.Remove();
        }

        // Optional: drop hidden nodes (keep policy explicit)
        // If you want this, implement IsHidden(el) and remove them here.
        // For now: do nothing to avoid accidentally deleting real content.
        _ = options;
    }

    private static List<BdirBlock> ExtractBlocks(IDocument doc, BlockExtractionOptions options)
    {
        var result = new List<BdirBlock>(capacity: 256);

        var root = (IElement?)doc.Body ?? doc.DocumentElement ?? doc.QuerySelector("html") ?? doc.QuerySelector("body");
        if (root is null) return result;

        // Walk the DOM in a deterministic preorder traversal
        foreach (var el in EnumerateElementsPreOrder(root))
        {
            if (!IsCandidate(el, options))
                continue;

            // Split policies:
            // - If we're on <table> and SplitTableRows=true, we will handle rows instead of the table itself.
            // - If we're on <ul>/<ol> and SplitListItems=true, we rely on <li> candidates, so skip container.
            if (IsListContainer(el) && options.SplitListItems)
                continue;

            if (IsTable(el) && options.SplitTableRows)
            {
                ExtractTableRows(el, options, result);
                continue;
            }

            var kind = KindCodeFrom(el, options);

            // Canonical text
            var text = CanonicalizeElementText(el, options);
            if (string.IsNullOrWhiteSpace(text))
                continue;

            // Heading context (optional, but useful for stable ids)
            var ctx = ComputeHeadingContext(el);

            // Stable block id (replace this strategy later if you want)
            var blockId = ComputeStableBlockId(el, text, ctx);

            var textHash = HashUtf8Hex(options.HashAlgorithm, text, options.NormalizeUnicodeNfc);

            result.Add(new BdirBlock(
                BlockId: blockId,
                KindCode: kind,
                Text: text,
                TextHash: TruncateHash(textHash, 16) // keep small for now; adjust later if you want full digests
            ));
        }

        // Optional: enforce unique IDs (fail fast if collision)
        EnsureUniqueBlockIds(result);

        return result;
    }

    private static IEnumerable<IElement> EnumerateElementsPreOrder(IElement root)
    {
        // Deterministic preorder: yield element, then children in DOM order.
        var stack = new Stack<IElement>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            // push children in reverse so we pop them in document order
            var children = current.Children;
            for (int i = children.Length - 1; i >= 0; i--)
                stack.Push(children[i]);
        }
    }

    private static bool IsCandidate(IElement el, BlockExtractionOptions options)
    {
        var tag = el.TagName;

        // If we exclude boilerplate, skip candidates inside known boilerplate containers
        if (!options.IncludeBoilerplate && IsInsideBoilerplate(el))
            return false;

        if (BlockTags.Contains(tag))
            return true;

        // Treat <ul>/<ol> as a candidate only when NOT splitting list items
        if (IsListContainer(el) && !options.SplitListItems)
            return true;

        // Treat <table> as candidate when NOT splitting rows
        if (IsTable(el) && !options.SplitTableRows)
            return true;

        return false;
    }

    private static bool IsInsideBoilerplate(IElement el)
    {
        for (var p = el.ParentElement; p is not null; p = p.ParentElement)
        {
            if (BoilerplateContainers.Contains(p.TagName))
                return true;
        }
        return false;
    }

    private static bool IsListContainer(IElement el)
        => el.TagName.Equals("ul", StringComparison.OrdinalIgnoreCase)
        || el.TagName.Equals("ol", StringComparison.OrdinalIgnoreCase);

    private static bool IsTable(IElement el)
        => el.TagName.Equals("table", StringComparison.OrdinalIgnoreCase);

    private static void ExtractTableRows(IElement table, BlockExtractionOptions options, List<BdirBlock> output)
    {
        // Deterministic row order: use DOM order of <tr>
        var rows = table.QuerySelectorAll("tr");
        if (rows.Length == 0)
            return;

        int rowIndex = 0;
        foreach (var tr in rows)
        {
            rowIndex++;

            var kind = KindCodeFrom(tr, options);

            var text = CanonicalizeElementText(tr, options);
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var ctx = ComputeHeadingContext(table); // bind to table context
            var blockId = ComputeStableBlockId(tr, text, ctx + $"|row:{rowIndex}");

            var textHash = HashUtf8Hex(options.HashAlgorithm, text, options.NormalizeUnicodeNfc);

            output.Add(new BdirBlock(
                BlockId: blockId,
                KindCode: kind,
                Text: text,
                TextHash: TruncateHash(textHash, 16)
            ));
        }
    }

    private static int KindCodeFrom(IElement el, BlockExtractionOptions options)
    {
        // Simple, deterministic mapping. Refine later.
        // Content range: 0-19; Boilerplate: 20-39; UI: 40-59; Unknown: 99.
        _ = options;

        // If inside boilerplate container, mark as boilerplate (unless you decide to drop instead).
        if (IsInsideBoilerplate(el))
            return 25;

        var tag = el.TagName.ToLowerInvariant();

        if (tag is "h1" or "h2" or "h3" or "h4" or "h5" or "h6")
            return 2;

        if (tag is "p" or "blockquote" or "figcaption")
            return 5;

        if (tag is "pre")
            return 8;

        if (tag is "li")
            return 6;

        if (tag is "table" or "tr")
            return 10;

        // Default content
        return 99;
    }

    private static string CanonicalizeElementText(IElement el, BlockExtractionOptions options)
    {
        // Preformatted: keep line breaks and spaces as-is (but normalize line endings + NFC if configured)
        if (el.TagName.Equals("pre", StringComparison.OrdinalIgnoreCase))
        {
            var raw = el.TextContent ?? string.Empty;
            raw = NormalizeLineEndings(raw);
            return NormalizeUnicode(raw.TrimEnd(), options.NormalizeUnicodeNfc);
        }

        // Generic: walk text with basic whitespace collapsing
        var text = ExtractTextWithBreaks(el);
        text = NormalizeLineEndings(text);

        // collapse whitespace runs to single spaces, but preserve intentional newlines
        // strategy: collapse spaces/tabs, then collapse multiple blank lines
        text = CollapseWhitespaceRegex().Replace(text, " ");
        text = CollapseBlankLinesRegex().Replace(text, "\n\n");
        text = text.Trim();

        return NormalizeUnicode(text, options.NormalizeUnicodeNfc);
    }

    private static string ExtractTextWithBreaks(IElement el)
    {
        // Deterministic text extraction:
        // - include text nodes
        // - treat <br> as newline
        // - add newline between certain block-ish children to avoid run-ons
        var sb = new StringBuilder();

        void Walk(INode node)
        {
            if (node.NodeType == NodeType.Text)
            {
                sb.Append(node.TextContent);
                return;
            }

            if (node is IElement childEl)
            {
                if (childEl.TagName.Equals("br", StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append('\n');
                    return;
                }

                // Skip always-drop tags defensively
                if (AlwaysDrop.Contains(childEl.TagName))
                    return;

                // Recurse
                foreach (var c in childEl.ChildNodes)
                    Walk(c);

                // Add a newline after certain elements to avoid accidental concatenation
                var tag = childEl.TagName;
                if (tag.Equals("p", StringComparison.OrdinalIgnoreCase) ||
                    tag.Equals("li", StringComparison.OrdinalIgnoreCase) ||
                    tag.Equals("div", StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append('\n');
                }
            }
        }

        foreach (var c in el.ChildNodes)
            Walk(c);

        return sb.ToString();
    }

    private static string ComputeHeadingContext(IElement el)
    {
        // Deterministic heuristic: find nearest preceding headings (h1..h3) in document order.
        // MVP: only uses nearest previous heading of any level.
        var heading = FindNearestPreviousHeading(el);
        if (heading is null)
            return string.Empty;

        var tag = heading.TagName.ToLowerInvariant();
        var text = (heading.TextContent ?? string.Empty).Trim();
        text = CollapseAllWhitespaceRegex().Replace(text, " ");
        if (text.Length > 80) text = text[..80];

        return $"{tag}:{text}";
    }

    private static IElement? FindNearestPreviousHeading(IElement el)
    {
        // Walk backwards in DOM order: use previous siblings then ascend.
        INode? n = el;

        while (n is not null)
        {
            // Try previous sibling chain, deep last-child walk
            var prev = n.PreviousSibling;
            while (prev is not null)
            {
                var found = FindLastHeadingInSubtree(prev);
                if (found is not null) return found;
                prev = prev.PreviousSibling;
            }

            n = n.Parent;
        }

        return null;
    }

    private static IElement? FindLastHeadingInSubtree(INode node)
    {
        // Depth-first from the end to find the last heading.
        if (node is IElement el)
        {
            if (IsHeading(el))
                return el;

            // Search children from end to start
            for (int i = el.Children.Length - 1; i >= 0; i--)
            {
                var found = FindLastHeadingInSubtree(el.Children[i]);
                if (found is not null) return found;
            }
        }
        return null;
    }

    private static bool IsHeading(IElement el)
    {
        var t = el.TagName;
        return t.Equals("h1", StringComparison.OrdinalIgnoreCase)
            || t.Equals("h2", StringComparison.OrdinalIgnoreCase)
            || t.Equals("h3", StringComparison.OrdinalIgnoreCase)
            || t.Equals("h4", StringComparison.OrdinalIgnoreCase)
            || t.Equals("h5", StringComparison.OrdinalIgnoreCase)
            || t.Equals("h6", StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeStableBlockId(IElement el, string canonicalText, string context)
    {
        // Recommended strategy C (content-fingerprint with locality):
        // id = <tag>_<sha256(ctx + "\n" + tag + "\n" + textPrefix)[0..12]>
        var tag = el.TagName.ToLowerInvariant();
        var prefix = canonicalText.Length <= 64 ? canonicalText : canonicalText[..64];

        var material = $"{context}\n{tag}\n{prefix}";
        var fp = Sha256Hex(Encoding.UTF8.GetBytes(material));
        return $"{tag}_{fp[..12]}";
    }

    private static void EnsureUniqueBlockIds(List<BdirBlock> blocks)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var b in blocks)
        {
            if (!seen.Add(b.BlockId))
                throw new InvalidOperationException($"block_id collision detected: {b.BlockId}");
        }
    }

    private static string HashUtf8Hex(string algorithm, string text, bool normalizeNfc)
    {
        // For now: support sha256 only (baseline). Add others later behind the same interface.
        var canonical = NormalizeUnicode(text, normalizeNfc);
        var bytes = Encoding.UTF8.GetBytes(canonical);

        return algorithm.ToLowerInvariant() switch
        {
            "sha256" => Sha256Hex(bytes),
            _ => throw new NotSupportedException($"Unsupported hash_algorithm: '{algorithm}'")
        };
    }

    private static string Sha256Hex(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return System.Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string TruncateHash(string hex, int prefixLen)
    {
        if (prefixLen <= 0) return string.Empty;
        if (hex.Length <= prefixLen) return hex;
        return hex[..prefixLen];
    }

    private static string NormalizeLineEndings(string s)
        => s.Replace("\r\n", "\n").Replace("\r", "\n");

    private static string NormalizeUnicode(string s, bool nfc)
        => nfc ? s.Normalize(NormalizationForm.FormC) : s;
}
