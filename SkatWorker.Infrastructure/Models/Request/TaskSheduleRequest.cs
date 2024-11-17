using WorkflowCore.Models.Enums;

namespace SkatWorker.Infrastructure.Models.Request
{
    /// <summary>
    /// Класс передачи данных для добавления задачи в расписание.
    /// </summary>
    public class TaskSheduleRequest
    {
        /// <summary>
        /// Идентификатор задачи.
        /// </summary>
        public string WorkflowId { get; set; }
        
        /// <summary>
        /// Версия задачи.
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Дата / время запуска.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Входные параметры задачи.
        /// </summary>
        public string Data { get; set; }

        /// <summary>
        /// Необходимость включения повторного выполнения.
        /// </summary>
        public bool? Retry { get; set; }

        /// <summary>
        /// Интервал запуска задачи.
        /// </summary>
        public Interval Interval { get; set; } 

        /// <summary>
        /// Период запуска задачи.
        /// </summary>
        public Int32 TimePeriod { get; set; }

        /// <summary>
        /// Периодичность запуска задачи
        /// </summary>
        public Periodicity Periodicity { get; set; }

        /// <summary>
        /// Дни недели для запуска.
        /// </summary>
        public string DaysOfWeekSch { get; set; }

        /// <summary>
        /// День месяца запуска задачи.
        /// </summary>
        public string DaysOfMonthSch { get; set; }
    }
}
