using Microsoft.AspNetCore.Mvc;

namespace XiangqiOnline.API.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index() => View();
}
