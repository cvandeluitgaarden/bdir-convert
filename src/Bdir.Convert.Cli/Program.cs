using System.Text.Json;
using Bdir.Convert.Core.Extraction;
using Bdir.Convert.Html;

internal class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return 1;
        }

        var command = args[0];

        return command switch
        {
            "regen-goldens" => RegenGoldens([.. args.Skip(1)]),
            _ => Fail($"Unknown command: {command}")
        };
    }

    static int RegenGoldens(string[] args)
    {
        if (args.Length != 1)
            return Fail("Usage: bdir-convert regen-goldens <fixturesDir>");

        var fixturesDir = Path.GetFullPath(args[0]);
        if (!Directory.Exists(fixturesDir))
            return Fail($"Fixtures directory not found: {fixturesDir}");

        var extractor = new HtmlBlockExtractor();

        foreach (var dir in Directory.EnumerateDirectories(fixturesDir).OrderBy(d => d, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(dir);

            var inputPath = Path.Combine(dir, "input.html");
            var optionsPath = Path.Combine(dir, "options.json");
            var expectedPath = Path.Combine(dir, "expected.bdir.json");

            if (!File.Exists(inputPath))
                return Fail($"Missing input.html in fixture: {name}");
            if (!File.Exists(optionsPath))
                return Fail($"Missing options.json in fixture: {name}");

            var html = File.ReadAllText(inputPath);
            var options = LoadOptions(optionsPath);

            var doc = extractor.Extract(html, options);
            if (doc.Blocks.Count == 0)
                return Fail($"Fixture '{name}' produced zero blocks; refusing to write expected.bdir.json");

            var json = SerializeCanonical(doc);
            File.WriteAllText(expectedPath, json);

            Console.WriteLine($"Regenerated: {Path.GetRelativePath(fixturesDir, expectedPath)}");
        }

        Console.WriteLine("Done.");
        return 0;
    }

    static BlockExtractionOptions LoadOptions(string path)
    {
        var json = File.ReadAllText(path);
        var dto = JsonSerializer.Deserialize<OptionsDto>(json, JsonOpts)
                  ?? throw new InvalidOperationException($"Invalid options.json: {path}");

        return new BlockExtractionOptions(
            HashAlgorithm: dto.HashAlgorithm ?? "sha256",
            NormalizeUnicodeNfc: dto.NormalizeUnicodeNfc ?? true,
            IncludeBoilerplate: dto.IncludeBoilerplate ?? false,
            SplitListItems: dto.SplitListItems ?? true,
            SplitTableRows: dto.SplitTableRows ?? false
        );
    }

    static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    static string SerializeCanonical<T>(T value)
        => NormalizeJson(JsonSerializer.Serialize(value, JsonOpts));

    static string NormalizeJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(doc.RootElement, JsonOpts);
    }

    static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }

    static void PrintHelp()
    {
        Console.WriteLine("""
bdir-convert

Commands:
  regen-goldens <fixturesDir>   Regenerate expected.bdir.json for all fixtures in <fixturesDir>

Example:
  dotnet run --project src/Bdir.Convert.Cli -- regen-goldens tests/Bdir.Convert.Html.Tests/Fixtures
""");
    }
}


sealed class OptionsDto
{
    public string? HashAlgorithm { get; set; }
    public bool? NormalizeUnicodeNfc { get; set; }
    public bool? IncludeBoilerplate { get; set; }
    public bool? SplitListItems { get; set; }
    public bool? SplitTableRows { get; set; }
}
