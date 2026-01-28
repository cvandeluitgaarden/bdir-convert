using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Bdir.Convert.Html;

/// <summary>
/// Preserves raw <script> and <style> blocks across AngleSharp parse/serialize cycles by
/// replacing them with stable placeholders before parsing and reinserting them verbatim after.
/// </summary>
internal static class HtmlRawPreserver
{
    private static readonly Regex ScriptStyleRegex = new(
        @"<\s*(script|style)\b[^>]*>[\s\S]*?<\s*/\s*\1\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal sealed record Wrapped(string Html, IReadOnlyDictionary<string, string> Placeholders);

    public static Wrapped Wrap(string html)
    {
        if (string.IsNullOrEmpty(html))
            return new Wrapped(html ?? string.Empty, new Dictionary<string, string>(StringComparer.Ordinal));

        var placeholders = new Dictionary<string, string>(StringComparer.Ordinal);
        int i = 0;

        var replaced = ScriptStyleRegex.Replace(html, match =>
        {
            var key = $"<!--BDIR_PRESERVE_{i++}-->";
            placeholders[key] = match.Value;
            return key;
        });

        return new Wrapped(replaced, placeholders);
    }

    public static string Unwrap(string html, IReadOnlyDictionary<string, string> placeholders)
    {
        if (string.IsNullOrEmpty(html) || placeholders is null || placeholders.Count == 0)
            return html ?? string.Empty;

        var output = html;
        foreach (var kv in placeholders)
            output = output.Replace(kv.Key, kv.Value, StringComparison.Ordinal);

        return output;
    }
}