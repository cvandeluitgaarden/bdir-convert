using System.Text.Json;
using Bdir.Convert.Core.Extraction;

namespace Bdir.Convert.Html.Tests.TestSupport;

internal static class OptionsLoader
{
    internal static BlockExtractionOptions LoadOptions(string path)
    {
        var json = File.ReadAllText(path);
        var dto = JsonSerializer.Deserialize<OptionsDto>(json, GoldenTestHelpers.JsonOpts)
                  ?? throw new InvalidOperationException($"Invalid options.json: {path}");

        return new BlockExtractionOptions(
            HashAlgorithm: dto.HashAlgorithm ?? "sha256",
            NormalizeUnicodeNfc: dto.NormalizeUnicodeNfc ?? true,
            IncludeBoilerplate: dto.IncludeBoilerplate ?? false,
            SplitListItems: dto.SplitListItems ?? true,
            SplitTableRows: dto.SplitTableRows ?? false
        );
    }

    private sealed class OptionsDto
    {
        public string? HashAlgorithm { get; set; }
        public bool? NormalizeUnicodeNfc { get; set; }
        public bool? IncludeBoilerplate { get; set; }
        public bool? SplitListItems { get; set; }
        public bool? SplitTableRows { get; set; }
    }
}
