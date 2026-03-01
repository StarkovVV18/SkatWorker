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
            _runnableTaskScheduleTimer = new Timer(new TimerCallback(RunMonitoring), null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(10));
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
            var taskSchedules = await _persistenceProvider.GetTaskSchedules(x => x.Status != ScheduleStatus.Terminated
                && x.StartTime <= DateTime.Now
                && x.CompleteTime == null
                && !x.IsProcessed.GetValueOrDefault()
                && !x.Retry.GetValueOrDefault());

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
            var taskSchedules = await _persistenceProvider.GetTaskSchedules(x => x.Status != ScheduleStatus.Terminated
            && x.Retry.GetValueOrDefault()
            && !x.IsProcessed.GetValueOrDefault());

            if (!taskSchedules.Any())
            {
                _logger.LogInformation("Tasks for retry today or earlier not found");
                return;
            }
            
            var currentDate = DateTime.Now;

            foreach (var task in taskSchedules)
            {
                // Проверка на выполнения задачи на текущий день.
                //if (task.Interval == Models.Enums.Interval.OnceADay)
                //{
                //    if ((!task.NextExecuted.HasValue || !task.LastExecuted.HasValue)
                //        || (task.LastExecuted == currentDate.AddDays(-1) && task.NextExecuted == currentDate))
                //        this.CheckConditionAndStartTask(task);

                //    continue;
                //}

                // Проверка на выполнения задачи в течение дня.
                //if (task.Interval == Models.Enums.Interval.DuringDay)
                //{
                    //if (task.LastExecuted != currentDate && task.NextExecuted == currentDate)
                        this.CheckConditionAndStartTask(task);

                    //continue;
                //}
            }
        }

        /// <summary>
        /// Проверит условия запуска задачи и в случае успеха запустить.
        /// </summary>
        /// <param name="taskSchedule">Расписание запуска задачи.</param>
        private void CheckConditionAndStartTask(TaskSchedule taskSchedule)
        {
            var currentDate = DateTime.Now;

            // Каждый день.
            if (taskSchedule.Periodicity == Models.Enums.Periodicity.Everyday
                && taskSchedule.StartTime <= currentDate)
            {
                if (taskSchedule.Interval == Models.Enums.Interval.DuringDay)
                {
                    var taskPeriod = taskSchedule.TimePeriod;
                    var lastExecute = taskSchedule.LastExecuted;
                    var nextExecute = taskSchedule.NextExecuted;
                    var different = currentDate.Subtract(lastExecute.GetValueOrDefault());

                    if (lastExecute == null && nextExecute == null)
                    {
                        this.StartWorkflowFromSchedule(taskSchedule);
                        return;
                    }

                    if (different.Minutes >= taskPeriod && taskSchedule.CompleteTime.HasValue)
                    {
                        this.StartWorkflowFromSchedule(taskSchedule);
                        _logger.LogInformation(string.Format("Start task {0} everyday and during day", taskSchedule.Id));
                    }


                    return;
                }

                this.StartWorkflowFromSchedule(taskSchedule);
                return;
            }

            // Еженедельно.
            if (taskSchedule.Periodicity == Models.Enums.Periodicity.Weekly)
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

                var currentDayOfWeek = (int)currentDate.DayOfWeek;
                var daysOfWeek = taskSchedule.DaysOfWeekSch.Split(',');

                if (!daysOfWeek.Contains(currentDayOfWeek.ToString()))
                    return;

                if (taskSchedule.Interval == Models.Enums.Interval.DuringDay)
                {
                    var taskPeriod = taskSchedule.TimePeriod;
                    var lastExecute = taskSchedule.LastExecuted;
                    var nextExecute = taskSchedule.NextExecuted;
                    var different = currentDate.Subtract(lastExecute.GetValueOrDefault());

                    if (lastExecute == null && nextExecute == null)
                    {
                        this.StartWorkflowFromSchedule(taskSchedule);
                        return;
                    }

                    if (different.Minutes >= taskPeriod && taskSchedule.CompleteTime.HasValue)
                    {
                        this.StartWorkflowFromSchedule(taskSchedule);
                        _logger.LogInformation(string.Format("Start task {0} weekly and during day", taskSchedule.Id));
                    }

                    return;
                }

                if (taskSchedule.Interval == Models.Enums.Interval.OnceADay)
                {
                    if ((!taskSchedule.NextExecuted.HasValue && !taskSchedule.LastExecuted.HasValue)
                        || (currentDate >= taskSchedule.NextExecuted))
                    {
                        this.StartWorkflowFromSchedule(taskSchedule);
                        _logger.LogInformation(string.Format("Start task {0} weekly and once a day", taskSchedule.Id));
                    }    
                }
            }

            // По числам.
            if (taskSchedule.Periodicity == Models.Enums.Periodicity.DaysOfMonth)
            {
                var currentDay = currentDate.Day;
                var daysOfMonth = taskSchedule.DaysOfMonthSch.Split(',');

                if (!daysOfMonth.Contains(currentDay.ToString()))
                    return;

                if (taskSchedule.Interval == Models.Enums.Interval.DuringDay)
                {
                    var taskPeriod = taskSchedule.TimePeriod;
                    var lastExecute = taskSchedule.LastExecuted;
                    var nextExecute = taskSchedule.NextExecuted;
                    var different = currentDate.Subtract(lastExecute.GetValueOrDefault());

                    if (lastExecute == null && nextExecute == null)
                    {
                        this.StartWorkflowFromSchedule(taskSchedule);
                        return;
                    }

                    if (different.Minutes >= taskPeriod && taskSchedule.CompleteTime.HasValue)
                    {
                        this.StartWorkflowFromSchedule(taskSchedule);
                        _logger.LogInformation(string.Format("Start task {0} day of month and during day", taskSchedule.Id));
                    }

                    return;
                }

                if (taskSchedule.Interval == Models.Enums.Interval.OnceADay)
                {
                    if ((!taskSchedule.NextExecuted.HasValue || !taskSchedule.LastExecuted.HasValue)
                        || (currentDate >= taskSchedule.NextExecuted))
                    {
                        this.StartWorkflowFromSchedule(taskSchedule);
                        _logger.LogInformation(string.Format("Start task {0} day of month and once a day", taskSchedule.Id));
                    }
                }
            }
        }
    }
}
