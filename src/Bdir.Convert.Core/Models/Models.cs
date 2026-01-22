namespace Bdir.Convert.Core.Models;

public sealed record BdirBlock (
    string BlockId,
    int KindCode,
    string Text,
    string TextHash
);

public sealed record BdirDocument (
    int Version,
    string HashAlgorithm,
    string PageHash,
    IReadOnlyList<BdirBlock> Blocks
);

public sealed record ExtractionResult (
    BdirDocument Document,
    AnchoredSource AnchoredSource
);

public sealed record AnchoredSource (
    string OriginalSource,
    string Content
);
