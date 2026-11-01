using Microsoft.AspNetCore.Mvc;

namespace XiangqiOnline.API.Controllers;

public sealed class LobbyController : Controller
{
    public IActionResult Index() => View();
}
