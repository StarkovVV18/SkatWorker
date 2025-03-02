using SkatWorker.Workflows.Public.Steps.SystemService.Inputs;
using WorkflowCore.Interface;

namespace SkatWorker.Workflows.Workflows.SystemService
{
    public class SystemService : IWorkflow<SystemServiceParam>
    {
        public string Id => "SystemService";

        public int Version => 1;

        public void Build(IWorkflowBuilder<SystemServiceParam> builder)
        {
            builder.StartWith<SkatWorker.Workflows.Public.Steps.SystemService.SystemService>()
                .Input(step => step.ServiceName, data => data.ServiceName)
                .Input(step => step.SystemServiceCommand, data => data.SystemServiceCommand);
        }
    }
}
