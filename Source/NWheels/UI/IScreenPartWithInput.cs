﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NWheels.UI
{
    public interface IScreenPartWithInput<TInput>
    {
        string QualifiedName { get; }
    }
}
