using Microsoft.AspNetCore.Mvc;

namespace Devfreco.MediaServer.Controllers
{
    public class MediaServerController : Controller
    {
        // GET
        public IActionResult Index()
        {
            return View();
        }
    }
}