using System;
using System.Collections.Generic;
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
            _runnableTaskScheduleTimer = new Timer(new TimerCallback(RunTaskSchedule), null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(5));
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
            var taskSchedules = await _persistenceProvider.GetTaskSchedules(x => x.IsProcessed.GetValueOrDefault());

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

                    var nextExecuted = this.GetNextExecutedTime(task, wfInstance);

                    if (wfInstance.CompleteTime != null)
                    {
                        await _persistenceProvider.MarkTaskScheduleCompleted(task.Id, wfInstance.CompleteTime.Value.ToLocalTime(), nextExecuted.Value.ToLocalTime());
                        _logger.LogInformation($"Task {task.Id} mark as completed with workflow {wfInstance.Id}");
                    }

                    if (wfInstance.CompleteTime == null && wfInstance.Status == WorkflowStatus.Complete)
                    {
                        await _persistenceProvider.MarkTaskScheduleCompleted(task.Id, DateTime.Now, nextExecuted.Value);
                        _logger.LogInformation($"Workflow {wfInstance.Id} is not completed but status is Completed, so task {task.Id} mark as completed.");
                    }

                    continue;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Workflow {task.WorkflowId} failed to mark completed. Exception message {ex.Message}");
                    _logger.LogError($"Workflow {task.WorkflowId} failed to mark completed. Stack trace {ex.StackTrace}");
                    await _persistenceProvider.MarkTaskScheduleUnCompleted(task.Id);

                    continue;
                }
            }
        }

        private DateTime? GetNextExecutedTime(TaskSchedule taskSchedule, WorkflowInstance workflowInstance)
        {
            if (!taskSchedule.Retry.GetValueOrDefault())
                return null;

            var currentDate = DateTime.Now;
            bool isDuringDay = taskSchedule.Interval == Models.Enums.Interval.DuringDay;

            // Каждый день.
            if (taskSchedule.Periodicity == Models.Enums.Periodicity.Everyday && taskSchedule.StartTime <= currentDate)
            {
                if (isDuringDay)
                    return this.GetNextExecutedOnDuringDay(taskSchedule, workflowInstance);

                return currentDate.AddDays(1);
            }

            // Еженедельно.
            if (taskSchedule.Periodicity == Models.Enums.Periodicity.Weekly)
            {
                var currentDayOfWeek = (int)currentDate.DayOfWeek;
                var planedDaysOfWeek = taskSchedule.DaysOfWeekSch.Split(',').ToList();
                bool currentDayContainsInPlanedDay = planedDaysOfWeek.Contains(currentDayOfWeek.ToString());

                if (!currentDayContainsInPlanedDay && taskSchedule.LastExecuted.HasValue)
                    return taskSchedule.NextExecuted;

                if (isDuringDay)
                    return this.GetNextExecutedOnDuringDay(taskSchedule, workflowInstance);

                DateTime lastExecuted = workflowInstance.CompleteTime.GetValueOrDefault();
                DateTime nextExecuted = this.NextDateTimeByDayOfWeek(lastExecuted, planedDaysOfWeek);

                return nextExecuted;
            }

            // По числам.
            if (taskSchedule.Periodicity == Models.Enums.Periodicity.DaysOfMonth)
            {
                var currentDay = currentDate.Day;
                var daysOfMonth = taskSchedule.DaysOfMonthSch.Split(',');
                bool currentDayContainsInDaysOfMonth = daysOfMonth.Contains(currentDay.ToString());

                if (!currentDayContainsInDaysOfMonth && taskSchedule.LastExecuted.HasValue)
                    return taskSchedule.NextExecuted;

                if (isDuringDay)
                    return this.GetNextExecutedOnDuringDay(taskSchedule, workflowInstance);

                string nextDaysOfMonth = daysOfMonth.SkipWhile(x => !x.Equals(currentDay.ToString())).Skip(1).DefaultIfEmpty(daysOfMonth[0]).FirstOrDefault();

                DateTime firstDayOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                DateTime endDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

                DateTime? nextDay = null;

                if (!int.Parse(nextDaysOfMonth).Equals(endDayOfMonth.Day) && int.Parse(nextDaysOfMonth) != 1)
                    nextDay = new DateTime(currentDate.Year, currentDate.Month, int.Parse(nextDaysOfMonth),
                        workflowInstance.CompleteTime.Value.Hour,
                        workflowInstance.CompleteTime.Value.Minute,
                        workflowInstance.CompleteTime.Value.Second);
                else
                    nextDay = new DateTime(currentDate.Year, currentDate.AddMonths(1).Month, int.Parse(nextDaysOfMonth),
                        workflowInstance.CompleteTime.Value.Hour,
                        workflowInstance.CompleteTime.Value.Minute,
                        workflowInstance.CompleteTime.Value.Second);

                return nextDay;
            }

            return null;
        }

        /// <summary>
        /// Определить следующей день выполнения задания на основе дня недели.
        /// </summary>
        /// <param name="target">Дата, от которой искать следующую дату.</param>
        /// <param name="validDaysOfWeek">Список дней недели</param>
        /// <returns>Следующая дата выполнения.</returns>
        private DateTime NextDateTimeByDayOfWeek(DateTime target, List<string> validDaysOfWeek)
        {
            return Enumerable.Range(1, 7)
                .Select(n => target.AddDays(n))
                .First(date => validDaysOfWeek.Contains(((int)date.DayOfWeek).ToString()));
        }


        /// <summary>
        /// Получить следующее время выполнения в течение дня.
        /// </summary>
        /// <param name="taskSchedule">Расписание задачи.</param>
        /// <returns>Следующая дата запуска.</returns>
        private DateTime? GetNextExecutedOnDuringDay(TaskSchedule taskSchedule, WorkflowInstance workflowInstance)
        {
            if (taskSchedule.Interval != Models.Enums.Interval.DuringDay)
                return null;

            var currentDate = DateTime.Now;
            var different = currentDate.Subtract(taskSchedule.LastExecuted.GetValueOrDefault());

            // Каждый день.
            if (taskSchedule.Periodicity == Models.Enums.Periodicity.Everyday && taskSchedule.StartTime <= currentDate)
            {
                if (taskSchedule.Interval == Models.Enums.Interval.OnceADay)
                    return taskSchedule.LastExecuted.Value.AddDays(1);

                if (taskSchedule.Interval == Models.Enums.Interval.DuringDay)
                    if (different.Minutes >= taskSchedule.TimePeriod && workflowInstance.CompleteTime.HasValue)
                        return workflowInstance.CompleteTime.GetValueOrDefault().ToLocalTime().AddMinutes(taskSchedule.TimePeriod.GetValueOrDefault());

            }

            // Еженедельно.
            if (taskSchedule.Periodicity == Models.Enums.Periodicity.Weekly)
            {
                var currentDayOfWeek = (int)currentDate.DayOfWeek;
                var planedDaysOfWeek = taskSchedule.DaysOfWeekSch.Split(',');

                bool currentDayContainsInPlanedDay = planedDaysOfWeek.Contains(currentDayOfWeek.ToString());

                if (!currentDayContainsInPlanedDay && !taskSchedule.LastExecuted.HasValue)
                    return taskSchedule.NextExecuted;

                if (taskSchedule.LastExecuted.HasValue && taskSchedule.NextExecuted.HasValue)
                {
                    //var differentNextExecuted = currentDate.Subtract(taskSchedule.NextExecuted.GetValueOrDefault());

                    //if (differentNextExecuted.Minutes >= taskSchedule.TimePeriod && workflowInstance.CompleteTime.HasValue)
                        return workflowInstance.CompleteTime.GetValueOrDefault().ToLocalTime().AddMinutes(taskSchedule.TimePeriod.GetValueOrDefault());

                    //return taskSchedule.NextExecuted;
                }

                if (taskSchedule.LastExecuted == null && taskSchedule.NextExecuted == null)
                    //if (different.Minutes >= taskSchedule.TimePeriod && workflowInstance.CompleteTime.HasValue)
                    //{
                        return workflowInstance.CompleteTime.GetValueOrDefault().ToLocalTime().AddMinutes(taskSchedule.TimePeriod.GetValueOrDefault());

                        // TODO: Переделать получение даты на текущий день, чтобы след. запуск не выходил на следующий день.
                    //}
            }

            // По числам.
            if (taskSchedule.Periodicity == Models.Enums.Periodicity.DaysOfMonth)
            {
                var currentDay = currentDate.Day;
                var daysOfMonth = taskSchedule.DaysOfMonthSch.Split(',');
                bool currentDayContainsInDaysOfMonth = daysOfMonth.Contains(currentDay.ToString());

                if (!currentDayContainsInDaysOfMonth && !taskSchedule.LastExecuted.HasValue)
                     return taskSchedule.NextExecuted;

                if (taskSchedule.LastExecuted.HasValue && taskSchedule.NextExecuted.HasValue)
                    return workflowInstance.CompleteTime.GetValueOrDefault().ToLocalTime().AddMinutes(taskSchedule.TimePeriod.GetValueOrDefault());

                if (taskSchedule.LastExecuted == null && taskSchedule.NextExecuted == null)
                    if (different.Minutes >= taskSchedule.TimePeriod && workflowInstance.CompleteTime.HasValue)
                    {
                        return workflowInstance.CompleteTime.GetValueOrDefault().ToLocalTime().AddMinutes(taskSchedule.TimePeriod.GetValueOrDefault());

                        // TODO: Переделать получение даты на текущий день, чтобы след. запуск не выходил на следующий день.
                    }
            }

            return null;
        }
    }
}
