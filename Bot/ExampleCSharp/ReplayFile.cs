using Google.Protobuf;
using Swoq.Interface;

namespace Bot;

internal class ReplayFile
{
    private readonly string filename;

    public ReplayFile(string userName, StartRequest request, StartResponse response)
    {
        // Determine file name
        var sanitizedUserName = Uri.EscapeDataString(userName);
        filename = Path.Combine(AppContext.BaseDirectory, "Replays", $"{sanitizedUserName} - {response.GameId}.swoq");
        // Create directory first
        var directory = Path.GetDirectoryName(filename);
        if (directory != null) Directory.CreateDirectory(directory);
        // Create a new file
        using var stream = File.Create(filename);

        // Store header
        var header = new ReplayHeader { UserName = userName, DateTime = DateTime.Now.ToString("s") };
        header.WriteDelimitedTo(stream);

        // Store start
        request.WriteDelimitedTo(stream);
        response.WriteDelimitedTo(stream);
    }

    public void Append(ActionRequest request, ActionResponse response)
    {
        using var stream = File.OpenWrite(filename);
        stream.Seek(0, SeekOrigin.End);

        request.WriteDelimitedTo(stream);
        response.WriteDelimitedTo(stream);
    }
}
