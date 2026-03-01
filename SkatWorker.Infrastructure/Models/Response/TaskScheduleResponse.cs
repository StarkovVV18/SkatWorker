using WorkflowCore.Models;

namespace SkatWorker.Infrastructure.Models.Response
{
    /// <summary>
    /// Запись расписания задачи.
    /// </summary>
    public class TaskScheduleResponse
    {
        /// <summary>
        /// Идентификатор записи расписания.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Идентификатор задачи.
        /// </summary>
        public string WorkflowId { get; set; }

        /// <summary>
        /// Идентификатор запущенной задачи.
        /// </summary>
        public string InstanceId { get; set; }

        /// <summary>
        /// Версия задачи.
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Запланированное время запуска.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Дата завершения.
        /// </summary>
        public DateTime? CompleteTime { get; set; }

        /// <summary>
        /// Входные параметры задачи.
        /// </summary>
        public object Data { get; set; }

        /// <summary>
        /// Статус обозначающий, что задач в процессе выполнения.
        /// </summary>
        public bool IsProcessed { get; set; }

        /// <summary>
        /// Результат выполнения.
        /// </summary>
        public string Result { get; set; }

        /// <summary>
        /// Состояние записи расписания.
        /// </summary>
        public ScheduleStatus Status { get; set; }
    }
}
