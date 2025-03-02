using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using SkatWorker.Infrastructure.Models.Request;
using SkatWorker.Infrastructure.Models.Response;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace SkatWorkerAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StepResultController : ControllerBase
    {
        private readonly IPersistenceProvider _persistenceProvider;
        private readonly IWorkflowRegistry _workflowRegistry;
        private readonly IMapper _mapper;

        public StepResultController(IPersistenceProvider persistenceProvider, IWorkflowRegistry workflowRegistry, IMapper mapper)
        {
            _persistenceProvider = persistenceProvider;
            _workflowRegistry = workflowRegistry;
            _mapper = mapper;
        }

        /// <summary>
        /// Найти результаты выполнения задачи.
        /// </summary>
        /// <param name="id">Идентификатор запущенной задачи.</param>
        /// <returns>Список результатов по запущенной задаче.</returns>
        [ProducesResponseType(typeof(IEnumerable<StepResultResponse>), 200)]
        [ProducesResponseType(typeof(NotFoundResponse), 404)]
        [HttpPost]
        public async Task<ActionResult<IEnumerable<StepResultResponse>>> GetStepResult([FromBody] StepResultRequest request)
        {
            var stepResults = await _persistenceProvider.GetStepResults(x => string.Equals(x.InstanceId.ToLower(), request.InstanceId.ToLower())
                                                                            && string.Equals(x.WorkflowId.ToLower(), request.WorkflowId.ToLower())
                                                                            && x.Version == request.Version);

            return Ok(_mapper.Map<List<StepResultResponse>>(stepResults));

        }
    }
}
