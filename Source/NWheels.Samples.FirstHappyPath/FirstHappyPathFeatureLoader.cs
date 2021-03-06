﻿using NWheels.Microservices;
using NWheels.Injection;
using System;

namespace NWheels.Samples.FirstHappyPath
{
    [DefaultFeatureLoader]
    public class FirstHappyPathFeatureLoader : FeatureLoaderBase
    {
        public override void ContributeComponents(IComponentContainerBuilder containerBuilder)
        {
            containerBuilder.Register<ILifecycleListenerComponent, FirstLifecycleListenerComponent>();
        }

        public override void CompileComponents(IInternalComponentContainer input)
        {
        }

        public override void ContributeCompiledComponents(IInternalComponentContainer input, IComponentContainerBuilder output)
        {
            output.Register<IDisposable, FirstHappyPathCompiler>();
        }

        public class FirstHappyPathCompiler : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
