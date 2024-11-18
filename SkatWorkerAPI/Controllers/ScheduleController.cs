using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SkatWorker.Infrastructure.Models.Request;
using SkatWorker.Infrastructure.Models.Response;
using System.Collections.Generic;
using System.Threading.Tasks;

using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace SkatWorkerAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ScheduleController : ControllerBase
    {
        private readonly IPersistenceProvider _persistenceProvider;
        private readonly IWorkflowRegistry _workflowRegistry;
        private readonly IMapper _mapper;

        public ScheduleController(IPersistenceProvider persistenceProvider, IWorkflowRegistry workflowRegistry, IMapper mapper)
        {
            _persistenceProvider = persistenceProvider;
            _workflowRegistry = workflowRegistry;
            _mapper = mapper;
        }

        /// <summary>
        /// Получить расписание.
        /// </summary>
        /// <returns>Список задач добавленных в расписание.</returns>
        //[ProducesResponseType(typeof(TaskScheduleResponse), 200)]
        //[ProducesResponseType(typeof(NotFoundResponse), 404)]
        [HttpGet("list")]
        public async Task<ActionResult<IEnumerable<TaskScheduleResponse>>> GetSchedule()
        {
            var schedules = await _persistenceProvider.GetTaskSchedules();

            if (schedules == null)
                return NotFound(new NotFoundResponse("Отсутствуют задачи в расписании."));


            return Ok(_mapper.Map<List<TaskScheduleResponse>>(schedules));
        }

        /// <summary>
        /// Добавление задачи в расписание.
        /// </summary>
        /// <returns>Добавленную в расписание задачу.</returns>
        [ProducesResponseType(typeof(TaskScheduleResponse), 200)]
        [ProducesResponseType(typeof(NotFoundResponse), 404)]
        [HttpPost("create")]
        public async Task<ActionResult<TaskScheduleResponse>> CreateSchedule([FromBody] TaskSheduleRequest param)
        {
            var definition = _workflowRegistry.GetDefinition(param.WorkflowId);

            if (definition == null)
                return NotFound(new NotFoundResponse(string.Format("Не удалось найти задачу с идентификатором {0}",param.WorkflowId)));

            var dataTypeInstance = JsonConvert.DeserializeObject(param.Data, definition.DataType);
            var taskSchedule = _mapper.Map<TaskSchedule>(param);

            // TODO: Переделать.
            taskSchedule.Data = dataTypeInstance;

            var result = await _persistenceProvider.CreateTaskSchedule(taskSchedule);

            return Ok(_mapper.Map<TaskScheduleResponse>(result));
        }
    }
}
