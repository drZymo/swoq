using Google.Protobuf;
using Swoq.Interface;

namespace Bot;

internal class ReplayFile : IDisposable
{
    private readonly FileStream stream;

    public ReplayFile(string replaysFolder, StartRequest request, StartResponse response)
    {
        // Determine file name
        var dateTimeStr = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var folder = Path.GetFullPath(replaysFolder, AppContext.BaseDirectory);
        var filename = Path.Combine(folder, $"{request.UserName} - {dateTimeStr} - {response.GameId}.swoq");
        // Create directory first
        var directory = Path.GetDirectoryName(filename);
        if (directory != null) Directory.CreateDirectory(directory);
        // Create a new file, allow reading
        stream = new FileStream(filename, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 512, FileOptions.WriteThrough);
        // Store start
        request.WriteDelimitedTo(stream);
        response.WriteDelimitedTo(stream);
    }

    public void Dispose()
    {
        stream.Dispose();
    }

    public void Append(ActRequest request, ActResponse response)
    {
        request.WriteDelimitedTo(stream);
        response.WriteDelimitedTo(stream);
    }
}
