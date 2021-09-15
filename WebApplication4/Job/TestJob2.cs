using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebApplication4.Job
{
    [DisallowConcurrentExecution]
    public class TestJob2 : IJob
    {
        private readonly ILogger<TestJob2> _logger;

        private readonly IServiceProvider _provider;

        private readonly QuartzHostedService _quartzHostedService;

        private int a=0;

        public TestJob2(ILogger<TestJob2> logger, IServiceProvider provider, QuartzHostedService quartzHostedService)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _quartzHostedService = quartzHostedService ?? throw new ArgumentNullException(nameof(quartzHostedService));
        }


        public async Task Execute(IJobExecutionContext context)
        {

            // 可取得自定義的 JobSchedule 資料, 可根據 JobSchedule 提供的內容建立不同 report 資料
            var schedule = context.JobDetail.JobDataMap.Get("Payload") as JobSchedule;
            var jobName = schedule.JobName;

            using (var scope = _provider.CreateScope())
            {
                // 如果要使用到 DI 容器中定義為 Scope 的物件實體時，由於 Job 定義為 singleton
                // 因此無法直接取得 Scope 的實體，此時就需要於 CreateScope 在 scope 中產生該實體
                // ex. var dbContext = scope.ServiceProvider.GetService<AppDbContext>();
            }
            var aaaa = await _quartzHostedService.GetJobSchedules();

            var ip = new List<string>() { "8.8.8.8" };
            var isLife = await _quartzHostedService.PingHost();

            if (isLife)
            {
                _logger.LogError($"WWWWWWWWWWWWWWWWWWWWWWWWWW");
            }
            _logger.LogInformation($"@{DateTime.Now:HH:mm:ss} - job{jobName} - start");
            for (int i = 0; i < 3; i++)
            {

                // 自己定義當 job 要被迫被被中斷時，哪邊適合結束
                // 如果沒有設定，當作業被中斷時，並不會真的中斷，而會整個跑完
                if (context.CancellationToken.IsCancellationRequested)
                {
                    break;
                }

                System.Threading.Thread.Sleep(1000);
                _logger.LogInformation($"@{DateTime.Now:HH:mm:ss} - job{jobName} - working{i}");

            }


            _logger.LogInformation($"@{DateTime.Now:HH:mm:ss} - job{jobName} - done");
        }
    }
}
