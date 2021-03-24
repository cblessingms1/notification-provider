// <autogenerated />
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace NotificationService
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.ApplicationInsights.AspNetCore;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.DependencyCollector;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;
    using Microsoft.OpenApi.Models;
    using DirectSend;
    using DirectSend.Models.Configurations;
    using NotificationService.BusinessLibrary;
    using NotificationService.BusinessLibrary.Business.v1;
    using NotificationService.BusinessLibrary.Interfaces;
    using NotificationService.BusinessLibrary.Providers;
    using NotificationService.BusinessLibrary.Utilities;
    using NotificationService.Common;
    using NotificationService.Common.Logger;
    using NotificationService.Data;
    using NotificationService.Data.Interfaces;
    using NotificationService.SvCommon.Common;
    using NotificationService.Common.Configurations;

    /// <summary>
    /// Startup configuration for the service.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class Startup : StartupCommon
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Startup"/> class.
        /// </summary>
        public Startup()
            : base("Service")
        {
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        /// <param name="services">An instance of <see cref="IServiceCollection"/>.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            this.ConfigureServicesCommon(services);

            _ = services.AddMvc();
            _ = services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "NotificationService", Version = "v1" });
            });

            ITelemetryInitializer[] itm = new ITelemetryInitializer[1];
            var envInitializer = new EnvironmentInitializer
            {
                Service = this.Configuration[AIConstants.ServiceConfigName],
                ServiceLine = this.Configuration[AIConstants.ServiceLineConfigName],
                ServiceOffering = this.Configuration[AIConstants.ServiceOfferingConfigName],
                ComponentId = this.Configuration[AIConstants.ComponentIdConfigName],
                ComponentName = this.Configuration[AIConstants.ComponentNameConfigName],
                EnvironmentName = this.Configuration[AIConstants.EnvironmentName],
                IctoId = "IctoId",
            };
            itm[0] = envInitializer;

            NotificationProviders.Common.Logger.LoggingConfiguration loggingConfiguration = new NotificationProviders.Common.Logger.LoggingConfiguration
            {
                IsTraceEnabled = true,
                TraceLevel = (SeverityLevel)Enum.Parse(typeof(SeverityLevel), this.Configuration[ConfigConstants.AITraceLelelConfigKey]),
                EnvironmentName = this.Configuration[AIConstants.EnvironmentName],
            };

            var tconfig = TelemetryConfiguration.CreateDefault();
            tconfig.InstrumentationKey = this.Configuration[ConfigConstants.AIInsrumentationConfigKey];

            DependencyTrackingTelemetryModule depModule = new DependencyTrackingTelemetryModule();
            depModule.Initialize(tconfig);

            RequestTrackingTelemetryModule requestTrackingTelemetryModule = new RequestTrackingTelemetryModule();
            requestTrackingTelemetryModule.Initialize(tconfig);

            _ = services.AddSingleton<NotificationProviders.Common.Logger.ILogger>(_ => new NotificationProviders.Common.Logger.AILogger(loggingConfiguration, tconfig, itm));

            _ = services.AddScoped<IEmailManager, EmailManager>(s =>
                    new EmailManager(
                        this.Configuration,
                        s.GetService<IRepositoryFactory>(),
                        s.GetService<ILogger>(),
                        s.GetService<IMailTemplateManager>(),
                        s.GetService<ITemplateMerge>()))
                .AddScoped<IEmailServiceManager, EmailServiceManager>(s =>
                    new EmailServiceManager(this.Configuration, s.GetService<IRepositoryFactory>(), s.GetService<ICloudStorageClient>(), s.GetService<ILogger>(),
                    s.GetService<INotificationProviderFactory>(), s.GetService<IEmailManager>()))
                .AddScoped<ITemplateMerge, TemplateMerge>()
                .AddSingleton<IEmailAccountManager, EmailAccountManager>()
                .AddScoped<INotificationProviderFactory, NotificationProviderFactory>();

            NotificationProviderType providerType = (NotificationProviderType)Enum.Parse(typeof(NotificationProviderType), this.Configuration[ConfigConstants.NotificationProviderType]);

            if (NotificationProviderType.DirectSend == providerType)
            {
                this.ConfigureDirectSendServices(services);
            }
            else
            {
                this.ConfigureGraphServices(services);
            }
        }

        /// <summary>
        /// Configure Graph services.
        /// </summary>
        /// <param name="services">IServiceCollection instance.</param>
        private void ConfigureGraphServices(IServiceCollection services)
        {
            _ = services.Configure<MSGraphSetting>(this.Configuration.GetSection(ConfigConstants.MSGraphSettingConfigSectionKey));
            _ = services.Configure<MSGraphSetting>(s => s.ClientCredential = this.Configuration[ConfigConstants.MSGraphSettingClientCredentialConfigKey]);
            _ = services.Configure<MSGraphSetting>(s => s.ClientId = this.Configuration[ConfigConstants.MSGraphSettingClientIdConfigKey]);
            _ = services.AddHttpClient<IMSGraphProvider, MSGraphProvider>();
            _ = services.AddScoped<MSGraphNotificationProvider>(s => new MSGraphNotificationProvider(this.Configuration, s.GetService<IEmailAccountManager>(), s.GetService<ILogger>(),
            Options.Create(this.Configuration.GetSection(ConfigConstants.MSGraphSettingConfigSectionKey).Get<MSGraphSetting>()), s.GetService<ITokenHelper>(), s.GetService<IMSGraphProvider>(), s.GetService<IEmailManager>()))
            .AddScoped<INotificationProvider, MSGraphNotificationProvider>();
        }

        /// <summary>
        /// Configure Direct Send Services.
        /// </summary>
        /// <param name="services">IServiceCollection instance.</param>
        private void ConfigureDirectSendServices(IServiceCollection services)
        {
            _ = services.AddSingleton<SendAccountConfiguration>(new SendAccountConfiguration()
            {
                DisplayName = this.Configuration[ConfigConstants.DirectSendDisplayNameConfigKey],
            });

            if (int.TryParse(this.Configuration[ConfigConstants.DirectSendSMTPPortConfigKey], out int port))
            {
                _ = services.AddSingleton<ISmtpConfiguration>(new SmtpConfiguration()
                {
                    SmtpPort = port,
                    SmtpServer = this.Configuration[ConfigConstants.DirectSendSMTPServerConfigKey]
                });
            }

            _ = services.AddSingleton<ISmtpClientFactory, DSSmtpClientFactory>()
                .AddSingleton<ISmtpClientPool, SmtpClientPool>()
                .AddSingleton<IEmailService, DirectSendMailService>()
                .AddScoped<DirectSendNotificationProvider>(s => new DirectSendNotificationProvider(this.Configuration, s.GetService<IEmailService>(), s.GetService<ILogger>(), s.GetService<IEmailManager>()))
                .AddScoped<INotificationProvider, DirectSendNotificationProvider>();

        }
        /// <summary>
        /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </summary>
        /// <param name="app">An instance of <see cref="IApplicationBuilder"/>.</param>
        /// <param name="env">An instance of <see cref="IWebHostEnvironment"/>.</param>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            ConfigureCommon(app, env);

            _ = app.UseEndpoints(endpoints =>
            {
                _ = endpoints.MapControllers();
            });

            _ = app.UseSwagger();
            _ = app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "NotificationService");
            });
        }
    }
}
