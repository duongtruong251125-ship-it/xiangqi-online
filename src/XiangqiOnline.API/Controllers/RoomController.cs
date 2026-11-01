using Microsoft.AspNetCore.Mvc;

namespace XiangqiOnline.API.Controllers;

public sealed class RoomController : Controller
{
    public IActionResult Index(string? id) => View(model: id);
}
