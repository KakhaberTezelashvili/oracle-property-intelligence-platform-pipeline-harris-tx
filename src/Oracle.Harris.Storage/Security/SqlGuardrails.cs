using System.Text.RegularExpressions;

namespace Oracle.Harris.Storage.Security;

public sealed partial class SqlGuardrails
{
    private static readonly string[] BlockedKeywords =
    [
        "insert", "update", "delete", "drop", "alter", "create", "attach", "copy", "pragma", "install", "load"
    ];

    public void EnsureReadOnlySelect(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new InvalidOperationException("SQL must not be empty.");
        }

        var trimmed = sql.Trim();
        if (!SelectRegex().IsMatch(trimmed))
        {
            throw new InvalidOperationException("Only SELECT / WITH queries are allowed.");
        }

        foreach (var keyword in BlockedKeywords)
        {
            if (ContainsWholeWord(trimmed, keyword))
            {
                throw new InvalidOperationException($"Keyword '{keyword}' is not allowed.");
            }
        }
    }

    public string ApplyRowLimit(string sql, int limit)
    {
        if (limit <= 0 || limit > 500)
        {
            limit = 100;
        }

        if (Regex.IsMatch(sql, @"\blimit\s+\d+", RegexOptions.IgnoreCase))
        {
            return sql;
        }

        return $"{sql.TrimEnd().TrimEnd(';')}\nLIMIT {limit}";
    }

    private static bool ContainsWholeWord(string text, string word) =>
        Regex.IsMatch(text, $@"\b{Regex.Escape(word)}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [GeneratedRegex(@"^\s*(with|select)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SelectRegex();
}