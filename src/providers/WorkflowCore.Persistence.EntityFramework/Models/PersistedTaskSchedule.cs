using System;
using System.ComponentModel.DataAnnotations;
using WorkflowCore.Models;
using WorkflowCore.Models.Enums;
using static WorkflowCore.Models.TaskSchedule;

namespace WorkflowCore.Persistence.EntityFramework.Models
{
    /// <summary>
    /// Расписание запуска задачи.
    /// </summary>
    public class PersistedTaskSchedule
    {
        /// <summary>
        /// Иднетификатор записи.
        /// </summary>
        [Key]
        public string Id { get; set; }

        /// <summary>
        /// Идентификатор задачи.
        /// </summary>
        public string WorkflowId { get; set; }

        /// <summary>
        /// Идентификатор стартованой задачи.
        /// </summary>
        [MaxLength(200)]
        public string InstanceId { get; set; }

        /// <summary>
        /// Версия задачи.
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Дата запуска задачи.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Дата завершения выполнения задачи.
        /// </summary>
        public DateTime? CompleteTime { get; set; }

        /// <summary>
        /// Признак того, что задач выполняется.
        /// </summary>
        public bool IsProcessed { get; set; }

        /// <summary>
        /// Входные данные задачи.
        /// </summary>
        public string Data { get; set; }

        /// <summary>
        /// Результат выполнения.
        /// </summary>
        public string Result { get; set; }

        /// <summary>
        /// Обозначает, необходимость включения повторного выполнения.
        /// </summary>
        public bool? Retry { get; set; }

        /// <summary>
        /// Дата последнего выполнения задачи.
        /// </summary>
        public DateTime? LastExecuted { get; set; }

        /// <summary>
        /// Дата следующего выполнения задачи.
        /// </summary>
        public DateTime? NextExecuted { get; set; }

        /// <summary>
        /// Интервал запуска задачи.
        /// </summary>
        public Interval? Interval { get; set; }

        ///// <summary>
        ///// Дата запуска задачи в интервале.
        ///// </summary>
        //public DateTime? StartAt { get; set; }

        /// <summary>
        /// Период запуска задачи.
        /// </summary>
        public Int32? TimePeriod { get; set; }

        /// <summary>
        /// Периодичность запуска задачи
        /// </summary>
        public Periodicity? Periodicity { get; set; }

        /// <summary>
        /// Дни недели для запуска.
        /// </summary>
        public string DaysOfWeekSch { get; set; }

        /// <summary>
        /// День месяца запуска задачи.
        /// </summary>
        public string DaysOfMonthSch { get; set; }

        /// <summary>
        /// Состояние расписания.
        /// </summary>
        public ScheduleStatus Status { get; set; }
    }
}
