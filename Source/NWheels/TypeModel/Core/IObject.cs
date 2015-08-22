﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NWheels.TypeModel.Core;

namespace NWheels.DataObjects.Core
{
    public interface IObject
    {
        Type ContractType { get; }
        Type FactoryType { get; }
        bool IsModified { get; }
    }
}
