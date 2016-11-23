﻿using System;
using Autofac;
using Hapil;
using NWheels.Configuration;
using NWheels.Configuration.Core;
using NWheels.DataObjects;
using NWheels.TypeModel.Core.Factories;

namespace NWheels.Conventions.Core
{
    internal class ConfigurationObjectFactory : ConventionObjectFactory, IConfigurationObjectFactory, IAutoObjectFactory
    {
        private readonly ITypeMetadataCache _metadataCache;
        private readonly IComponentContext _components;

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public ConfigurationObjectFactory(IComponentContext components, DynamicModule module, ITypeMetadataCache metadataCache)
            : base(module, ctx => new IObjectFactoryConvention[] {
                new ConfigurationObjectConvention(metadataCache),
                new ImplementIObjectConvention(implementIsModifiedAsFalse: true), 
            })
        {
            _metadataCache = metadataCache;
            _components = components;
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public TService CreateService<TService>() where TService : class
        {
            var sectionObject = base.CreateInstanceOf<TService>().UsingConstructor(
                (IConfigurationObjectFactory)this, 
                _components.Resolve<IConfigurationLogger>(), 
                string.Empty);

            return sectionObject;
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public TElement CreateConfigurationElement<TElement>(ConfigurationElementBase parent, string xmlElementName)
        {
            //TODO: add support of polymorphic collection items by determining element concrete type based on xmlElementName
            return base.CreateInstanceOf<TElement>().UsingConstructor(parent);
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public Type ServiceAncestorMarkerType
        {
            get
            {
                return typeof(IConfigurationSection);
            }
        }
    }
}
