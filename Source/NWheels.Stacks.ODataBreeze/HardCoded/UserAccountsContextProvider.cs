﻿#if false

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Breeze.ContextProvider;
using Hapil;
using Microsoft.Data.Edm;
using Microsoft.Data.Edm.Csdl;
using Microsoft.Data.Edm.Library;
using NWheels.DataObjects;
using NWheels.Domains.Security;
using NWheels.Extensions;
using NWheels.Entities;
using NWheels.Entities.Core;

namespace NWheels.Stacks.ODataBreeze.HardCoded
{
    public class UserAccountsContextProvider : ContextProvider
    {
        private readonly IFramework _framework;
        private readonly ITypeMetadataCache _metadataCache;
        private readonly IUserAccountDataRepository _querySource;

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public UserAccountsContextProvider(IFramework framework, ITypeMetadataCache metadataCache)
        {
            _framework = framework;
            _metadataCache = metadataCache;
            _querySource = framework.NewUnitOfWork<IUserAccountDataRepository>(autoCommit: false);
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public string GetRepositoryMetadataString(bool fullEdmx)
        {
            var builder = new EdmModelBuilder(_metadataCache);

            builder.AddEntity(_metadataCache.GetTypeMetadata(typeof(IUserAccountEntity)));
            builder.AddEntity(_metadataCache.GetTypeMetadata(typeof(IUserRoleEntity)));
            builder.AddEntity(_metadataCache.GetTypeMetadata(typeof(IOperationPermissionEntity)));
            builder.AddEntity(_metadataCache.GetTypeMetadata(typeof(IEntityAccessRuleEntity)));

            return builder.GetModelXmlString();
        }
        
        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public IUserAccountDataRepository QuerySource
        {
            get { return _querySource; }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        protected override string BuildJsonMetadata()
        {
            var builder = new BreezeMetadataBuilder(_metadataCache);

            builder.AddDataService("rest/UserAccounts/");
            builder.AddEntity(typeof(IUserAccountEntity));
            builder.AddEntity(typeof(IFrontEndUserAccountEntity));
            builder.AddEntity(typeof(IBackEndUserAccountEntity));

            return builder.GetMetadataJsonString();
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        protected override void CloseDbConnection()
        {
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public override System.Data.IDbConnection GetDbConnection()
        {
            return null;
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        protected override void OpenDbConnection()
        {
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        protected override void SaveChangesCore(SaveWorkState saveWorkState)
        {
            saveWorkState.KeyMappings = new List<KeyMapping>();

            using ( var data = _framework.NewUnitOfWork<IUserAccountDataRepository>() )
            {
                var entityRepositories = data.GetEntityRepositories().Where(repo => repo != null).ToDictionary(repo => repo.ImplementationType);

                foreach ( var typeGroup in saveWorkState.SaveMap )
                {
                    var entityImplementationType = typeGroup.Key;
                    var entityRepository = entityRepositories[entityImplementationType];

                    foreach ( var entityToSave in typeGroup.Value )
                    {
                        switch ( entityToSave.EntityState )
                        {
                            case EntityState.Added:
                                MapAutoGeneratedKey(saveWorkState, entityToSave, entityRepository);
                                entityRepository.Insert(entityToSave.Entity);
                                break;
                            case EntityState.Modified:
                                entityRepository.Update(entityToSave.Entity);
                                break;
                            case EntityState.Deleted:
                                entityRepository.Delete(entityToSave.Entity);
                                break;
                        }
                    }
                }

                data.CommitChanges();
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private void MapAutoGeneratedKey(SaveWorkState saveWorkState, EntityInfo entityToSave, IEntityRepository entityRepository)
        {
            if ( entityToSave.AutoGeneratedKey != null && entityToSave.AutoGeneratedKey.AutoGeneratedKeyType != AutoGeneratedKeyType.None )
            {
                entityToSave.AutoGeneratedKey.TempValue = EntityId.ValueOf(entityToSave.Entity);
                
                var tempEntity = entityRepository.New(); // will be initialized with auto-generated id
                var newEntityId = EntityId.ValueOf(tempEntity);
                
                ((IEntityObject)entityToSave.Entity).SetId(newEntityId);
                entityToSave.AutoGeneratedKey.RealValue = newEntityId;
                
                saveWorkState.KeyMappings.Add(new KeyMapping() {
                    EntityTypeName = BreezeMetadataBuilder.GetQualifiedTypeString(entityToSave.Entity.GetType()),
                    TempValue = entityToSave.AutoGeneratedKey.TempValue,
                    RealValue = entityToSave.AutoGeneratedKey.RealValue
                });
            }
        }
    }
}

#endif