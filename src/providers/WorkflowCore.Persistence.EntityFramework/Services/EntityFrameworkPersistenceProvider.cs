using Microsoft.EntityFrameworkCore;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

using WorkflowCore.Interface;
using WorkflowCore.Persistence.EntityFramework.Models;
using WorkflowCore.Models;
using WorkflowCore.Persistence.EntityFramework.Interfaces;
using System.Linq.Expressions;

namespace WorkflowCore.Persistence.EntityFramework.Services
{
    public class EntityFrameworkPersistenceProvider : IPersistenceProvider
    {
        private readonly bool _canCreateDB;
        private readonly bool _canMigrateDB;
        private readonly IWorkflowDbContextFactory _contextFactory;

        public bool SupportsScheduledCommands => true;

        public EntityFrameworkPersistenceProvider(IWorkflowDbContextFactory contextFactory, bool canCreateDB, bool canMigrateDB)
        {
            _contextFactory = contextFactory;
            _canCreateDB = canCreateDB;
            _canMigrateDB = canMigrateDB;
        }

        private WorkflowDbContext ConstructDbContext()
        {
            return _contextFactory.Build();
        }

        #region IDefinition

        public async Task<Definition> CreateDefinition(Definition definition, CancellationToken cancellationToken = default)
        {
            using (var db = ConstructDbContext())
            {
                definition.Id = Guid.NewGuid().ToString();
                
                var persistable = definition.ToPersistable();
                var result = db.Set<PersistedDefinition>().Add(persistable);

                await db.SaveChangesAsync(cancellationToken);

                return definition;
            }
        }

        public async Task<Definition> GetDefinition(string id, CancellationToken cancellationToken = default)
        {
            using (var db = ConstructDbContext())
            {
                Guid uid = new Guid(id);

                var raw = await db.Set<PersistedDefinition>()
                    .FirstAsync(x => Guid.Equals(x.Id, uid), cancellationToken);

                if (raw == null)
                    return null;

                return raw.ToDefinition();
            }
        }

        #endregion

        #region IEvent

        public async Task<string> CreateEvent(Event newEvent, CancellationToken cancellationToken = default)
        {
            using (var db = ConstructDbContext())
            {
                newEvent.Id = Guid.NewGuid().ToString();
                var persistable = newEvent.ToPersistable();
                var result = db.Set<PersistedEvent>().Add(persistable);
                await db.SaveChangesAsync(cancellationToken);
                return newEvent.Id;
            }
        }

        public async Task<Event> GetEvent(string id, CancellationToken cancellationToken = default)
        {
            using (var db = ConstructDbContext())
            {
                Guid uid = new Guid(id);
                var raw = await db.Set<PersistedEvent>()
                    .FirstAsync(x => x.EventId == uid, cancellationToken);

                if (raw == null)
                    return null;

                return raw.ToEvent();
            }
        }

        public async Task<IEnumerable<string>> GetRunnableEvents(DateTime asAt, CancellationToken cancellationToken = default)
        {
            var now = asAt.ToUniversalTime();
            using (var db = ConstructDbContext())
            {
                asAt = asAt.ToUniversalTime();
                var raw = await db.Set<PersistedEvent>()
                    .Where(x => !x.IsProcessed)
                    .Where(x => x.EventTime <= now)
                    .Select(x => x.EventId)
                    .ToListAsync(cancellationToken);

                return raw.Select(s => s.ToString()).ToList();
            }
        }

        public async Task<IEnumerable<string>> GetEvents(string eventName, string eventKey, DateTime asOf, CancellationToken cancellationToken)
        {
            using (var db = ConstructDbContext())
            {
                var raw = await db.Set<PersistedEvent>()
                    .Where(x => x.EventName == eventName && x.EventKey == eventKey)
                    .Where(x => x.EventTime >= asOf)
                    .Select(x => x.EventId)
                    .ToListAsync(cancellationToken);

                var result = new List<string>();

                foreach (var s in raw)
                    result.Add(s.ToString());

                return result;
            }
        }

        public async Task MarkEventProcessed(string id, CancellationToken cancellationToken = default)
        {
            using (var db = ConstructDbContext())
            {
                var uid = new Guid(id);
                var existingEntity = await db.Set<PersistedEvent>()
                    .Where(x => x.EventId == uid)
                    .AsTracking()
                    .FirstAsync(cancellationToken);

                existingEntity.IsProcessed = true;
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task MarkEventUnprocessed(string id, CancellationToken cancellationToken = default)
        {
            using (var db = ConstructDbContext())
            {
                var uid = new Guid(id);
                var existingEntity = await db.Set<PersistedEvent>()
                    .Where(x => x.EventId == uid)
                    .AsTracking()
                    .FirstAsync(cancellationToken);

                existingEntity.IsProcessed = false;
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        #endregion

        #region IPersistence

        public virtual void EnsureStoreExists()
        {
            using (var context = ConstructDbContext())
            {
                if (_canCreateDB && !_canMigrateDB)
                {
                    context.Database.EnsureCreated();
                    return;
                }

                if (_canMigrateDB)
                {
                    context.Database.Migrate();
                    return;
                }
            }
        }

        public async Task PersistErrors(IEnumerable<ExecutionError> errors, CancellationToken cancellationToken = default)
        {
            using (var db = ConstructDbContext())
            {
                var executionErrors = errors as ExecutionError[] ?? errors.ToArray();
                if (executionErrors.Any())
                {
                    foreach (var error in executionErrors)
                    {
                        db.Set<PersistedExecutionError>().Add(error.ToPersistable());
                    }
                    await db.SaveChangesAsync(cancellationToken);

                }
            }
        }

        #endregion

        #region IScheduledCommand

        public async Task ScheduleCommand(ScheduledCommand command)
        {
            try
            {
                using (var db = ConstructDbContext())
                {
                    var persistable = command.ToPersistable();
                    var result = db.Set<PersistedScheduledCommand>().Add(persistable);
                    await db.SaveChangesAsync();
                }
            }
            catch (DbUpdateException)
            {
                //log
            }
        }

        public async Task ProcessCommands(DateTimeOffset asOf, Func<ScheduledCommand, Task> action, CancellationToken cancellationToken = default)
        {
            using (var db = ConstructDbContext())
            {
                var cursor = db.Set<PersistedScheduledCommand>()
                    .Where(x => x.ExecuteTime < asOf.UtcDateTime.Ticks)
                    .AsAsyncEnumerable();

                await foreach (var command in cursor)
                {
                    try
                    {
                        await action(command.ToScheduledCommand());
                        using var db2 = ConstructDbContext();
                        db2.Set<PersistedScheduledCommand>().Remove(command);
                        await db2.SaveChangesAsync();
                    }
                    catch (Exception)
                    {
                        //TODO: add logger
                    }
                }
            }
        }

        #endregion

        #region ISubscription

        public async Task<string> CreateEventSubscription(EventSubscription subscription, CancellationToken cancellationToken = default)
        {
            using (var db = ConstructDbContext())
            {
                subscription.Id = Guid.NewGuid().ToString();
                var persistable = subscription.ToPersistable();
                var result = db.Set<PersistedSubscription>().Add(persistable);
                await db.SaveChangesAsync(cancellationToken);
                return subscription.Id;
            }
        }

        public async Task TerminateSubscription(string eventSubscriptionId, CancellationToken cancellationToken = default)
        {
            using (var db = ConstructDbContext())
            {
                var uid = new Guid(eventSubscriptionId);
                var existing = await db.Set<PersistedSubscription>().FirstAsync(x => x.SubscriptionId == uid, cancellationToken);
                db.Set<PersistedSubscription>().Remove(existing);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task<IEnumerable<EventSubscription>> GetSubscriptions(string eventName, string eventKey, DateTime asOf, CancellationToken cancellationToken = default)
        {
            using (var db = ConstructDbContext())
            {
                asOf = asOf.ToUniversalTime();
                var raw = await db.Set<PersistedSubscription>()
                    .Where(x => x.EventName == eventName && x.EventKey == eventKey && x.SubscribeAsOf <= asOf)
                    .ToListAsync(cancellationToken);

                return raw.Select(item => item.ToEventSubscription()).ToList();
            }
        }

        public async Task<EventSubscription> GetSubscription(string eventSubscriptionId, CancellationToken cancellationToken = default)
        {
            using (var db = ConstructDbContext())
            {
                var uid = new Guid(eventSubscriptionId);
                var raw = await db.Set<PersistedSubscription>().FirstOrDefaultAsync(x => x.SubscriptionId == uid, cancellationToken);

                return raw?.ToEventSubscription();
            }
        }

        public async Task<EventSubscription> GetFirstOpenSubscription(string eventName, string eventKey, DateTime asOf, CancellationToken cancellationToken = default)
        {
            using (var db = ConstructDbContext())
            {
                var raw = await db.Set<PersistedSubscription>().FirstOrDefaultAsync(x => x.EventName == eventName && x.EventKey == eventKey && x.SubscribeAsOf <= asOf && x.ExternalToken == null, cancellationToken);

                return raw?.ToEventSubscription();
            }
        }

        public async Task<bool> SetSubscriptionToken(string eventSubscriptionId, string token, string workerId, DateTime expiry, CancellationToken cancellationToken = default)
        {
            using (var db = ConstructDbContext())
            {
                var uid = new Guid(eventSubscriptionId);
                var existingEntity = await db.Set<PersistedSubscription>()
                    .Where(x => x.SubscriptionId == uid)
                    .AsTracking()
                    .FirstAsync(cancellationToken);

                existingEntity.ExternalToken = token;
                existingEntity.ExternalWorkerId = workerId;
                existingEntity.ExternalTokenExpiry = expiry;
                await db.SaveChangesAsync(cancellationToken);

                return true;
            }
        }

        public async Task ClearSubscriptionToken(string eventSubscriptionId, string token, CancellationToken cancellationToken = default)
        {
            using (var db = ConstructDbContext())
            {
                var uid = new Guid(eventSubscriptionId);
                var existingEntity = await db.Set<PersistedSubscription>()
                    .Where(x => x.SubscriptionId == uid)
                    .AsTracking()
                    .FirstAsync(cancellationToken);

                if (existingEntity.ExternalToken != token)
                    throw new InvalidOperationException();

                existingEntity.ExternalToken = null;
                existingEntity.ExternalWorkerId = null;
                existingEntity.ExternalTokenExpiry = null;
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        #endregion

        #region ITaskSchedule

        public async Task<TaskSchedule> CreateTaskSchedule(TaskSchedule taskSchedule, CancellationToken cancellationToken = default)
        {
            using (var db = ConstructDbContext())
            {
                taskSchedule.Id = Guid.NewGuid().ToString();

                var persistable = taskSchedule.ToPersistable();
                var result = db.Set<PersistedTaskSchedule>().Add(persistable);

                await db.SaveChangesAsync(cancellationToken);

                return taskSchedule;
            }
        }

        public async Task<bool> TerminateTaskSchedule(string id, CancellationToken cancellationToken = default)
        {
            using (var db = ConstructDbContext())
            {
                var existingEntity = db.Set<PersistedTaskSchedule>()
                    .Where(x => x.Id == id)
                    .AsTracking();

                if (!existingEntity.Any())
                    return false;

                var firstRow = existingEntity.FirstOrDefault();
                firstRow.IsProcessed = false;
                firstRow.Status = ScheduleStatus.Terminated;

                await db.SaveChangesAsync(cancellationToken);
                return true;
            }
        }

        public async Task<TaskSchedule> GetTaskSchedule(string id, CancellationToken cancellationToken = default)
        {
            using (var db = ConstructDbContext())
            {
                Guid uid = new Guid(id);

                var raw = await db.Set<PersistedTaskSchedule>()
                    .FirstAsync(x => Guid.Equals(x.Id, uid), cancellationToken);

                if (raw == null)
                    return null;

                return raw.ToTaskSchedule();
            }
        }

        public async Task<IEnumerable<TaskSchedule>> GetTaskSchedules(CancellationToken cancellationToken = default)
        {
            var persistedTaskSchedule = new List<PersistedTaskSchedule>();
            var taskSchedule = new List<TaskSchedule>();

            using (var db = ConstructDbContext())
                persistedTaskSchedule = await db.Set<PersistedTaskSchedule>().ToListAsync(cancellationToken);

            foreach (var task in persistedTaskSchedule)
                taskSchedule.Add(task.ToTaskSchedule());

            return taskSchedule;
        }

        public async Task<IEnumerable<TaskSchedule>> GetTaskSchedules(Func<TaskSchedule, bool> expression, CancellationToken cancellationToken = default)
        {
            var persistedTaskSchedule = new List<PersistedTaskSchedule>();
            var taskSchedule = new List<TaskSchedule>();

            using (var db = ConstructDbContext())
                persistedTaskSchedule = await db.Set<PersistedTaskSchedule>().ToListAsync(cancellationToken);

            foreach (var task in persistedTaskSchedule)
                taskSchedule.Add(task.ToTaskSchedule());

            return taskSchedule.Where(expression);
        }

        public async Task MarkTaskScheduleProcessed(string id, string instanceId, CancellationToken cancellationToken = default)
        {
            using (var db = ConstructDbContext())
            {
                var existingEntity = db.Set<PersistedTaskSchedule>()
                    .Where(x => x.Id == id)
                    .AsTracking();

                if (!existingEntity.Any())
                    return;

                var firstRow = existingEntity.FirstOrDefault();
                firstRow.IsProcessed = true;
                firstRow.InstanceId = instanceId;

                await db.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task MarkTaskScheduleUnprocessed(string id, CancellationToken cancellationToken = default)
        {
            using (var db = ConstructDbContext())
            {
                var existingEntity = db.Set<PersistedTaskSchedule>()
                    .Where(x => x.Id == id)
                    .AsTracking();

                if (!existingEntity.Any())
                    return;

                var firstRow = existingEntity.FirstOrDefault();
                firstRow.IsProcessed = false;

                await db.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task MarkTaskScheduleCompleted(string id, DateTime completeTime, DateTime nextExecuted, CancellationToken cancellationToken = default)
        {
            using (var db = ConstructDbContext())
            {
                var existingEntity = db.Set<PersistedTaskSchedule>()
                    .Where(x => x.Id == id)
                    .AsTracking();

                if (!existingEntity.Any())
                    return;

                var firstRow = existingEntity.FirstOrDefault();
                firstRow.IsProcessed = false;
                firstRow.CompleteTime = completeTime;
                firstRow.NextExecuted = nextExecuted;
                firstRow.LastExecuted = completeTime;

                await db.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task MarkTaskScheduleUnCompleted(string id, CancellationToken cancellationToken = default)
        {
            using (var db = ConstructDbContext())
            {
                var existingEntity = db.Set<PersistedTaskSchedule>()
                    .Where(x => x.Id == id)
                    .AsTracking();

                if (!existingEntity.Any())
                    return;

                var firstRow = existingEntity.FirstOrDefault();
                firstRow.CompleteTime = null;

                await db.SaveChangesAsync(cancellationToken);
            }
        }

        #endregion

        #region IWorkflow

        public async Task<string> CreateNewWorkflow(WorkflowInstance workflow, CancellationToken cancellationToken = default)
        {
            using (var db = ConstructDbContext())
            {
                workflow.Id = Guid.NewGuid().ToString();
                var persistable = workflow.ToPersistable();
                var result = db.Set<PersistedWorkflow>().Add(persistable);
                await db.SaveChangesAsync(cancellationToken);
                return workflow.Id;
            }
        }

        public async Task<IEnumerable<string>> GetRunnableInstances(DateTime asAt, CancellationToken cancellationToken = default)
        {
            using (var db = ConstructDbContext())
            {
                var now = asAt.ToUniversalTime().Ticks;
                var raw = await db.Set<PersistedWorkflow>()
                    .Where(x => x.NextExecution.HasValue && (x.NextExecution <= now) && (x.Status == WorkflowStatus.Runnable))
                    .Select(x => x.InstanceId)
                    .ToListAsync(cancellationToken);

                return raw.Select(s => s.ToString()).ToList();
            }
        }

        public async Task<IEnumerable<WorkflowInstance>> GetWorkflowInstances(WorkflowStatus? status, string type, DateTime? createdFrom, DateTime? createdTo, int skip, int take)
        {
            using (var db = ConstructDbContext())
            {
                IQueryable<PersistedWorkflow> query = db.Set<PersistedWorkflow>()
                    .Include(wf => wf.ExecutionPointers)
                    .ThenInclude(ep => ep.ExtensionAttributes)
                    .Include(wf => wf.ExecutionPointers)
                    .AsQueryable();

                if (status.HasValue)
                    query = query.Where(x => x.Status == status.Value);

                if (!String.IsNullOrEmpty(type))
                    query = query.Where(x => x.WorkflowDefinitionId == type);

                if (createdFrom.HasValue)
                    query = query.Where(x => x.CreateTime >= createdFrom.Value);

                if (createdTo.HasValue)
                    query = query.Where(x => x.CreateTime <= createdTo.Value);

                var rawResult = await query.Skip(skip).Take(take).ToListAsync();
                List<WorkflowInstance> result = new List<WorkflowInstance>();

                foreach (var item in rawResult)
                    result.Add(item.ToWorkflowInstance());

                return result;
            }
        }

        public async Task<WorkflowInstance> GetWorkflowInstance(string Id, CancellationToken cancellationToken = default)
        {
            using (var db = ConstructDbContext())
            {
                var uid = new Guid(Id);
                var raw = db.Set<PersistedWorkflow>()
                    .Include(wf => wf.ExecutionPointers)
                    .ThenInclude(ep => ep.ExtensionAttributes)
                    .Include(wf => wf.ExecutionPointers)
                    .Where<PersistedWorkflow>(x => x.InstanceId == uid);

                if (raw == null || !raw.Any())
                    return null;

                return raw.FirstOrDefault<PersistedWorkflow>().ToWorkflowInstance();
            }
        }

        public async Task<IEnumerable<WorkflowInstance>> GetWorkflowInstances(IEnumerable<string> ids, CancellationToken cancellationToken = default)
        {
            if (ids == null)
            {
                return new List<WorkflowInstance>();
            }

            using (var db = ConstructDbContext())
            {
                var uids = ids.Select(i => new Guid(i));
                var raw = db.Set<PersistedWorkflow>()
                    .Include(wf => wf.ExecutionPointers)
                    .ThenInclude(ep => ep.ExtensionAttributes)
                    .Include(wf => wf.ExecutionPointers)
                    .Where(x => uids.Contains(x.InstanceId));

                return (await raw.ToListAsync(cancellationToken)).Select(i => i.ToWorkflowInstance());
            }
        }

        public async Task PersistWorkflow(WorkflowInstance workflow, CancellationToken cancellationToken = default)
        {
            using (var db = ConstructDbContext())
            {
                var uid = new Guid(workflow.Id);
                var existingEntity = await db.Set<PersistedWorkflow>()
                    .Where(x => x.InstanceId == uid)
                    .Include(wf => wf.ExecutionPointers)
                    .ThenInclude(ep => ep.ExtensionAttributes)
                    .Include(wf => wf.ExecutionPointers)
                    .AsTracking()
                    .FirstAsync(cancellationToken);

                var persistable = workflow.ToPersistable(existingEntity);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task PersistWorkflow(WorkflowInstance workflow, List<EventSubscription> subscriptions, CancellationToken cancellationToken = default)
        {
            using (var db = ConstructDbContext())
            {
                var uid = new Guid(workflow.Id);
                var existingEntity = await db.Set<PersistedWorkflow>()
                    .Where(x => x.InstanceId == uid)
                    .Include(wf => wf.ExecutionPointers)
                    .ThenInclude(ep => ep.ExtensionAttributes)
                    .Include(wf => wf.ExecutionPointers)
                    .AsTracking()
                    .FirstAsync(cancellationToken);

                var workflowPersistable = workflow.ToPersistable(existingEntity);

                foreach (var subscription in subscriptions)
                {
                    subscription.Id = Guid.NewGuid().ToString();
                    var subscriptionPersistable = subscription.ToPersistable();
                    db.Set<PersistedSubscription>().Add(subscriptionPersistable);
                }

                await db.SaveChangesAsync(cancellationToken);
            }
        }

        #endregion

        #region IStepResult

        public async Task<StepResult> CreateStepResult(StepResult stepResult, CancellationToken cancellationToken = default)
        {
            using (var db = ConstructDbContext())
            {
                stepResult.Id = Guid.NewGuid().ToString();

                var persistable = stepResult.ToPersistable();
                var result = db.Set<PersistedStepResult>().Add(persistable);

                await db.SaveChangesAsync(cancellationToken);

                return stepResult;
            }
        }

        public async Task<StepResult> GetStepResult(string id, CancellationToken cancellationToken = default)
        {
            using (var db = ConstructDbContext())
            {
                Guid uid = new Guid(id);

                var raw = await db.Set<PersistedStepResult>()
                    .FirstAsync(x => Guid.Equals(x.Id, uid), cancellationToken);

                if (raw == null)
                    return null;

                return raw.ToStepResult();
            }
        }

        public async Task<IEnumerable<StepResult>> GetStepResults(CancellationToken cancellationToken = default)
        {
            var persistedStepResults = new List<PersistedStepResult>();
            var stepResults = new List<StepResult>();

            using (var db = ConstructDbContext())
                persistedStepResults = await db.Set<PersistedStepResult>().ToListAsync(cancellationToken);

            foreach (var stepResult in persistedStepResults)
                stepResults.Add(stepResult.ToStepResult());

            return stepResults;
        }

        public async Task<IEnumerable<StepResult>> GetStepResults(Func<StepResult, bool> expression, CancellationToken cancellationToken = default)
        {
            var persistedStepResults = new List<PersistedStepResult>();
            var stepResults = new List<StepResult>();

            using (var db = ConstructDbContext())
                persistedStepResults = await db.Set<PersistedStepResult>().ToListAsync(cancellationToken);

            foreach (var stepResult in persistedStepResults)
                stepResults.Add(stepResult.ToStepResult());

            return stepResults.Where(expression);
        }

        #endregion
    }
}
