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
            _runnableTaskScheduleTimer = new Timer(new TimerCallback(RunTaskSchedule), null, TimeSpan.FromSeconds(0), TimeSpan.FromMinutes(5));
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
            var taskSchedules = await _persistenceProvider.GetTaskSchedules(x => x.IsProcessed.GetValueOrDefault() && x.CompleteTime == null);

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

                    var nextExecuted = this.GetNextExecutedTime(task);

                    if (wfInstance.CompleteTime != null && (task.CompleteTime == null && task.IsProcessed.GetValueOrDefault()))
                    {
                        await _persistenceProvider.MarkTaskScheduleCompleted(task.Id, wfInstance.CompleteTime.Value, nextExecuted.GetValueOrDefault());
                        _logger.LogInformation($"Task {task.Id} mark as completed with workflow {wfInstance.Id}");
                    }

                    if (wfInstance.CompleteTime == null && wfInstance.Status == WorkflowStatus.Complete)
                    {
                        await _persistenceProvider.MarkTaskScheduleCompleted(task.Id, DateTime.Now, nextExecuted.GetValueOrDefault());
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

        private DateTime? GetNextExecutedTime(TaskSchedule taskSchedule)
        {
            if (!taskSchedule.Retry.GetValueOrDefault())
                return null;

            var currentDate = DateTime.Now;
            bool isDuringDay = taskSchedule.Interval == Models.Enums.Interval.DuringDay;

            // Каждый день.
            if (taskSchedule.Periodicity == Models.Enums.Periodicity.Everyday && taskSchedule.StartTime <= currentDate)
            {
                if (isDuringDay)
                    return this.GetNextExecutedOnDuringDay(taskSchedule);

                return currentDate.AddDays(1);
            }

            // Еженедельно.
            if (taskSchedule.Periodicity == Models.Enums.Periodicity.Weekly)
            {
                var currentDayOfWeek = currentDate.DayOfWeek;
                var planedDaysOfWeek = taskSchedule.DaysOfWeekSch.Split(',');

                if (!planedDaysOfWeek.Contains(currentDayOfWeek.ToString()))
                    return taskSchedule.CompleteTime;

                if (isDuringDay)
                    return this.GetNextExecutedOnDuringDay(taskSchedule);

                DateTime lastExecuted = taskSchedule.LastExecuted.GetValueOrDefault();
                int intLastExecutedDayOfWeek = (int)lastExecuted.DayOfWeek;
                int intTodayDayOfWeek = (int)currentDayOfWeek;
                DateTime nextExecuted = currentDate.AddDays(intLastExecutedDayOfWeek - intTodayDayOfWeek);

                return nextExecuted;
            }

            // По числам.
            if (taskSchedule.Periodicity == Models.Enums.Periodicity.DaysOfMonth)
            {
                var currentDay = currentDate.Day;
                var daysOfMonth = taskSchedule.DaysOfMonthSch.Split(',');

                if (!daysOfMonth.Contains(currentDay.ToString()))
                    return taskSchedule.CompleteTime;

                if (isDuringDay)
                    return this.GetNextExecutedOnDuringDay(taskSchedule);

                string nextDaysOfMonth = daysOfMonth.SkipWhile(x => !x.Equals(currentDay.ToString())).Skip(1).DefaultIfEmpty(daysOfMonth[0]).FirstOrDefault();

                DateTime firstDayOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                DateTime endDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

                DateTime? nextDay = null;

                if (!int.Parse(nextDaysOfMonth).Equals(endDayOfMonth.Day) && int.Parse(nextDaysOfMonth) != 1)
                    nextDay = new DateTime(currentDate.Year, currentDate.Month, int.Parse(nextDaysOfMonth));
                else
                    nextDay = new DateTime(currentDate.Year, currentDate.AddMonths(1).Month, int.Parse(nextDaysOfMonth));

                return nextDay;
            }

            return null;
        }

        /// <summary>
        /// Получить следующее время выполнения в течение дня.
        /// </summary>
        /// <param name="taskSchedule">Расписание задачи.</param>
        /// <returns>Следующая дата запуска.</returns>
        private DateTime? GetNextExecutedOnDuringDay(TaskSchedule taskSchedule)
        {
            if (taskSchedule.Interval != Models.Enums.Interval.DuringDay)
                return null;

            var currentDate = DateTime.Now;
            var different = currentDate.Subtract(taskSchedule.LastExecuted.GetValueOrDefault());

            // Каждый день.
            if (taskSchedule.Periodicity == Models.Enums.Periodicity.Everyday && taskSchedule.StartTime <= currentDate)
            {
                if (taskSchedule.LastExecuted == null && taskSchedule.NextExecuted == null)
                    return taskSchedule.StartAt;

                if (different.Minutes >= taskSchedule.TimePeriod && taskSchedule.CompleteTime.HasValue)
                    return taskSchedule.LastExecuted.GetValueOrDefault().AddMinutes(taskSchedule.TimePeriod.GetValueOrDefault());

            }

            // Еженедельно.
            if (taskSchedule.Periodicity == Models.Enums.Periodicity.Weekly)
            {
                var currentDayOfWeek = currentDate.DayOfWeek;
                var planedDaysOfWeek = taskSchedule.DaysOfWeekSch.Split(',');

                if (!planedDaysOfWeek.Contains(currentDayOfWeek.ToString()))
                    return taskSchedule.CompleteTime;

                if (taskSchedule.LastExecuted == null && taskSchedule.NextExecuted == null)
                    return taskSchedule.StartAt;

                if (different.Minutes >= taskSchedule.TimePeriod && taskSchedule.CompleteTime.HasValue)
                {
                    return taskSchedule.LastExecuted.GetValueOrDefault().AddMinutes(taskSchedule.TimePeriod.GetValueOrDefault());

                    // TODO: Переделать получение даты на текущий день, чтобы след. запуск не выходил на следующий день.
                    //int intLastExecutedDayOfWeek = (int)taskSchedule.LastExecuted.GetValueOrDefault().DayOfWeek;
                    //int intTodayDayOfWeek = (int)currentDayOfWeek;
                    //DateTime nextExecuted = currentDate.AddDays(intLastExecutedDayOfWeek - intTodayDayOfWeek);

                    //return nextExecuted;
                }
            }

            // По числам.
            if (taskSchedule.Periodicity == Models.Enums.Periodicity.DaysOfMonth)
            {
                var currentDay = currentDate.Day;
                var daysOfMonth = taskSchedule.DaysOfMonthSch.Split(',');

                if (!daysOfMonth.Contains(currentDay.ToString()))
                    return taskSchedule.CompleteTime;

                if (taskSchedule.LastExecuted == null && taskSchedule.NextExecuted == null)
                    return taskSchedule.StartAt;

                if (different.Minutes >= taskSchedule.TimePeriod && taskSchedule.CompleteTime.HasValue)
                {
                    return taskSchedule.LastExecuted.GetValueOrDefault().AddMinutes(taskSchedule.TimePeriod.GetValueOrDefault());

                    // TODO: Переделать получение даты на текущий день, чтобы след. запуск не выходил на следующий день.
                    //string nextDaysOfMonth = daysOfMonth.SkipWhile(x => !x.Equals(currentDay.ToString())).Skip(1).DefaultIfEmpty(daysOfMonth[0]).FirstOrDefault();

                    //DateTime firstDayOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                    //DateTime endDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

                    //DateTime? nextDay = null;

                    //if (!int.Parse(nextDaysOfMonth).Equals(endDayOfMonth.Day) && int.Parse(nextDaysOfMonth) != 1)
                    //    nextDay = new DateTime(currentDate.Year, currentDate.Month, int.Parse(nextDaysOfMonth));
                    //else
                    //    nextDay = new DateTime(currentDate.Year, currentDate.AddMonths(1).Month, int.Parse(nextDaysOfMonth));

                    //return nextDay;
                }
            }

            return null;
        }
    }
}
