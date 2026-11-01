using Microsoft.AspNetCore.Mvc;

namespace XiangqiOnline.API.Controllers;

public sealed class AiGameController : Controller
{
    public IActionResult Index() => View();
    public IActionResult Play(Guid id) => id == Guid.Empty ? RedirectToAction(nameof(Index)) : View(model: id);
}
