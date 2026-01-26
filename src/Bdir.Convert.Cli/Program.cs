using System.Text.Json;
using System.Collections.Generic;
using Bdir.Convert.Core.Extraction;
using Bdir.Convert.Core.Models;
using Bdir.Convert.Core.Patching;
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
            "apply-patch-html" => ApplyPatchHtml(args.Skip(1).ToArray()),
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
        string? editPacketOutPath = null;
        string? anchorHtmlOut = null;
        bool warnOverwriteRisk = true;

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

                case "--edit-packet-out":
                    editPacketOutPath = args[++i];
                    break;

                case "--anchor-html-out":
                    anchorHtmlOut = args[++i];
                    break;

                case "--no-warn-overwrite-risk":
                    warnOverwriteRisk = false;
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
            Console.Out.WriteLine(json);
        else
            File.WriteAllText(outputPath, json);

        // Optional minified Edit Packet output (AI payload)
        if (editPacketOutPath is not null)
        {
            var min = WireJson.SerializeMinified(wire);
            File.WriteAllText(editPacketOutPath, min);
        }

        // Optional anchored HTML output.
        if (anchorHtmlOut is not null)
        {
            var warnings = warnOverwriteRisk ? new List<HtmlAnchorWarning>() : null;
            var anchored = extractor.AnchorHtml(html, options, doc.Blocks, warnings);
            File.WriteAllText(anchorHtmlOut, anchored);

            if (warnOverwriteRisk && warnings is not null)
                EmitOverwriteWarnings(warnings);
        }

        return 0;
    }

    static int ApplyPatchHtml(string[] args)
    {
        if (args.Length < 2)
            return Fail("Usage: bdir-convert apply-patch-html <input.html> <patch.json> [options]");

        var inputPath = args[0];
        var patchPath = args[1];

        string? outputPath = null;
        string? editPacketOutPath = null;
        string? exportHtmlOutPath = null;
        string? anchoredHtmlOutPath = null;

        bool force = false;
        bool warnOverwriteRisk = true;

        // Defaults must match convert-html.
        var options = new BlockExtractionOptions(
            HashAlgorithm: "sha256",
            NormalizeUnicodeNfc: true,
            IncludeBoilerplate: false,
            SplitListItems: true,
            SplitTableRows: false
        );

        // Parse options after positional args.
        for (int i = 2; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "-o":
                case "--out":
                    outputPath = args[++i];
                    break;

                case "--edit-packet-out":
                    editPacketOutPath = args[++i];
                    break;

                case "--export-html-out":
                    exportHtmlOutPath = args[++i];
                    break;

                case "--anchor-html-out":
                    anchoredHtmlOutPath = args[++i];
                    break;

                case "--force":
                    force = true;
                    break;

                case "--no-warn-overwrite-risk":
                    warnOverwriteRisk = false;
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
                    return Fail($"Unexpected argument: {arg}");
            }
        }

        if (!File.Exists(inputPath))
            return Fail($"Input file not found: {inputPath}");
        if (!File.Exists(patchPath))
            return Fail($"Patch file not found: {patchPath}");

        var html = File.ReadAllText(inputPath);
        var patchJson = File.ReadAllText(patchPath);

        WirePatchV1 patch;
        try
        {
            patch = JsonSerializer.Deserialize<WirePatchV1>(patchJson)
                    ?? throw new InvalidOperationException("Invalid patch JSON");
        }
        catch (Exception ex)
        {
            return Fail($"Failed to parse patch.json: {ex.Message}");
        }

        var extractor = new HtmlBlockExtractor();
        var originalDoc = extractor.Extract(html, options);

        // In suggest-only mode (default), we do not mutate anything.
        // We still validate patch binding (v/ha/h) and emit useful warnings.
        if (!force)
        {
            try
            {
                ValidatePatchBinding(originalDoc, patch);
            }
            catch (Exception ex)
            {
                return Fail($"Patch validation failed: {ex.Message}");
            }

            EmitSuggestOnlyDiagnostics(patch);

            // Emit original edit packet (pretty/min) so downstream tools can still operate.
            var wire = WireEditPacketV1.From(originalDoc);
            var pretty = WireJson.SerializeCanonical(wire);

            if (outputPath is null)
                Console.Out.WriteLine(pretty);
            else
                File.WriteAllText(outputPath, pretty);

            if (editPacketOutPath is not null)
            {
                var min = WireJson.SerializeMinified(wire);
                File.WriteAllText(editPacketOutPath, min);
            }

            // Optional HTML outputs are based on the original (unpatched) HTML.
            if (anchoredHtmlOutPath is not null)
            {
                var warnings = warnOverwriteRisk ? new List<HtmlAnchorWarning>() : null;
                var anchored = extractor.AnchorHtml(html, options, originalDoc.Blocks, warnings);
                File.WriteAllText(anchoredHtmlOutPath, anchored);

                if (warnOverwriteRisk && warnings is not null)
                    EmitOverwriteWarnings(warnings);
            }

            if (exportHtmlOutPath is not null)
            {
                // Export without anchors == original HTML; still run through strip to guarantee no anchor attrs.
                var stripped = extractor.StripAnchors(html);
                File.WriteAllText(exportHtmlOutPath, stripped);
            }

            return 0;
        }

        // Force mode: apply patch back to BDIR and (optionally) HTML.
        BdirDocument patchedDoc;
        try
        {
            patchedDoc = PatchApplier.Apply(originalDoc, patch, options.NormalizeUnicodeNfc);
        }
        catch (Exception ex)
        {
            return Fail($"Patch application failed: {ex.Message}");
        }

        // Emit updated Edit Packet (wire form)
        {
            var wire = WireEditPacketV1.From(patchedDoc);
            var pretty = WireJson.SerializeCanonical(wire);

            if (outputPath is null)
                Console.Out.WriteLine(pretty);
            else
                File.WriteAllText(outputPath, pretty);

            if (editPacketOutPath is not null)
            {
                var min = WireJson.SerializeMinified(wire);
                File.WriteAllText(editPacketOutPath, min);
            }
        }

        // Emit HTML outputs (optional)
        if (anchoredHtmlOutPath is not null || exportHtmlOutPath is not null)
        {
            // If export is requested, write stripped HTML. If anchor is requested, keep anchors.
            var wantStrip = exportHtmlOutPath is not null;
            var patchedHtml = HtmlPatchRenderer.ApplyPatchedBlocks(
                originalHtml: html,
                options: options,
                extractor: extractor,
                originalBlocks: originalDoc.Blocks,
                patchedBlocks: patchedDoc.Blocks,
                stripAnchors: wantStrip
            );

            if (anchoredHtmlOutPath is not null)
            {
                var anchoredPatched = HtmlPatchRenderer.ApplyPatchedBlocks(
                    originalHtml: html,
                    options: options,
                    extractor: extractor,
                    originalBlocks: originalDoc.Blocks,
                    patchedBlocks: patchedDoc.Blocks,
                    stripAnchors: false
                );

                // Collect overwrite warnings for anchored output if enabled.
                if (warnOverwriteRisk)
                {
                    var warnings = new List<HtmlAnchorWarning>();
                    _ = extractor.AnchorHtml(anchoredPatched, options, patchedDoc.Blocks, warnings);
                    EmitOverwriteWarnings(warnings);
                }

                File.WriteAllText(anchoredHtmlOutPath, anchoredPatched);
            }

            if (exportHtmlOutPath is not null)
                File.WriteAllText(exportHtmlOutPath, patchedHtml);
        }

        return 0;
    }

    static void ValidatePatchBinding(BdirDocument doc, WirePatchV1 patch)
    {
        if (patch.Version != 1)
            throw new InvalidOperationException($"Unsupported patch version: {patch.Version}");

        var patchHa = string.IsNullOrWhiteSpace(patch.HashAlgorithm) ? "sha256" : patch.HashAlgorithm!;
        if (!string.Equals(patchHa, doc.HashAlgorithm, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"patch hash algorithm mismatch (patch.ha='{patchHa}', doc.hash_algorithm='{doc.HashAlgorithm}')");

        if (!string.Equals(patch.PageHash, doc.PageHash, StringComparison.Ordinal))
            throw new InvalidOperationException("patch page hash mismatch");
    }

    static void EmitSuggestOnlyDiagnostics(WirePatchV1 patch)
    {
        // Always tell the operator what's happening.
        Console.Error.WriteLine("WARN suggest-only: patch will NOT be applied unless --force is provided.");

        foreach (var op in patch.Ops)
        {
            switch (op)
            {
                case WireSuggestOp s:
                    Console.Error.WriteLine($"SUGGEST block_id={s.BlockId}: {s.Message}");
                    break;

                case WireReplaceOp r:
                    Console.Error.WriteLine($"WARN skipped replace block_id={r.BlockId}: run with --force to apply");
                    break;

                case WireDeleteOp d:
                    Console.Error.WriteLine($"WARN skipped delete block_id={d.BlockId}: run with --force to apply");
                    break;

                case WireInsertAfterOp ins:
                    Console.Error.WriteLine($"WARN skipped insert_after block_id={ins.BlockId}: run with --force to apply");
                    break;

                default:
                    Console.Error.WriteLine($"WARN skipped op type={op.GetType().Name}: run with --force to apply");
                    break;
            }
        }
    }

    static void EmitOverwriteWarnings(IEnumerable<HtmlAnchorWarning> warnings)
    {
        foreach (var w in warnings)
        {
            var tags = string.Join(',', w.ChildElementTags.Distinct(StringComparer.Ordinal));
            Console.Error.WriteLine($"WARN anchor-overwrite-risk block_id={w.BlockId} kind={w.KindCode} tag=<{w.TagName}> children=[{tags}]");
        }
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
   apply-patch-html <input.html> <patch.json> [options]
   regen-goldens <fixturesDir>

 convert-html options:
   -o, --out <file>               Write output to file (default: stdout)
   --edit-packet-out <file>       Write a minified Edit Packet JSON (AI payload)
   --anchor-html-out <file>       Write HTML with deterministic BDIR anchors injected
   --no-warn-overwrite-risk       Disable warnings for anchors that contain child elements
   --split-table-rows             Emit one block per <tr>
   --no-split-table-rows          Emit whole table as one block
   --include-boilerplate          Include nav/header/footer content
   --exclude-boilerplate          Exclude boilerplate (default)

 apply-patch-html options:
   -o, --out <file>               Write (updated) Edit Packet JSON (default: stdout)
   --edit-packet-out <file>       Write a minified (updated) Edit Packet JSON
   --export-html-out <file>       Write updated HTML with anchors stripped
   --anchor-html-out <file>       Write HTML with anchors preserved
   --force                        Actually apply the patch (default is suggest-only)
   --no-warn-overwrite-risk       Disable warnings for anchors that contain child elements

 Examples:
   bdir-convert convert-html input.html -o edit-packet.json --edit-packet-out edit-packet.min.json --anchor-html-out anchored.html
   bdir-convert apply-patch-html input.html patch.json -o out.json   # suggest-only (no mutation)
   bdir-convert apply-patch-html input.html patch.json --force -o out.json --export-html-out updated.html
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
