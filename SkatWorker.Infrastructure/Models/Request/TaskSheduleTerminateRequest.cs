using WorkflowCore.Models.Enums;
using static WorkflowCore.Models.TaskSchedule;

namespace SkatWorker.Infrastructure.Models.Request
{
    /// <summary>
    /// Класс передачи данных для добавления задачи в расписание.
    /// </summary>
    public class TaskSheduleTerminateRequest
    {
        /// <summary>
        /// Идентификатор записи расписания.
        /// </summary>
        public string ScheduleId { get; set; }
    }
}
