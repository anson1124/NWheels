﻿using NWheels.Compilation.Mechanism.Syntax.Members;
using NWheels.Compilation.Mechanism.Syntax.Statements;
using System;
using System.Collections.Generic;
using System.Text;

namespace NWheels.Compilation.Mechanism.Syntax.Expressions
{
    public class AnonymousDelegateExpression : AbstractExpression
    {
        public AnonymousDelegateExpression()
        {
            this.Body = new BlockStatement();
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public override void AcceptVisitor(StatementVisitor visitor)
        {
            visitor.VisitAnonymousDelegateExpression(this);
            Body.AcceptVisitor(visitor);
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public MethodSignature Signature { get; set; }
        public BlockStatement Body { get; }
    }
}
