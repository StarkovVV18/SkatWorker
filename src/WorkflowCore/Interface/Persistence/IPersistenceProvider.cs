﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WorkflowCore.Interface.Persistence;
using WorkflowCore.Models;

namespace WorkflowCore.Interface
{
    public interface IPersistenceProvider :
        IWorkflowRepository,
        ISubscriptionRepository,
        IEventRepository,
        IScheduledCommandRepository,
        IDefinitionRepository,
        ITaskScheduleRepository,
        IStepResultRepository
    {        

        Task PersistErrors(IEnumerable<ExecutionError> errors, CancellationToken cancellationToken = default);

        void EnsureStoreExists();

    }
}
