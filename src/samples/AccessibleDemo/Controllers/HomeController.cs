using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using AccessibleDemo.Models;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace AccessibleDemo.Controllers
{
	public class HomeController : Controller
	{
		private readonly IWebHostEnvironment _environment;

		public HomeController(IWebHostEnvironment environment) => _environment = environment;

		public IActionResult Components() => View();

		public IActionResult Index()
		{
			return View();
		}

		public IActionResult About()
		{
			ViewData["Message"] = "Your application description page.";

			return View();
		}

		public IActionResult Contact()
		{
			ViewData["Message"] = "Your contact page.";

			return View();
		}

		public IActionResult Privacy()
		{
			return View();
		}

		[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
		public IActionResult Error()
		{
			return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
		}

		public async Task<IActionResult> TreeViewExample()
		{
			var viewModel = JsonSerializer.Deserialize<TreeViewViewModel>(
				await System.IO.File.ReadAllTextAsync(Path.Combine(_environment.WebRootPath, "jsonFiles", "wcag21.json")),
				new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

			return await Task.FromResult(View(viewModel));
		}

	}
}
