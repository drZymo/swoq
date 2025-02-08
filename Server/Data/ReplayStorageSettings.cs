namespace Swoq.Server.Data;

internal class ReplayStorageSettings
{
    private string folder = "";
    public string Folder
    {
        get => folder;
        set
        {
            folder = Environment.ExpandEnvironmentVariables(value);
        }
    }
}
