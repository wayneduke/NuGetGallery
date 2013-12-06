using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using NuGet.Services.Azure;
using NuGet.Services.Jobs.Monitoring;

namespace NuGet.Services.Jobs
{
    public class JobsWorkerRole : NuGetWorkerRole
    {
        protected override IEnumerable<NuGetService> CreateServices(NuGetServiceHost host)
        {
            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                // One worker per proc
                yield return new JobsService(host);
            }

            // One HTTP worker
            yield return new JobStatusHttpService(host);
        }
    }
}