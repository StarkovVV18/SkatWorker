﻿using System.ComponentModel.DataAnnotations;

namespace WorkflowCore.Persistence.EntityFramework.Models
{
    public class PersistedDefinition
    {
        [Key]
        public string Id { get; set; }

        public string Value { get; set; }

        public string Version { get; set; }
    }
}
