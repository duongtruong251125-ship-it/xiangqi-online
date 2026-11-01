using Microsoft.AspNetCore.Mvc;

namespace XiangqiOnline.API.Controllers;

public sealed class AccountController : Controller
{
    [HttpGet]
    public IActionResult Login() => View();

    [HttpGet]
    public IActionResult Register() => View();
}
