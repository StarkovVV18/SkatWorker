﻿using AutoMapper;
using SkatWorker.Infrastructure.Models.Request;
using SkatWorker.Infrastructure.Models.Response;
using WorkflowCore.Models;

namespace SkatWorker.Infrastructure.Mapper
{
    public class SkatWorkerMapper : Profile
    {
        public SkatWorkerMapper()
        {
            CreateMap<WorkflowCore.Models.WorkflowInstance, WorkflowInstanceResponse>()
                .ForMember(dest => dest.DefinitionId, opt => opt.MapFrom(src => src.WorkflowDefinitionId))
                .ForMember(dest => dest.EndTime, opt => opt.MapFrom(src => src.CompleteTime))
                .ForMember(dest => dest.Reference, opt => opt.MapFrom(src => src.Reference))
                .ForMember(dest => dest.StartTime, opt => opt.MapFrom(src => src.CreateTime))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
                .ForMember(dest => dest.Version, opt => opt.MapFrom(src => src.Version))
                .ForMember(dest => dest.Data, opt => opt.MapFrom(src => src.Data))
                .ForMember(dest => dest.WorkflowId, opt => opt.MapFrom(src => src.Id));

            CreateMap<TaskSheduleRequest, TaskSchedule>()
                .ForMember(dest => dest.WorkflowId, opt => opt.MapFrom(src => src.WorkflowId))
                .ForMember(dest => dest.Version, opt => opt.MapFrom(src => src.Version))
                .ForMember(dest => dest.StartTime, opt => opt.MapFrom(src => src.StartTime))
                .ForMember(dest => dest.Data, opt => opt.MapFrom(src => src.Data));

            CreateMap<TaskSchedule, TaskScheduleResponse>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.WorkflowId, opt => opt.MapFrom(src => src.WorkflowId))
                .ForMember(dest => dest.InstanceId, opt => opt.MapFrom(src => src.InstanceId))
                .ForMember(dest => dest.Version, opt => opt.MapFrom(src => src.Version))
                .ForMember(dest => dest.Data, opt => opt.MapFrom(src => src.Data))
                .ForMember(dest => dest.StartTime, opt => opt.MapFrom(src => src.StartTime))
                .ForMember(dest => dest.CompleteTime, opt => opt.MapFrom(src => src.CompleteTime))
                .ForMember(dest => dest.Result, opt => opt.MapFrom(src => src.Result));

            CreateMap<StepResult, StepResultResponse>();
        }
    }
}
