namespace Bdir.Convert.Core;

using Bdir.Convert.Core.Extraction;
using Bdir.Convert.Core.Models;

public interface IDocumentConverter
{
    ExtractionResult Convert(
        string source,
        BlockExtractionOptions extractionOptions
    );
}