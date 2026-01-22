using Bdir.Convert.Core.Wire;
using Bdir.Convert.Html.Tests.TestSupport;

namespace Bdir.Convert.Html.Tests;

public sealed class GoldenDeterminismTests
{
    [Fact]
    [Trait("Category", "Golden")]
    public void HtmlExtractor_matches_golden_wire_fixtures()
    {
        var projectRoot = GoldenTestHelpers.FindProjectDirectory("Bdir.Convert.Html.Tests.csproj");
        var fixtureRoot = Path.Combine(projectRoot, "Fixtures");

        Assert.True(Directory.Exists(fixtureRoot), $"Fixtures directory not found: {fixtureRoot}");

        var extractor = new HtmlBlockExtractor();

        foreach (var dir in Directory.EnumerateDirectories(fixtureRoot).OrderBy(d => d, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(dir);

            var inputPath = Path.Combine(dir, "input.html");
            var optionsPath = Path.Combine(dir, "options.json");
            var expectedPath = Path.Combine(dir, "expected.bdir.json");

            Assert.True(File.Exists(inputPath), $"Fixture {name} is missing input.html");
            Assert.True(File.Exists(optionsPath), $"Fixture {name} is missing options.json");
            Assert.True(File.Exists(expectedPath), $"Fixture {name} is missing expected.bdir.json (run regen)");

            var html = File.ReadAllText(inputPath);
            var options = OptionsLoader.LoadOptions(optionsPath);

            var doc = extractor.Extract(html, options);
            Assert.NotEmpty(doc.Blocks);

            // Enforce RFC-ish truncation semantics: at least 8 hex chars
            Assert.All(doc.Blocks, b => Assert.True(b.TextHash.Length >= 8, $"Fixture {name}: text_hash too short (<8)"));

            var wire = WireEditPacketV1.From(doc);
            var actualJson = GoldenTestHelpers.SerializeCanonical(wire);

            var expectedJson = File.ReadAllText(expectedPath);
            JsonAssert.AssertJsonEqual(expectedJson, actualJson, $"fixture:{name}");
        }
    }
}
