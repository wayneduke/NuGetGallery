﻿using System;
using System.Reactive.Linq;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using NuGetGallery.Storage;
using NuGetGallery;

namespace NuGet.Services
{
    public abstract class NuGetService : IDisposable
    {
        private static int _nextId = 0;

        private AzureTable<ServiceInstance> _instancesTable;
        private const string TraceTableBaseName = "ServiceTrace";
        private SinkSubscription<WindowsAzureTableSink> _globalSinkSubscription;
        private ServiceInstance _serviceInstance;

        public string Name { get; private set; }
        public NuGetServiceHost Host { get; private set; }
        public ServiceConfiguration Configuration { get { return Host.Configuration; } }
        public StorageHub Storage { get { return Configuration.Storage; } }
        public string ServiceInstanceName { get; private set; }

        public string TempDirectory { get; protected set; }

        protected NuGetService(string serviceName, NuGetServiceHost host)
        {
            Name = serviceName;
            Host = host;
            Host.AttachService(this);

            _instancesTable = Host.Configuration.Storage.Primary.Tables.Table<ServiceInstance>();
            ServiceInstanceName = Host.Name + "_" + Name + "_" + ServiceInstanceId.Get().ToString();

            TempDirectory = Path.Combine(Path.GetTempPath(), "NuGetServices", serviceName);

            ServiceInstanceId.Set(Interlocked.Increment(ref _nextId) - 1);
        }

        public virtual async Task<bool> Start()
        {
            ServicePlatformEventSource.Log.Starting(Name, ServiceInstanceName);
            if (Host == null)
            {
                throw new InvalidOperationException(Strings.NuGetService_HostNotSet);
            }
            Host.ShutdownToken.Register(OnShutdown);

            await StartTracing();

            var ret = await OnStart();
            ServicePlatformEventSource.Log.Started(Name, ServiceInstanceName);
            return ret;
        }

        public virtual async Task Run()
        {
            ServicePlatformEventSource.Log.Running(Name, ServiceInstanceName);
            if (Host == null)
            {
                throw new InvalidOperationException(Strings.NuGetService_HostNotSet);
            }
            await OnRun();
            ServicePlatformEventSource.Log.Stopped(Name, ServiceInstanceName);
        }

        public void Dispose()
        {
            if (_globalSinkSubscription != null)
            {
                _globalSinkSubscription.Dispose();
            }
        }

        public virtual Task Heartbeat()
        {
            _serviceInstance.LastHeartbeat = DateTimeOffset.UtcNow;
            return _instancesTable.InsertOrReplace(_serviceInstance);
        }

        protected virtual Task<bool> OnStart() { return Task.FromResult(true); }
        protected virtual void OnShutdown() { }
        protected abstract Task OnRun();

        protected virtual IEnumerable<EventSource> GetTraceEventSources()
        {
            return Enumerable.Empty<EventSource>();
        }

        private async Task StartTracing()
        {
 	        // Set up worker logging
            var listener = new ObservableEventListener();
            var capturedId = ServiceInstanceId.Get();
            var stream = listener.Where(_ => ServiceInstanceId.Get() == capturedId);
            foreach (var source in GetTraceEventSources())
            {
                listener.EnableEvents(source, EventLevel.Informational);
            }
            listener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.Informational);
            listener.EnableEvents(ServicePlatformEventSource.Log, EventLevel.Informational);
            _globalSinkSubscription = stream.LogToWindowsAzureTable(
                ServiceInstanceName,
                Storage.Primary.ConnectionString,
                tableAddress: Storage.Primary.Tables.GetTableFullName(Name + TraceTableBaseName));

            // Log Instance Status
            _serviceInstance = new ServiceInstance(
                Host.Name,
                ServiceInstanceName,
                Name,
                Environment.MachineName,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                AssemblyInformation.ForAssembly(GetType().Assembly));
            await _instancesTable.InsertOrReplace(_serviceInstance);
        }

        private static class ServiceInstanceId
        {
            private const string RunnerIdDataName = "_NuGet_Service_Instance_Id";

            public static int Get()
            {
                return (int)CallContext.LogicalGetData(RunnerIdDataName);
            }

            public static void Set(int id)
            {
                CallContext.LogicalSetData(RunnerIdDataName, id);
            }
        }
    }
}