using Microsoft.AspNetCore.Mvc;

namespace XiangqiOnline.API.Controllers;

public sealed class ProfileController : Controller
{
    public IActionResult Index() => View();
}
