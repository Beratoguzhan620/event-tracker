using System.Globalization;
using System.Text;

namespace EventTracker.Data;

/// <summary>
/// Turns text into lower-case ASCII so a search matches regardless of Turkish casing and accents
/// ("İSTANBUL", "istanbul" and "Istanbul" all become "istanbul"). SQLite's own LOWER() only knows ASCII.
/// </summary>
public static class SearchFolding
{
    private static readonly CultureInfo Turkish = new("tr-TR");

    public static string Fold(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var lower = text.Trim().ToLower(Turkish).Replace('ı', 'i');
        var builder = new StringBuilder(lower.Length);
        foreach (var c in lower.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.IsWhiteSpace(c) ? ' ' : c);
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
