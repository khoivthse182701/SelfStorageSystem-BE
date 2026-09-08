using Microsoft.AspNetCore.Mvc;

namespace SelfStorageSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            Status = "Healthy",
            System = "SelfStorageManagementSystem",
            Timestamp = DateTime.UtcNow,
            Environment = "Local"
        });
    }
}
