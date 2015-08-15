﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NWheels.Extensions
{
    public static class ExceptionExtensions
    {
        public static string GetMessageDeep(this Exception exception)
        {
            var text = new StringBuilder();
            text.Append(exception.Message);

            for ( Exception inner = exception.InnerException ; inner != null ; inner = inner.InnerException )
            {
                text.Append(" -> ");
                text.Append(inner.Message);
            }

            var aggregate = exception as AggregateException;

            if ( aggregate != null )
            {
                foreach ( var inner in aggregate.InnerExceptions )
                {
                    text.Append(" -> ");
                    text.Append(GetMessageDeep(inner));
                }
            }

            return text.ToString();
        }
    }
}
