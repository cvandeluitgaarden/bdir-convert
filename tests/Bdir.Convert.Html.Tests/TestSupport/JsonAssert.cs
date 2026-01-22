using System.Text;
using System.Text.Json;
using Xunit.Sdk;

namespace Bdir.Convert.Html.Tests.TestSupport;

internal static class JsonAssert
{
    private static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true
    };

    internal static string Normalize(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(doc.RootElement, Pretty);
    }

    internal static void AssertJsonEqual(string expectedJson, string actualJson, string? context = null)
    {
        var expectedNorm = Normalize(expectedJson);
        var actualNorm = Normalize(actualJson);

        if (string.Equals(expectedNorm, actualNorm, StringComparison.Ordinal))
            return;

        var (expectedPath, actualPath) = WriteTempPair(expectedNorm, actualNorm, context);

        var msg = new StringBuilder();
        msg.AppendLine("JSON mismatch.");
        if (!string.IsNullOrWhiteSpace(context))
            msg.AppendLine($"Context: {context}");

        msg.AppendLine("Wrote temp files for diffing:");
        msg.AppendLine($"  Expected: {expectedPath}");
        msg.AppendLine($"  Actual:   {actualPath}");
        msg.AppendLine();
        msg.AppendLine("--- Expected ---");
        msg.AppendLine(expectedNorm);
        msg.AppendLine();
        msg.AppendLine("--- Actual ---");
        msg.AppendLine(actualNorm);

        throw new XunitException(msg.ToString());
    }

    private static (string expectedPath, string actualPath) WriteTempPair(string expected, string actual, string? context)
    {
        var safeContext = string.IsNullOrWhiteSpace(context)
            ? "json"
            : string.Concat(context.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));

        var dir = Path.Combine(Path.GetTempPath(), "bdir-convert-golden-diffs");
        Directory.CreateDirectory(dir);

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");

        var expectedPath = Path.Combine(dir, $"{safeContext}-{stamp}.expected.json");
        var actualPath = Path.Combine(dir, $"{safeContext}-{stamp}.actual.json");

        File.WriteAllText(expectedPath, expected, Encoding.UTF8);
        File.WriteAllText(actualPath, actual, Encoding.UTF8);

        return (expectedPath, actualPath);
    }
}
