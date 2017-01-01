﻿using System;
using System.Collections.Generic;
using System.Text;

namespace NWheels.Api.Compilation.Syntax.Expressions
{
    public interface INewObjectExpression : IExpression
    {
        IReadOnlyList<IExpression> ConstructorArguments { get; }
    }
}
