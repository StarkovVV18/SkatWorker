using System;
using System.Numerics;
using WorkflowCore.Interface;
using WorkflowCore.Models.Enums;

namespace WorkflowCore.Models
{
    /// <summary>
    /// Расписание запуска задачи.
    /// </summary>
    public class TaskSchedule
    {
        /// <summary>
        /// Иднетификатор записи.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Идентификатор задачи.
        /// </summary>
        public string WorkflowId { get; set; }

        /// <summary>
        /// Идентификатор стартованой задачи.
        /// </summary>
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
        public bool? IsProcessed { get; set; }

        /// <summary>
        /// Входные данные задачи.
        /// </summary>
        public object Data { get; set; }

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

        /// <summary>
        /// Начало в (дата запуска задачи в периоде).
        /// </summary>
        public DateTime? StartAt { get; set; }

        /// <summary>
        /// Период запуска задачи (в минутах).
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


    }
}
