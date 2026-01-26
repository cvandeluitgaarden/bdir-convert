using System.Collections.Generic;

namespace Bdir.Convert.Html;

/// <summary>
/// Non-fatal warning emitted when an anchored element contains child elements.
/// Applying text via element.TextContent would clobber inline markup (e.g. <a>, <em>).
/// </summary>
public sealed record HtmlAnchorWarning(
    string BlockId,
    int KindCode,
    string TagName,
    IReadOnlyList<string> ChildElementTags,
    string Message
);
