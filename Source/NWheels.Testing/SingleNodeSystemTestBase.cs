﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using NUnit.Framework;
using NWheels.DataObjects.Core.Conventions;
using NWheels.Entities;
using NWheels.Entities.Core;
using NWheels.Extensions;
using NWheels.Hosting;
using NWheels.Hosting.Core;
using NWheels.Logging.Core;
using NWheels.Testing.Entities.Impl;

namespace NWheels.Testing
{
    [TestFixture, Category(TestCategory.System)]
    public abstract class SingleNodeSystemTestBase
    {
        [SetUp]
        public void SetUp()
        {
            Thread.CurrentPrincipal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            StartNodeHost();
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [TearDown]
        public void TearDown()
        {
            StopNodeHost();
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        protected virtual void OnBuildingBootConfiguration(BootConfiguration configuration)
        {
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        protected virtual void OnRegisteringHostComponents(Autofac.ContainerBuilder builder)
        {
            builder.RegisterInstance(this).As<SingleNodeSystemTestBase>();

            builder.RegisterModule<NWheels.Stacks.Nlog.ModuleLoader>();
            builder.RegisterType<TestIntIdValueGenerator>().SingleInstance();
            builder.RegisterType<IntIdGeneratorMetadataConvention>().As<IntIdGeneratorMetadataConvention, IMetadataConvention>().SingleInstance().LastInPipeline();
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        protected virtual void OnRegisteringModuleComponents(Autofac.ContainerBuilder builder)
        {
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        protected virtual void StartNodeHost()
        {
            this.Node = new NodeHost(CreateNodeConfiguration(), OnRegisteringHostComponents);
            this.Node.LoadAndActivate();
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        protected virtual void StopNodeHost()
        {
            this.Node.DeactivateAndUnload();
            this.Node = null;
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        protected NodeHost Node { get; private set; }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        protected IFramework Framework { get; private set; }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        protected IComponentContext Components { get; private set; }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        protected virtual void OnInitializedStorage()
        {
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        protected abstract string StorageConnectionString { get; }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private BootConfiguration CreateNodeConfiguration()
        {
            NWheels.Stacks.Nlog.NLogBasedPlainLog.Instance.ConfigureConsoleOutput();

            var configuration = new BootConfiguration
            {
                ApplicationName = "TEST",
                EnvironmentName = "TEST",
                EnvironmentType = "DEV",
                NodeName = "TEST",
                InstanceId = "1",
                FrameworkModules = new List<BootConfiguration.ModuleConfig>(),
                ApplicationModules = new List<BootConfiguration.ModuleConfig> { 
                    new BootConfiguration.ModuleConfig {
                        Assembly = typeof(TestModule).Assembly.GetName().Name + ".dll",
                        LoaderClass = typeof(TestModule).FullName
                    }
                },
                ConfigFiles = new List<BootConfiguration.ConfigFile>(),
                LoadedFromDirectory = TestContext.CurrentContext.TestDirectory
            };

            OnBuildingBootConfiguration(configuration);

            configuration.Validate();
            return configuration;
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public class TestModule : Autofac.Module
        {
            private readonly SingleNodeSystemTestBase _ownerTest;

            //-------------------------------------------------------------------------------------------------------------------------------------------------

            public TestModule(SingleNodeSystemTestBase ownerTest)
            {
                _ownerTest = ownerTest;
            }

            //-------------------------------------------------------------------------------------------------------------------------------------------------

            protected override void Load(ContainerBuilder builder)
            {
                builder.NWheelsFeatures().Hosting().RegisterLifecycleComponent<TestComponent>();
                _ownerTest.OnRegisteringModuleComponents(builder);
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public class TestComponent : LifecycleEventListenerBase
        {
            private readonly SingleNodeSystemTestBase _ownerTest;

            //-------------------------------------------------------------------------------------------------------------------------------------------------

            public TestComponent(
                SingleNodeSystemTestBase ownerTest,
                IComponentContext components,
                IFramework framework,
                IStorageInitializer storageInitializer,
                Auto<IFrameworkDatabaseConfig> databaseConfig)
            {
                _ownerTest = ownerTest;

                _ownerTest.Components = components;
                _ownerTest.Framework = framework;

                databaseConfig.Instance.ConnectionString = _ownerTest.StorageConnectionString;

                storageInitializer.DropStorageSchema(databaseConfig.Instance.ConnectionString);
                storageInitializer.CreateStorageSchema(databaseConfig.Instance.ConnectionString, schemaVersion: 1);

                _ownerTest.OnInitializedStorage();
            }

            //-------------------------------------------------------------------------------------------------------------------------------------------------

            public override void NodeUnloaded()
            {
                _ownerTest.Components = null;
                _ownerTest.Framework = null;
            }
        }
    }
}
