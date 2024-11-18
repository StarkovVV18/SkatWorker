using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace WorkflowCore.Services.BackgroundTasks
{
    /// <summary>
    /// Опросчик завершения задач.
    /// </summary>
    internal class CompletionTaskSchedule : IBackgroundTask
    {
        private readonly ILogger<CompletionTaskSchedule> _logger;
        private readonly IPersistenceProvider _persistenceProvider;
        private Timer _runnableTaskScheduleTimer;

        public CompletionTaskSchedule(IPersistenceProvider persistenceProvider, ILogger<CompletionTaskSchedule> logger)
        {
            _persistenceProvider = persistenceProvider;
            _logger = logger;
        }

        public void Start()
        {
            _runnableTaskScheduleTimer = new Timer(new TimerCallback(RunTaskSchedule), null, TimeSpan.FromSeconds(0), TimeSpan.FromMinutes(1));
        }

        public void Stop()
        {
            if (_runnableTaskScheduleTimer != null)
            {
                _runnableTaskScheduleTimer.Dispose();
                _runnableTaskScheduleTimer = null;
            }
        }

        /// <summary>
        /// Запустить опрос расписания.
        /// </summary>
        private async void RunTaskSchedule(object target)
        {
            await RunTaskSchedulePoller();
        }

        /// <summary>
        /// Запустить опросчик завершения задач.
        /// </summary>
        /// <returns></returns>
        private async Task RunTaskSchedulePoller()
        {
            var taskSchedules = await _persistenceProvider.GetTaskSchedules(x => x.IsProcessed && x.CompleteTime == null);

            if (!taskSchedules.Any())
            {
                _logger.LogInformation("Task for complete today or earlier not found");
                return;
            }

            foreach (var task in taskSchedules)
            {
                _logger.LogInformation($"Try mark task {task.Id} as completed");

                try
                {
                    WorkflowInstance wfInstance = await _persistenceProvider.GetWorkflowInstance(task.InstanceId);

                    if (wfInstance == null)
                        continue;

                    if (wfInstance.CompleteTime != null && (task.CompleteTime == null && task.IsProcessed))
                    {
                        await _persistenceProvider.MarkTaskScheduleCompleted(task.Id, wfInstance.CompleteTime.Value);
                        _logger.LogInformation($"Task {task.Id} mark as completed with workflow {wfInstance.Id}");
                    }

                    if (wfInstance.CompleteTime == null && wfInstance.Status == WorkflowStatus.Complete)
                    {
                        await _persistenceProvider.MarkTaskScheduleCompleted(task.Id, DateTime.Now);
                        _logger.LogInformation($"Workflow {wfInstance.Id} is not completed but status is Completed, so task {task.Id} mark as completed.");
                    }

                    continue;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Workflow {task.WorkflowId} failed to mark completed. Exception message {ex.Message}");
                    await _persistenceProvider.MarkTaskScheduleUnCompleted(task.Id);

                    continue;
                }
            }
        }
    }
}
