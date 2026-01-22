namespace Bdir.Convert.Core.Extraction;

using Bdir.Convert.Core.Models;

public interface IBlockExtractor
{
    /// <summary>
    /// Extracts a canonical BDIR document from source content.
    /// Must be deterministic for identical input.
    /// </summary>
    /// <param name="source">
    /// Raw source content (e.g. HTML, Markdown).
    /// </param>
    /// <param name="options">
    /// Extraction options influencing block segmentation,
    /// kind_code assignment, and normalization.
    /// </param>
    /// <returns>
    /// A fully canonical BDIR document.
    /// </returns>
    BdirDocument Extract(
        string source,
        BlockExtractionOptions options
    );
}

public sealed record BlockExtractionOptions (
    string HashAlgorithm = "sha256",
    bool NormalizeUnicodeNfc = true,
    bool IncludeBoilerplate = false,
    bool SplitListItems = true,
    bool SplitTableRows = true
);
