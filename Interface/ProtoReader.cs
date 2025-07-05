using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Swoq.Interface;

public static partial class ProtoReader
{
    public static string ReadProtoFileForLevel(string path, int level)
    {
        // Read full proto file
        var lines = File.ReadAllLines(path);

        // Filer out all lines of too high levels
        var filtered = new List<string>();
        bool hide = false;
        foreach (var line in lines)
        {
            if (!hide)
            {
                // Check if line defines level
                var match = LevelPattern().Match(line);
                if (match.Success)
                {
                    var lineLevel = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);

                    // Level too high, then hide from here
                    if (lineLevel > level) hide = true;
                }
            }
            else if (EndOfBlockPattern().Match(line).Success)
            {
                // Stop hiding at end of a block (i.e. }).
                hide = false;
            }

            if (!hide)
            {
                filtered.Add(line);

                if (filtered.Count > 1)
                {
                    var previousLine = filtered[^2];

                    // Remove empty blocks (i.e. enums)
                    if (previousLine.Length > 0 && previousLine[^1] == '{' && line.Length > 0 && line[0] == '}')
                    {
                        filtered = filtered[..^2];
                    }
                    // Remove second consecutive white line
                    if (string.IsNullOrWhiteSpace(previousLine) && string.IsNullOrWhiteSpace(line))
                    {
                        filtered = filtered[..^1];
                    }
                }
            }
        }

        var sb = new StringBuilder();
        foreach (var line in filtered)
        {
            sb.AppendLine(line);
        }
        return sb.ToString();
    }

    [GeneratedRegex(@"^\s*\}$")]
    private static partial Regex EndOfBlockPattern();

    [GeneratedRegex(@"^\s*// Level (\d+) and higher:$")]
    private static partial Regex LevelPattern();
}
