using System.Text;

namespace Oracle.Harris.Ingest.Logging;

public sealed class SourceLimitationLogger
{
    private readonly List<string> _limitations = [];

    public void Add(string source, string limitation) =>
        _limitations.Add($"{source}: {limitation}");

    public string Summarize()
    {
        if (_limitations.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var item in _limitations)
        {
            sb.AppendLine(item);
        }

        return sb.ToString().TrimEnd();
    }

    public IReadOnlyList<string> Entries => _limitations;
}