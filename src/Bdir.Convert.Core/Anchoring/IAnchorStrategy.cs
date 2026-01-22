namespace Bdir.Convert.Core.Anchoring;

using Bdir.Convert.Core.Models;

public interface IAnchorStrategy
{
    /// <summary>
    /// Produces an anchored copy of the original source.
    /// Anchors must be stable and reversible.
    /// </summary>
    AnchoredSource Anchor(
        string originalSource,
        IReadOnlyList<BdirBlock> blocks
    );

    /// <summary>
    /// Removes all anchors previously inserted by this strategy.
    /// </summary>
    string StripAnchors(string anchoredSource);
}
