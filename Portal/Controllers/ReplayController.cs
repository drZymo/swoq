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
        if (string.IsNullOrEmpty(_folderPath)) return NotFound();
        if (!Directory.Exists(_folderPath)) return NotFound();

        var filePath = Directory.GetFiles(_folderPath, $"*{gameId}*.swoq").FirstOrDefault();
        if (filePath == null || !System.IO.File.Exists(filePath)) return NotFound();

        var fileName = Path.GetFileName(filePath);

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return File(stream, "application/octet-stream", fileName);
    }
}
