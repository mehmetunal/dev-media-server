﻿using Microsoft.AspNetCore.Mvc;

namespace Devfreco.MediaServer.Controllers
{
    public class DevBaseController : Controller
    {
        // GET
        public IActionResult Index()
        {
            return View();
        }
    }
}