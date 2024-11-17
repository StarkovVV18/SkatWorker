using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

using WorkflowCore.Interface;
using SkatWorker.Infrastructure.Services;
using SkatWorker.Workflows.WorkflowDSLReader.Steps;
using SkatWorker.Workflows.WorkflowDSLReader.Inputs;
using SkatWorker.Workflows.WorkflowDSLReader;
using SkatWorker.Workflows.Public.Steps.CopyFiles;
using SkatWorker.Application.Interfaces.Services;
using SkatWorker.Infrastructure.Services.DownloaderService;
using SkatWorker.Workflows.Public.Steps.HttpDownloader;
using SkatWorker.Workflows.Public.Steps.FtpDownloader;
using SkatWorker.Workflows.Workflows.CopyFiles;
using SkatWorker.Workflows.Public.Steps.CopyFiles.Parameters;
using SkatWorker.Workflows.Public.Steps.HttpDownloader.Inputs;
using SkatWorker.Workflows.Public.Steps.FtpDownloader.Inputs;
using SkatWorker.Workflows.Public.Steps.SystemService.Inputs;
using SkatWorker.Workflows.Public.Steps.SystemService;

using AutoMapper;
using Serilog;
using SkatWorkerAPI.Models.AppSettings;

namespace SkatWorkerAPI
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IConfiguration _configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var settings = _configuration.GetSection("Settings").Get<Settings>();

            string connectionString = $"Data Source = " + Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wfdb.db");
            string pathToLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

            if (settings != null && !string.IsNullOrEmpty(settings.ConnectionString))
                connectionString = settings.ConnectionString;

            if (settings != null && !string.IsNullOrEmpty(settings.PathToLog))
                pathToLog = settings.PathToLog;

            services.AddSerilog(x =>
            {
                x.WriteTo.Console();
                x.WriteTo.File(Path.Combine(pathToLog, $"{DateTime.Now.Year}-{DateTime.Now.Month}-{DateTime.Now.Day}.txt"));
            });

            services.AddControllers();

            // Подключаем свагер.
            services.AddSwaggerGen(swagger =>
            {
                swagger.SwaggerDoc("v1", new OpenApiInfo
                {
                    Version = "v1",
                    Title = "SkatWorkerAPI"
                });

                var filePath = Path.Combine(System.AppContext.BaseDirectory, "SkatWorkerAPI.xml");
                swagger.IncludeXmlComments(filePath);
            });

            // Подключени мапера
            var mapperConfig = new MapperConfiguration(x => {
                    x.AllowNullCollections = true;
                    x.AddProfile<SkatWorker.Infrastructure.Mapper.SkatWorkerMapper>();
                });

            services.AddSingleton<IMapper>(x => new Mapper(mapperConfig));

            // Сервисы workflow.
            services.AddWorkflow(wf => wf.UseSqlite(connectionString, true));
            services.AddWorkflowDSL();

            // Шаги для базовых рабочих процессов.
            services.AddTransient<GetFilesFromDirectory>();
            services.AddTransient<LoadWorkflow>();
            services.AddTransient<GetFilesFromDirectoryWeb>();
            services.AddTransient<LoadWorkflowWeb>();

            // Публичные шаги.
            services.AddTransient<SkatWorker.Workflows.Public.Steps.CopyFiles.CopyFile>();
            services.AddTransient<HttpDownloader>();
            services.AddTransient<FtpDownloader>();
            services.AddTransient<SystemService>();

            // Внутренние сервисы.
            services.AddTransient<IDefinitionService, DefinitionService>();
            services.AddTransient<IWorkflowService, WorkflowService>();
            services.AddTransient<DownloaderService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "API V1");
            });

            //app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            // Запускает хост workflow.
            var wfHost = app.ApplicationServices.GetService<IWorkflowHost>();

            // Регистрируем базовые задачи.
            wfHost.RegisterWorkflow<WorkflowDSLReaderPath, FilesFromDirectory>();
            wfHost.RegisterWorkflow<WorkflowDSLReaderWeb, FilesFromDirectory>();
            wfHost.RegisterWorkflow<SkatWorker.Workflows.Workflows.CopyFiles.CopyFile, CopyFileParam>();
            wfHost.RegisterWorkflow<SkatWorker.Workflows.Workflows.HttpDownloader.HttpDownloader, HttpDownloaderParam>();
            wfHost.RegisterWorkflow<SkatWorker.Workflows.Workflows.FtpDownloader.FtpDownloader, FtpDownloaderParam>();
            wfHost.RegisterWorkflow<SkatWorker.Workflows.Workflows.SystemService.SystemService, SystemServiceParam>();

            wfHost.Start();
        }
    }
}
