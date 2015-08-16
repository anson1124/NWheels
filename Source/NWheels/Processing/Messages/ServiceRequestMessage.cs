﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace NWheels.Processing.Messages
{
    public class ServiceRequestMessage<TService, TRequest> : MessageObjectBase
    {
        private readonly MessageActionHeader _actionHeader;
        private readonly MessageIdHeader _idHeader;
        private readonly TRequest _request;

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public ServiceRequestMessage(IFramework framework, Expression<Func<TService, Action<TRequest>>> operationSelector, TRequest request)
        {
            _request = request;
            _actionHeader = MessageActionHeader.FromMethod(operationSelector);
            _idHeader = new MessageIdHeader(framework.NewGuid());
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public override IReadOnlyCollection<IMessageHeader> Headers
        {
            get
            {
                return new IMessageHeader[] {
                    _actionHeader,
                    _idHeader
                };
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public override object Body
        {
            get
            {
                return _request;
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public TRequest Request
        {
            get
            {
                return _request;
            }
        }
    }
}
