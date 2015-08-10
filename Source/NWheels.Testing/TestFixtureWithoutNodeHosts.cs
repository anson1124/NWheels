﻿using System;
using Autofac;
using Hapil;
using NUnit.Framework;
using NWheels.DataObjects;
using NWheels.Entities;

namespace NWheels.Testing
{
    [TestFixture]
    public abstract class TestFixtureWithoutNodeHosts
    {
        private TestFramework _framework;

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [SetUp]
        public void BaseSetUp()
        {
            _framework = new TestFramework(CreateDynamicModule());
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [TearDown]
        public void BaseTearDown()
        {
            _framework = null;
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        protected T Resolve<T>() where T : class
        {
            return _framework.Components.Resolve<T>();
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        protected Auto<T> ResolveAuto<T>() where T : class
        {
            return _framework.Components.Resolve<Auto<T>>();
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        protected virtual DynamicModule CreateDynamicModule()
        {
            return TestFramework.DefaultDynamicModule;
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------
            
        protected TestFramework Framework
        {
            get
            {
                return _framework;
            }
        }
    }
}
