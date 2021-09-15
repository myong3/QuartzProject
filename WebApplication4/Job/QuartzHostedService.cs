using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Spi;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace WebApplication4.Job
{
    public class QuartzHostedService : IHostedService
    {

        private readonly ISchedulerFactory _schedulerFactory;

        private readonly IJobFactory _jobFactory;

        private readonly ILogger<QuartzHostedService> _logger;

        private readonly IEnumerable<JobSchedule> _injectJobSchedules;

        private List<JobSchedule> _allJobSchedules;

        private List<TriggerKey> _allTriggerKeys;

        private readonly IConfiguration _configuration;

        private bool a = true;

        public IScheduler Scheduler { get; set; }

        public CancellationToken CancellationToken { get; private set; }



        public QuartzHostedService(ILogger<QuartzHostedService> logger, ISchedulerFactory schedulerFactory, IJobFactory jobFactory, IEnumerable<JobSchedule> jobSchedules, IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
            _jobFactory = jobFactory ?? throw new ArgumentNullException(nameof(jobFactory));
            _injectJobSchedules = jobSchedules ?? throw new ArgumentNullException(nameof(jobSchedules));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }



        /// <summary>
        /// 啟動排程器
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (Scheduler == null || Scheduler.IsShutdown)
            {
                // 存下 cancellation token 
                CancellationToken = cancellationToken;

                // 先加入在 startup 註冊注入的 Job 工作
                _allJobSchedules = new List<JobSchedule>();
                _allJobSchedules.AddRange(_injectJobSchedules);

                // 再模擬動態加入新 Job 項目 (e.g. 從 DB 來的，針對不同報表能動態決定產出時機)
                _allJobSchedules.Add(new JobSchedule(jobName: "1111", jobType: typeof(TestJob), cronExpression: "0/10 * * * * ?"));
                _allJobSchedules.Add(new JobSchedule(jobName: "2222", jobType: typeof(TestJob2), cronExpression: "0/7 * * * * ?"));

                // 初始排程器 Scheduler
                Scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
                Scheduler.JobFactory = _jobFactory;

                _allTriggerKeys = new List<TriggerKey>();
                // 逐一將工作項目加入排程器中 
                foreach (var jobSchedule in _allJobSchedules)
                {
                    var jobDetail = CreateJobDetail(jobSchedule);
                    var trigger = CreateTrigger(jobSchedule);
                    _allTriggerKeys.Add(trigger.Key);
                    await Scheduler.ScheduleJob(jobDetail, trigger, cancellationToken);
                    jobSchedule.JobStatus = JobStatus.Scheduled;
                }

                // 啟動排程
                await Scheduler.Start(cancellationToken);
            }
        }

        /// <summary>
        /// 停止排程器
        /// </summary>
        /// <returns></returns>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (Scheduler != null && !Scheduler.IsShutdown)
            {
                _logger.LogInformation($"@{DateTime.Now:HH:mm:ss} - Scheduler StopAsync");
                await Scheduler.Shutdown(cancellationToken);
            }
        }

        /// <summary>
        /// 取得所有作業的最新狀態
        /// </summary>
        public async Task<IEnumerable<JobSchedule>> GetJobSchedules()
        {
            if (Scheduler.IsShutdown)
            {
                // 排程器停止時更新各工作狀態為停止
                foreach (var jobSchedule in _allJobSchedules)
                {
                    jobSchedule.JobStatus = JobStatus.Stopped;
                }
            }
            else
            {
                // 取得目前正在執行的 Job 來更新各 Job 狀態
                var executingJobs = await Scheduler.GetCurrentlyExecutingJobs();
                foreach (var jobSchedule in _allJobSchedules)
                {
                    var isRunning = executingJobs.FirstOrDefault(j => j.JobDetail.Key.Name == jobSchedule.JobName) != null;
                    jobSchedule.JobStatus = isRunning ? JobStatus.Running : JobStatus.Scheduled;
                }

            }

            return _allJobSchedules;
        }

        /// <summary>
        /// 手動觸發作業
        /// </summary>
        public async Task TriggerJobAsync(string jobName)
        {
            if (Scheduler != null && !Scheduler.IsShutdown)
            {
                _logger.LogInformation($"@{DateTime.Now:HH:mm:ss} - job{jobName} - TriggerJobAsync");
                await Scheduler.TriggerJob(new JobKey(jobName), CancellationToken);
            }
        }

        /// <summary>
        /// 手動中斷作業
        /// </summary>
        public async Task InterruptJobAsync(string jobName)
        {
            if (Scheduler != null && !Scheduler.IsShutdown)
            {
                var targetExecutingJob = await GetExecutingJob(jobName);
                if (targetExecutingJob != null)
                {
                    _logger.LogInformation($"@{DateTime.Now:HH:mm:ss} - job{jobName} - InterruptJobAsync");
                    await Scheduler.Interrupt(new JobKey(jobName));
                }

            }
        }

        /// <summary>
        /// 重設排程器(取cousl KV)
        /// </summary>
        public async Task ResetTriggerTimeAsync(List<IJob> jobs, ServiceJobType serviceJobType)
        {
            if (Scheduler != null && !Scheduler.IsShutdown)
            {
                _logger.LogInformation($"@{DateTime.Now:HH:mm:ss} - Scheduler StopAsync");

                await Scheduler.Standby();
                await Scheduler.UnscheduleJobs(_allTriggerKeys);

                // 先加入在 startup 註冊注入的 Job 工作
                _allJobSchedules = new List<JobSchedule>();

                foreach (var job in jobs)
                {
                    var jobtype = job.GetType();
                    var jobName = jobtype.Name;

                    var TriggerTimes = _configuration[$"{serviceJobType}Jobs:{jobName}"];

                    foreach (var item in TriggerTimes.Split(';').ToList().Select((value, index) => new { value, index }))
                    {
                        _allJobSchedules.Add(new JobSchedule(jobName: $"{jobName}-{item.index + 1}", jobType: jobtype, cronExpression: item.value));
                    }
                }

                _allTriggerKeys = new List<TriggerKey>();

                // 逐一將工作項目加入排程器中 
                foreach (var jobSchedule in _allJobSchedules)
                {
                    var jobDetail = CreateJobDetail(jobSchedule);
                    var trigger = CreateTrigger(jobSchedule);
                    _allTriggerKeys.Add(trigger.Key);
                    await Scheduler.ScheduleJob(jobDetail, trigger, CancellationToken);
                    jobSchedule.JobStatus = JobStatus.Scheduled;
                }
                // 啟動排程
                await Scheduler.Start(CancellationToken);
            }
        }

        public async Task<bool> PingHost()
        {
            List<string> addressList = string.IsNullOrEmpty(_configuration["ReplyHost:CheckIP"]) ? new List<string>() : _configuration["ReplyHost:CheckIP"].Split(';').ToList();

            bool pingable = false;
            Ping pinger = null;

            try
            {
                foreach (var address in addressList)
                {
                    pinger = new Ping();
                    PingReply reply = await pinger.SendPingAsync(address);
                    pingable = reply.Status == IPStatus.Success;
                    if (pingable)
                        continue;
                }
            }
            catch (PingException ex)
            {
                _logger.LogInformation($"@{DateTime.Now:HH:mm:ss} - PingHost error : Exception{ex.Message}");
            }
            finally
            {
                if (pinger != null)
                {
                    pinger.Dispose();
                }
            }

            return pingable;
        }

        /// <summary>
        /// 取得特定執行中的作業
        /// </summary>
        private async Task<IJobExecutionContext> GetExecutingJob(string jobName)
        {
            if (Scheduler != null)
            {
                var executingJobs = await Scheduler.GetCurrentlyExecutingJobs();
                return executingJobs.FirstOrDefault(j => j.JobDetail.Key.Name == jobName);
            }

            return null;
        }

        /// <summary>
        /// 建立作業細節 (後續會透過 JobFactory 依此資訊從 DI 容器取出 Job 實體)
        /// </summary>
        private IJobDetail CreateJobDetail(JobSchedule jobSchedule)
        {
            var jobType = jobSchedule.JobType;
            var jobDetail = JobBuilder
                .Create(jobType)
                .WithIdentity(jobSchedule.JobName)
                .WithDescription(jobType.Name)
                .Build();

            // 可以在建立 job 時傳入資料給 job 使用
            jobDetail.JobDataMap.Put("Payload", jobSchedule);

            return jobDetail;
        }

        /// <summary>
        /// 產生觸發器
        /// </summary>
        /// <param name="schedule"></param>
        /// <returns></returns>
        private ITrigger CreateTrigger(JobSchedule schedule)
        {
            return TriggerBuilder
                .Create()
                .WithIdentity($"{schedule.JobName}.trigger")
                .WithCronSchedule(schedule.CronExpression)
                .WithDescription(schedule.CronExpression)
                .Build();
        }

        public enum ServiceJobType
        {
            [Description("AccountService")]
            AccountService = 0,
            [Description("EPostService")]
            EPostService = 1,
            [Description("PaymentService")]
            PaymentService = 2,
            [Description("TransferService")]
            TransferService = 3,
        }
    }
}
