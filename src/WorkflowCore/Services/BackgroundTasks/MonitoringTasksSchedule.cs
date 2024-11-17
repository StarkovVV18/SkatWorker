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
    /// Мониторинг запуска задач по расписанию.
    /// </summary>
    internal class MonitoringTasksSchedule : IBackgroundTask
    {
        private readonly ILogger<MonitoringTasksSchedule> _logger;
        private readonly IPersistenceProvider _persistenceProvider;
        private readonly IWorkflowController _workflowController;
        private readonly IWorkflowRegistry _workflowRegistry;
        private Timer _runnableTaskScheduleTimer;
        private static JsonSerializerSettings _serializerSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };

        public MonitoringTasksSchedule(IPersistenceProvider persistenceProvider, IWorkflowController workflowController, ILogger<MonitoringTasksSchedule> logger, IWorkflowRegistry workflowRegistry)
        {
            _persistenceProvider = persistenceProvider;
            _workflowController = workflowController;
            _workflowRegistry = workflowRegistry;
            _logger = logger;
        }

        public void Start()
        {
            _runnableTaskScheduleTimer = new Timer(new TimerCallback(RunMonitoring), null, TimeSpan.FromSeconds(0), TimeSpan.FromMinutes(20));
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
        /// Запустить мониторинг.
        /// </summary>
        private async void RunMonitoring(object target)
        {
            await RunMonitoringTasksSchedule();
        }

        /// <summary>
        /// Запустить мониторинг запуска задач по расписанию.
        /// </summary>
        /// <returns></returns>
        private async Task RunMonitoringTasksSchedule()
        {
            await StartOneTimeTasks();
            await StartTasksInPeriod();
        }

        /// <summary>
        /// Запустить задачу по расписанию.
        /// </summary>
        /// <param name="task">Расписание.</param>
        private async void StartWorkflowFromSchedule(TaskSchedule task)
        {
            _logger.LogInformation($"Try start workflow {task.WorkflowId}");

            try
            {
                var definition = _workflowRegistry.GetDefinition(task.WorkflowId);

                if (definition == null)
                {
                    _logger.LogError($"Workflow {task.WorkflowId} not started. Exception message {ex.Message}");
                    await _persistenceProvider.MarkTaskScheduleUnprocessed(task.Id);

                    return;
                }

                var dataTypeInstance = JsonConvert.DeserializeObject(task.Data, definition.DataType);
                string startedWf = await _workflowController.StartWorkflow(task.WorkflowId, task.Version, task.Data);
                WorkflowInstance wfInstance = await _persistenceProvider.GetWorkflowInstance(startedWf);

                await _persistenceProvider.MarkTaskScheduleProcessed(task.Id, wfInstance.Id);

                return;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Workflow {task.WorkflowId} not started. Exception message {ex.Message}");
                await _persistenceProvider.MarkTaskScheduleUnprocessed(task.Id);

                return;
            }
        }

        /// <summary>
        /// Запустить задачи разового выполнения.
        /// </summary>
        private async Task StartOneTimeTasks()
        {
            var taskSchedules = await _persistenceProvider.GetTaskSchedules(x => x.StartTime <= DateTime.Now && x.CompleteTime == null && !x.IsProcessed);

            if (!taskSchedules.Any())
            {
                _logger.LogInformation("One-time tasks for start today or earlier not found");
                return;
            }

            foreach (var task in taskSchedules)
                this.StartWorkflowFromSchedule(task);
        }

        /// <summary>
        /// Запустить задачи в периоде.
        /// </summary>
        /// <remarks>Запускает задачи, у которых настроено расписание повторения.</remarks>
        private async Task StartTasksInPeriod()
        {
            var taskSchedules = await _persistenceProvider.GetTaskSchedules(x => x.Retry.GetValueOrDefault());

            if (!taskSchedules.Any())
            {
                _logger.LogInformation("Tasks for retry today or earlier not found");
                return;
            }

            var currentDate = DateTime.Now;

            // Задачи для ежедневного запуска.
            var taskOnceADay = taskSchedules.Where(x => x.Interval == Models.Enums.Interval.OnceADay);

            foreach (var task in taskOnceADay)
            {
                // Запускаем задачи по периодичности.

                // Каждый день.
                if (task.Periodicity == Models.Enums.Periodicity.Everyday && task.StartTime <= currentDate)
                {
                    this.StartWorkflowFromSchedule(task);
                    continue;
                }

                // Еженедельно.
                if (task.Periodicity == Models.Enums.Periodicity.Weekly)
                {
                    /*
                    1 - Monday понедельник.
                    2 - Tuesday вторник.
                    3 - Wednesday среду.
                    4 - Thursday четверг.
                    5 - Friday пятницу
                    6 - Saturday субботу
                    0 - Sunday воскресенье.
                    */

                    var currentDayOfWeek = currentDate.DayOfWeek;
                    var daysOfWeek = task.DaysOfWeekSch.Split(',');

                    if (daysOfWeek.Contains(currentDayOfWeek.ToString()))
                        this.StartWorkflowFromSchedule(task);

                    continue;
                }

                // По числам.
                if (task.Periodicity == Models.Enums.Periodicity.DaysOfMonth)
                {
                    var currentDay = currentDate.Day;
                    var daysOfMonth = task.DaysOfMonthSch.Split(',');

                    if (daysOfMonth.Contains(currentDay.ToString()))
                        this.StartWorkflowFromSchedule(task);

                    continue;
                }
            }

            // Задачи для запуска в периоде.
            var taskDuringDay = taskSchedules.Where(x => x.Interval == Models.Enums.Interval.DuringDay);

            foreach (var task in taskDuringDay)
            {
                var taskPeriod = task.TimePeriod;
                var lastExecute = task.LastExecuted;
                var nextExecute = task.NextExecuted;
                var different = currentDate.Subtract(lastExecute.GetValueOrDefault());

                if (lastExecute == null && nextExecute == null)
                {
                    this.StartWorkflowFromSchedule(task);
                    continue;
                }

                if (different.Minutes >= taskPeriod && task.CompleteTime.HasValue)
                    this.StartWorkflowFromSchedule(task);

                continue;
            }
        }
    }
}
