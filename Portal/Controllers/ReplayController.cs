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

        var fileName = Path.GetFileName(filePath);

        using var stream = System.IO.File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var contents = new byte[stream.Length];
        stream.ReadExactly(contents);

        return File(contents, "application/octet-stream", fileName);
    }
}
