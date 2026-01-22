using System.Text;
using Xunit;
using Xunit.Sdk;
using Bdir.Convert.Html;
using Bdir.Convert.Core.Wire;
using Bdir.Convert.Html.Tests.TestSupport;

public sealed class GoldenDeterminismTests
{
    [Fact]
    [Trait("Category", "Golden")]
    public void HtmlExtractor_matches_golden_wire_fixtures()
    {
        const string OK = "✓";
        const string FAIL = "✗";
        const string SKIP = "⚠";

        var projectRoot = GoldenTestHelpers.FindProjectDirectory("Bdir.Convert.Html.Tests.csproj");
        var fixtureRoot = Path.Combine(projectRoot, "Fixtures");

        Assert.True(Directory.Exists(fixtureRoot), $"Fixtures directory not found: {fixtureRoot}");

        var extractor = new HtmlBlockExtractor();
        var failures = new List<string>();
        var skipped = 0;
        var ok = 0;
        var total = 0;

        var debugBlocks =
            string.Equals(Environment.GetEnvironmentVariable("BDIR_GOLDEN_DEBUG"), "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Environment.GetEnvironmentVariable("BDIR_GOLDEN_DEBUG"), "true", StringComparison.OrdinalIgnoreCase);

        foreach (var dir in Directory.EnumerateDirectories(fixtureRoot).OrderBy(d => d, StringComparer.Ordinal))
        {
            total++;
            var name = Path.GetFileName(dir);

            // Skip mechanism: if a fixture contains a file named "SKIP", we skip it.
            var skipFile = Path.Combine(dir, "SKIP");
            if (File.Exists(skipFile))
            {
                var reason = SafeReadFirstLine(skipFile) ?? "marked as skipped";
                Console.WriteLine($"[golden] {ConsoleStyle.YellowText(SKIP)} fixture={name} skipped ({reason})");
                skipped++;
                continue;
            }

            try
            {
                var inputPath = Path.Combine(dir, "input.html");
                var optionsPath = Path.Combine(dir, "options.json");
                var expectedPath = Path.Combine(dir, "expected.bdir.json");

                if (!File.Exists(inputPath))
                    throw new XunitException($"Fixture {name} is missing input.html");

                if (!File.Exists(optionsPath))
                    throw new XunitException($"Fixture {name} is missing options.json");

                if (!File.Exists(expectedPath))
                    throw new XunitException($"Fixture {name} is missing expected.bdir.json (run regen)");

                var html = File.ReadAllText(inputPath);
                var options = OptionsLoader.LoadOptions(optionsPath);

                var doc = extractor.Extract(html, options);

                // Always print a per-fixture header line for CI readability
                Console.WriteLine($"[golden] fixture={name} blocks={doc.Blocks.Count} page_hash={doc.PageHash}");

                if (doc.Blocks.Count == 0)
                    throw new XunitException($"Fixture {name} produced zero blocks");

                // Enforce RFC-ish truncation semantics: at least 8 hex chars
                foreach (var b in doc.Blocks)
                {
                    if (b.TextHash.Length < 8)
                        throw new XunitException($"Fixture {name}: text_hash too short (<8) for block {b.BlockId}");
                }

                if (debugBlocks)
                {
                    Console.WriteLine(ConsoleStyle.DimText($"[golden] debug fixture={name} blocks:"));
                    foreach (var b in doc.Blocks)
                    {
                        // dim gray per-block output
                        Console.WriteLine(ConsoleStyle.DimText($"  - {b.BlockId} kind={b.KindCode} text_hash={b.TextHash} text=\"{b.Text}\""));
                    }
                }

                var wire = WireEditPacketV1.From(doc);
                var actualJson = WireJson.SerializeCanonical(wire);
                var expectedJson = File.ReadAllText(expectedPath);

                // JsonAssert writes diff temp files on mismatch
                JsonAssert.AssertJsonEqual(expectedJson, actualJson, $"fixture:{name}");

                Console.WriteLine($"[golden] {ConsoleStyle.GreenText(OK)} fixture={name}");
                ok++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[golden] {ConsoleStyle.RedText(FAIL)} fixture={name}");
                Console.WriteLine(ex.Message);

                failures.Add($"Fixture '{name}': {ex.Message}");
            }
        }

        // Summary line (always printed)
        Console.WriteLine($"[golden] summary ok={ok} failed={failures.Count} skipped={skipped} total={total}");

        // Fail once at the end if anything failed
        if (failures.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Golden determinism failures:");
            sb.AppendLine();

            foreach (var f in failures)
            {
                sb.AppendLine("- " + f);
                sb.AppendLine();
            }

            throw new XunitException(sb.ToString());
        }
    }

    private static string? SafeReadFirstLine(string path)
    {
        try
        {
            var text = File.ReadAllText(path).Trim();
            if (string.IsNullOrEmpty(text)) return null;

            var idx = text.IndexOfAny(new[] { '\r', '\n' });
            return idx >= 0 ? text[..idx].Trim() : text;
        }
        catch
        {
            return null;
        }
    }
}
