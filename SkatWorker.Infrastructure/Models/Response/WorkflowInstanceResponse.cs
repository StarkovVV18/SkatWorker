﻿using System;

namespace SkatWorker.Infrastructure.Models.Response
{
    public class WorkflowInstanceResponse
    {
        public string WorkflowId { get; set; }
        public object Data { get; set; }
        public string DefinitionId { get; set; }
        public int Version { get; set; }
        public string Status { get; set; }
        public string Reference { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
    }
}
