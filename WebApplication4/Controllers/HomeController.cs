using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using WebApplication4.Job;
using WebApplication4.Models;
using static WebApplication4.Job.QuartzHostedService;

namespace WebApplication4.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        private readonly QuartzHostedService _quartzHostedService;

        private readonly TestJob _testJob;

        private readonly TestJob2 _testJob2;


        public HomeController(ILogger<HomeController> logger, QuartzHostedService quartzHostedService, TestJob testJob, TestJob2 testJob2)
        {
            _logger = logger;
            _quartzHostedService = quartzHostedService ?? throw new ArgumentNullException(nameof(quartzHostedService));
            _testJob = testJob;
            _testJob2 = testJob2;

        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Privacy()
        {
            var jobs = new List<IJob>();
            jobs.Add(_testJob);
            jobs.Add(_testJob2);

            await _quartzHostedService.ResetTriggerTimeAsync(jobs, ServiceJobType.TransferService);

            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
