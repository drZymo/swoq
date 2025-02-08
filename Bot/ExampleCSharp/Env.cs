using Newtonsoft.Json;

namespace Bot;

internal static class Env
{
    private record EnvData(string UserId, string Host);

    private static readonly EnvData data;

    static Env()
    {
        data = JsonConvert.DeserializeObject<EnvData>(File.ReadAllText(@"env.json"))
            ?? throw new FileNotFoundException("Env file cannot be opened", "env.json");
    }

    public static string UserId => data.UserId;
    public static string Host => data.Host;
}
