﻿using NWheels.DataObjects;
using NWheels.Entities;

namespace NWheels.Domains.DevOps.Alerts.Entities
{
    [EntityPartContract(IsAbstract=true)]
    public interface IEntityPartAlertAction
    {
        [PropertyContract.Calculated]
        string SummaryText { get; }
    }
}
