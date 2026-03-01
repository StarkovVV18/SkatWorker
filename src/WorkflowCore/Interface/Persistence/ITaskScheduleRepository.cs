using System.Threading.Tasks;
using System.Threading;
using WorkflowCore.Models;
using System.Collections.Generic;
using System.Linq.Expressions;
using System;

namespace WorkflowCore.Interface.Persistence
{
    public interface ITaskScheduleRepository
    {
        Task<TaskSchedule> CreateTaskSchedule(TaskSchedule taskSchedule, CancellationToken cancellationToken = default);

        Task<bool> TerminateTaskSchedule(string id, CancellationToken cancellationToken = default);

        Task<TaskSchedule> GetTaskSchedule (string id, CancellationToken cancellationToken = default);

        Task<IEnumerable<TaskSchedule>> GetTaskSchedules(CancellationToken cancellationToken = default);

        Task<IEnumerable<TaskSchedule>> GetTaskSchedules(Func<TaskSchedule, bool> expression, CancellationToken cancellationToken = default);

        Task MarkTaskScheduleProcessed(string id, string instanceId, CancellationToken cancellationToken = default);

        Task MarkTaskScheduleUnprocessed(string id, CancellationToken cancellationToken = default);

        Task MarkTaskScheduleCompleted(string id, DateTime completeTime, DateTime nextExecuted, CancellationToken cancellationToken = default);

        Task MarkTaskScheduleUnCompleted(string id, CancellationToken cancellationToken = default);
    }
}
