// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Internal;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Management;
using System;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    internal class ReactivationScheduler : IReactivationScheduler
    {
        private readonly EntityActionScheduler<DestinationInfo> _scheduler;

        public ReactivationScheduler(ISystemClock clock)
        {
            _scheduler = new EntityActionScheduler<DestinationInfo>(Reactivate, true, clock);
        }

        public void ScheduleRestoringAsHealthy(DestinationInfo destination, TimeSpan reactivationPeriod)
        {
            _scheduler.ScheduleEntity(destination, reactivationPeriod);
        }

        public void Dispose()
        {
            _scheduler.Dispose();
        }

        private void Reactivate(DestinationInfo destination)
        {
            destination.DynamicStateSignal.Value = new DestinationDynamicState(destination.DynamicState.Health.ChangePassive(DestinationHealth.Healthy));
        }
    }
}
