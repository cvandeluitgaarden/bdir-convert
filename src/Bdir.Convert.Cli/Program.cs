using System.Text.Json;
using Bdir.Convert.Core.Extraction;
using Bdir.Convert.Core.Wire;
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
            "convert-html" => ConvertHtml(args.Skip(1).ToArray()),
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

    static int ConvertHtml(string[] args)
    {
        if (args.Length == 0)
            return Fail("Usage: bdir-convert convert-html <input.html> [options]");

        string? inputPath = null;
        string? outputPath = null;
        string? anchorHtmlOut = null;

        // Defaults must be explicit
        var options = new BlockExtractionOptions(
            HashAlgorithm: "sha256",
            NormalizeUnicodeNfc: true,
            IncludeBoilerplate: false,
            SplitListItems: true,
            SplitTableRows: false
        );

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg)
            {
                case "-o":
                case "--out":
                    outputPath = args[++i];
                    break;

                case "--anchor-html-out":
                    anchorHtmlOut = args[++i];
                    break;

                case "--split-table-rows":
                    options = options with { SplitTableRows = true };
                    break;

                case "--no-split-table-rows":
                    options = options with { SplitTableRows = false };
                    break;

                case "--include-boilerplate":
                    options = options with { IncludeBoilerplate = true };
                    break;

                case "--exclude-boilerplate":
                    options = options with { IncludeBoilerplate = false };
                    break;

                default:
                    if (arg.StartsWith('-'))
                        return Fail($"Unknown option: {arg}");

                    inputPath ??= arg;
                    break;
            }
        }

        if (inputPath is null)
            return Fail("Missing input.html");

        if (!File.Exists(inputPath))
            return Fail($"Input file not found: {inputPath}");

        var html = File.ReadAllText(inputPath);

        var extractor = new HtmlBlockExtractor();
        var doc = extractor.Extract(html, options);

        if (doc.Blocks.Count == 0)
            return Fail("No blocks extracted (refusing to emit empty BDIR)");

        // RFC-ish wire output
        var wire = WireEditPacketV1.From(doc);
        var json = WireJson.SerializeCanonical(wire);

        if (outputPath is null)
        {
            Console.Out.WriteLine(json);
        }
        else
        {
            File.WriteAllText(outputPath, json);
        }

        // Anchors (future)
        if (anchorHtmlOut is not null)
        {
            // TODO: HtmlAnchorStrategy
            // For now, make this explicit
            return Fail("--anchor-html-out is not implemented yet");
        }

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

    static int PrintHelp()
    {
        Console.WriteLine("""
bdir-convert

Commands:
  convert-html <input.html> [options]
  regen-goldens <fixturesDir>

convert-html options:
  -o, --out <file>               Write output to file (default: stdout)
  --split-table-rows             Emit one block per <tr>
  --no-split-table-rows          Emit whole table as one block
  --include-boilerplate          Include nav/header/footer content
  --exclude-boilerplate          Exclude boilerplate (default)

Example:
  bdir-convert convert-html input.html -o output.bdir.json
""");
        return 0;
    }


    sealed class OptionsDto
    {
        public string? HashAlgorithm { get; set; }
        public bool? NormalizeUnicodeNfc { get; set; }
        public bool? IncludeBoilerplate { get; set; }
        public bool? SplitListItems { get; set; }
        public bool? SplitTableRows { get; set; }
    }
}
