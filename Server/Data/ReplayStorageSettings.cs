namespace Swoq.Server.Data;

internal class ReplayStorageSettings
{
    private string trainingFolder = "";
    public string TrainingFolder
    {
        get => trainingFolder;
        set
        {
            trainingFolder = Environment.ExpandEnvironmentVariables(value);
        }
    }

    private string questFolder = "";
    public string QuestFolder
    {
        get => questFolder;
        set
        {
            questFolder = Environment.ExpandEnvironmentVariables(value);
        }
    }
}
