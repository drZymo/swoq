using System.Globalization;

namespace Bot;

internal static class DotEnv
{
    static DotEnv()
    {
        foreach (var line in File.ReadAllLines(".env"))
        {
            // Split off comments
            var commentParts = line.Split('#', StringSplitOptions.TrimEntries);
            var keyValuePart = commentParts.Length > 0 ? commentParts[0] : line;
            if (string.IsNullOrWhiteSpace(keyValuePart)) continue;

            // Split key-value pair
            var keyValuePair = keyValuePart.Split('=', StringSplitOptions.TrimEntries);
            if (keyValuePair.Length != 2) throw new InvalidDataException(".env file is not in the correct format");

            // Set environment variable if not already set
            var key = keyValuePair[0];
            var value = keyValuePair[1];
            if (Environment.GetEnvironmentVariable(key) == null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    public static string Host => Environment.GetEnvironmentVariable("SWOQ_HOST") ?? "";
    public static string UserId => Environment.GetEnvironmentVariable("SWOQ_USER_ID") ?? "";
    public static string UserName => Environment.GetEnvironmentVariable("SWOQ_USER_NAME") ?? "";
    public static int? Level => GetEnvironmentVariableInt("SWOQ_LEVEL");
    public static int? Seed => GetEnvironmentVariableInt("SWOQ_SEED");

    private static int? GetEnvironmentVariableInt(string key)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (value == null) return null;
        return int.TryParse(value, CultureInfo.InvariantCulture, out var level) ? level : null;
    }
}
