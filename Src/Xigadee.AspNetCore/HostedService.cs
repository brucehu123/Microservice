﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xigadee;

namespace Xigadee
{
    /// <summary>
    /// This class host the Microservice within the AspNetCore application.
    /// </summary>
    /// <seealso cref = "Microsoft.Extensions.Hosting.IHostedService" />
    /// <seealso cref= "https://docs.microsoft.com/en-us/dotnet/standard/microservices-architecture/multi-container-microservice-net-applications/background-tasks-with-ihostedservice" />
    public class XigadeeHostedService: IHostedService, IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="XigadeeHostedService"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="serviceId">The service identifier.</param>
        /// <param name="description">The description.</param>
        /// <param name="policy">The policy.</param>
        /// <param name="properties">The properties.</param>
        /// <param name="config">The configuration.</param>
        /// <param name="assign">The assign.</param>
        /// <param name="configAssign">The configuration assign.</param>
        /// <param name="addDefaultJsonPayloadSerializer">if set to <c>true</c> [add default json payload serializer].</param>
        /// <param name="addDefaultPayloadCompressors">if set to <c>true</c> [add default payload compressors].</param>
        /// <param name="serviceVersionId">The service version identifier.</param>
        /// <param name="serviceReference">The service reference.</param>
        public XigadeeHostedService(string name = null
            , string serviceId = null
            , string description = null
            , IEnumerable<PolicyBase> policy = null
            , IEnumerable<Tuple<string, string>> properties = null
            , IEnvironmentConfiguration config = null
            , Action<IMicroservice> assign = null
            , Action<IEnvironmentConfiguration> configAssign = null
            , bool addDefaultJsonPayloadSerializer = true
            , bool addDefaultPayloadCompressors = true
            , string serviceVersionId = null
            , Type serviceReference = null)
        {
            Pipeline = new AspNetCoreMicroservicePipeline(name, serviceId, description, policy
                , properties, config, assign, configAssign
                , addDefaultJsonPayloadSerializer, addDefaultPayloadCompressors
                , serviceVersionId, serviceReference);
        }

        /// <summary>
        /// Gets the Microservice pipeline.
        /// </summary>
        public AspNetCoreMicroservicePipeline Pipeline { get; private set; }
        /// <summary>
        /// Gets the Microservice.
        /// </summary>
        public IMicroservice Service => Pipeline?.Service;
        /// <summary>
        /// Triggered when the application host is ready to start the service.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Returns the completed task.</returns>
        public virtual Task StartAsync(CancellationToken cancellationToken)
        {
            TryStart();

            // Otherwise it's running
            return Task.CompletedTask;
        }
        /// <summary>
        /// Triggered when the application host is performing a graceful shutdown.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns>A async completed task.</returns>
        public virtual Task StopAsync(CancellationToken cancellationToken)
        {

            try
            {
                // Signal cancellation to the executing method
                TryStop(false);
            }
            finally
            {
                // Wait until the task completes or the stop token triggers
                //await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite,cancellationToken));
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public virtual void Dispose()
        {
            TryStop(true);
        }
        /// <summary>
        /// Tries to start the service.
        /// </summary>
        /// <returns>Returns true if the service has started successfully.</returns>
        protected bool TryStart()
        {
            if (Pipeline.Service.Status != ServiceStatus.Created)
                return false;

            Pipeline.Service.Start();
            return true;
        }
        /// <summary>
        /// Tries to stop the service.
        /// </summary>
        /// <param name="force">if set to <c>true</c> [force].</param>
        /// <returns>Returns true if the service stops successfully.</returns>
        protected bool TryStop(bool force)
        {
            if (Pipeline.Service.Status != ServiceStatus.Running)
                return false;

            Pipeline.Service.Stop();

            return true;
        }
    }
}
