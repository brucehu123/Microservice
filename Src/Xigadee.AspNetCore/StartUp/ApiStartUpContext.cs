﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace Xigadee
{

    /// <summary>
    /// This is the default start up context.
    /// </summary>
    public class ApiStartUpContext : IApiStartupContext
    {
        #region Constructor
        /// <summary>
        /// This is the default construct that creates the directive class.
        /// </summary>
        public ApiStartUpContext()
        {
            Directives = new ContextDirectives(this);
        } 
        #endregion
        #region Directives
        /// <summary>
        /// This collection contains the list of repository directives for the context.
        /// This can be used to populate the repositories and run time from a central method.
        /// Useful when you want to set as memory backed for testing.
        /// </summary>
        public ContextDirectives Directives { get; }
        #endregion

        #region CXA => Initialize(IHostingEnvironment env)
        /// <summary>
        /// Initializes the context.
        /// </summary>
        /// <param name="env">The hosting environment.</param>
        public virtual void Initialize(Microsoft.AspNetCore.Hosting.IHostingEnvironment env)
        {
            Environment = env;

            Build();
            Bind();
        }
        #endregion
        #region 1.Build()
        /// <summary>
        /// Builds and sets the default configuration using the appsettings.json file and the appsettings.{Environment.EnvironmentName}.json file.
        /// </summary>
        protected virtual void Build()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Environment.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddJsonFile($"appsettings.{Environment.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();
        }
        #endregion
        #region 2.Bind()
        /// <summary>
        /// Creates and binds specific configuration components required by the application.
        /// </summary>
        protected virtual void Bind()
        {
            if (!string.IsNullOrWhiteSpace(BindNameConfigApplication))
            {
                ConfigApplication = new ConfigApplication();
                Configuration.Bind(BindNameConfigApplication, ConfigApplication);

                ConfigApplication.Connections = Configuration.GetSection("ConnectionStrings").GetChildren().ToDictionary((e) => e.Key, (e) => e.Value);
            }

            if (UseMicroservice)
            {
                ConfigMicroservice = new ConfigMicroservice();

                if (!string.IsNullOrEmpty(BindNameConfigMicroservice))
                    Configuration.Bind(BindNameConfigMicroservice, ConfigMicroservice);

            }
        }
        #endregion

        #region CXB => ModulesCreate(IServiceCollection services)
        /// <summary>
        /// Connects the application components and registers the relevant services.
        /// </summary>
        /// <param name="services">The services.</param>
        public virtual void ModulesCreate(IServiceCollection services)
        {
        }
        #endregion

        #region CXC => Connect(ILoggerFactory lf)
        /// <summary>
        /// Connects the application components and registers the relevant services.
        /// </summary>
        /// <param name="lf">The logger factory.</param>
        public virtual void Connect(ILoggerFactory lf)
        {
            Logger = lf.CreateLogger<IApiStartupContext>();
        }
        #endregion

        #region Environment
        /// <summary>
        /// Gets or sets the hosting environment.
        /// </summary>
        public virtual Microsoft.AspNetCore.Hosting.IHostingEnvironment Environment { get; set; }
        #endregion
        #region Configuration
        /// <summary>
        /// Gets or sets the application configuration.
        /// </summary>
        public virtual IConfiguration Configuration { get; set; }
        #endregion
        #region Logger
        /// <summary>
        /// Gets or sets the logger.
        /// </summary>
        public virtual ILogger Logger { get; set; }
        #endregion

        #region UseMicroservice
        /// <summary>
        /// Gets a value indicating whether the service should create an initialize a Microservice Pipeline
        /// </summary>
        public virtual bool UseMicroservice => true;
        #endregion
        #region ConfigMicroservice
        /// <summary>
        /// Gets or sets the microservice configuration.
        /// </summary>
        public virtual ConfigMicroservice ConfigMicroservice { get; set; }
        /// <summary>
        /// Gets the bind section for ConfigMicroservice.
        /// </summary>
        protected virtual string BindNameConfigMicroservice => "ConfigMicroservice";
        #endregion
        #region ConfigApplication
        /// <summary>
        /// Gets or sets the microservice configuration.
        /// </summary>
        public virtual ConfigApplication ConfigApplication { get; set; }
        /// <summary>
        /// Gets the bind section for ConfigMicroservice.
        /// </summary>
        protected virtual string BindNameConfigApplication => "ConfigApplication";
        #endregion
    }
}