using System.ServiceProcess;

namespace SkatWorker.Workflows.Public.Steps.SystemService.Inputs
{
    public class SystemServiceParam
    {
        /// <summary>
        /// Имя сервиса.
        /// </summary>
        public string ServiceName { get; set; }

        /// <summary>
        /// Действие над службой.
        /// </summary>
        public SystemServiceCommand SystemServiceCommand { get; set; }
    }
}
