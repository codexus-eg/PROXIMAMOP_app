namespace PROXIMAMOP.Models;

public class FeedItem
{
    public int Id { get; set; }

    public string Text { get; set; } = string.Empty;

    public long Time { get; set; }

    public string LocalTimeText
    {
        get
        {
            try
            {
                var dt = DateTimeOffset.FromUnixTimeSeconds(Time).ToLocalTime();
                return dt.ToString("dd MMM yyyy - hh:mm tt");
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    public string TitleLine => GetTitleLine();
    public string EntryLine => GetLineByKeywords("دخول", "entry");
    public string TargetLine => GetLineByKeywords("هدف", "tp", "target");
    public string StopLine => GetLineByKeywords("ستوب", "sl", "stop");
    public string OtherLines => GetOtherLines();

    private List<string> GetLines()
    {
        return (Text ?? string.Empty)
            .Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    private string GetTitleLine()
    {
        var lines = GetLines();
        return lines.Count > 0 ? lines[0] : string.Empty;
    }

    private string GetLineByKeywords(params string[] keywords)
    {
        var lines = GetLines();

        foreach (var line in lines)
        {
            var lower = line.ToLowerInvariant();

            foreach (var keyword in keywords)
            {
                if (lower.Contains(keyword.ToLowerInvariant()))
                    return line;
            }
        }

        return string.Empty;
    }

    private string GetOtherLines()
    {
        var lines = GetLines();
        var title = TitleLine;
        var entry = EntryLine;
        var target = TargetLine;
        var stop = StopLine;

        var others = lines
            .Where(x => x != title && x != entry && x != target && x != stop)
            .ToList();

        return string.Join(Environment.NewLine, others);
    }
}