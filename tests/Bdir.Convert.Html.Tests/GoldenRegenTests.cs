using Bdir.Convert.Core.Wire;
using Bdir.Convert.Html.Tests.TestSupport;

namespace Bdir.Convert.Html.Tests;

public sealed class GoldenRegenTests
{
    [Fact]
    [Trait("Category", "Regen")]
    public void Regenerate_all_golden_wire_fixtures()
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

            var html = File.ReadAllText(inputPath);
            var options = OptionsLoader.LoadOptions(optionsPath);

            var doc = extractor.Extract(html, options);
            Assert.NotEmpty(doc.Blocks);

            var wire = WireEditPacketV1.From(doc);
            var json = GoldenTestHelpers.SerializeCanonical(wire);

            File.WriteAllText(expectedPath, json);
        }
    }
}
