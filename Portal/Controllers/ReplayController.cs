using Microsoft.AspNetCore.Mvc;

namespace Swoq.Portal.Controllers;

[ApiController]
[Route("api/replays")]
public class ReplayController(IConfiguration config) : ControllerBase
{
    private readonly string _folderPath = config["ReplayStorage:Folder"] ?? "";

    [HttpGet("{gameId}")]
    public IActionResult GetReplay(string gameId)
    {
        if (string.IsNullOrEmpty(_folderPath) || !Directory.Exists(_folderPath)) return NotFound();

        var filePath = Directory.GetFiles(_folderPath, $"*{gameId}*.swoq").FirstOrDefault();
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath)) return NotFound();

        // Prevent downloading recently updated files,
        // they might be still in use.
        var info = new FileInfo(filePath);
        if (DateTime.UtcNow - info.LastWriteTimeUtc < TimeSpan.FromSeconds(10)) return NotFound();

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return File(stream, "application/octet-stream", info.Name);
    }
}
