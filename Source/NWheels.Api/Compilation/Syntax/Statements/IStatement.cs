﻿using NWheels.Api.Compilation.Syntax.Members;
using System;
using System.Collections.Generic;
using System.Text;

namespace NWheels.Api.Compilation.Syntax.Statements
{
    public interface IStatement
    {
        IBlockStatement ParentBlock { get; }
        IMethodMemberBase OwnerMethod { get; }
    }
}
