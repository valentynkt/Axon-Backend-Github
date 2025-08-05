using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;

namespace BuildingBlocks.Web;

public sealed partial class SlugifyParameterTransformer : IOutboundParameterTransformer
{
    [GeneratedRegex("([a-z])([A-Z])", RegexOptions.None, "en-US")]
    private static partial Regex SlugifyRegex();

    public string? TransformOutbound(object? value)
    {
        // Slugify value
        return value == null
            ? null
            : SlugifyRegex().Replace(value.ToString() ?? string.Empty, "$1-$2").ToLower(CultureInfo.CurrentCulture);
    }
}