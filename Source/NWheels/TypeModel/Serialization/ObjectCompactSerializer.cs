﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using NWheels.Core;
using NWheels.DataObjects;
using NWheels.Extensions;
using NWheels.Processing.Messages;
using NWheels.TypeModel.Factories;

namespace NWheels.TypeModel.Serialization
{
    public class ObjectCompactSerializer
    {
        private readonly IComponentContext _components;
        private readonly ITypeMetadataCache _metadataCache;
        private readonly ObjectCompactReaderWriterFactory _readerWriterFactory;
        private readonly Pipeline<IObjectTypeResolver> _typeResolvers;

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public ObjectCompactSerializer(IComponentContext components, ITypeMetadataCache metadataCache, ObjectCompactReaderWriterFactory readerWriterFactory)
            : this(components, metadataCache, readerWriterFactory, new IObjectTypeResolver[] { new VoidTypeResolver() })
        {
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public ObjectCompactSerializer(
            IComponentContext components,
            ITypeMetadataCache metadataCache,
            ObjectCompactReaderWriterFactory readerWriterFactory,
            Pipeline<IObjectTypeResolver> typeResolvers)
        {
            _components = components;
            _metadataCache = metadataCache;
            _readerWriterFactory = readerWriterFactory;
            _typeResolvers = typeResolvers;
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public object ReadObject(Type declaredType, Stream input, ObjectCompactSerializerDictionary dictionary)
        {
            return ReadObject(declaredType, new CompactBinaryReader(input), dictionary);
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public object ReadObject(Type declaredType, byte[] input, ObjectCompactSerializerDictionary dictionary)
        {
            return ReadObject(declaredType, new CompactBinaryReader(new MemoryStream(input)), dictionary);
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public object ReadObject(Type declaredType, CompactBinaryReader input, ObjectCompactSerializerDictionary dictionary)
        {
            return ReadObject(declaredType, new CompactDeserializationContext(this, dictionary, input));
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public void WriteObject(Type declaredType, object obj, Stream output, ObjectCompactSerializerDictionary dictionary)
        {
            WriteObject(declaredType, obj, new CompactBinaryWriter(output), dictionary);
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public byte[] WriteObject(Type declaredType, object obj, ObjectCompactSerializerDictionary dictionary)
        {
            byte[] serializedBytes;

            using (var output = new MemoryStream())
            {
                WriteObject(declaredType, obj, new CompactBinaryWriter(output), dictionary);
                serializedBytes = output.ToArray();
            }

            return serializedBytes;
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public void WriteObject(Type declaredType, object obj, CompactBinaryWriter output, ObjectCompactSerializerDictionary dictionary)
        {
            WriteObject(declaredType, obj, new CompactSerializationContext(this, dictionary, output));
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        internal object ReadObject(Type declaredType, CompactDeserializationContext context)
        {
            var input = context.Input;
            var dictionary = context.Dictionary;

            var indicatorByte = input.ReadByte();
            Type serializedType;

            switch (indicatorByte)
            {
                case ObjectIndicatorByte.Null:
                    return null;
                case ObjectIndicatorByte.NotNull:
                    serializedType = declaredType;
                    break;
                case ObjectIndicatorByte.NotNullWithTypeKey:
                    var serializedTypeKey = input.ReadInt16();
                    serializedType = dictionary.LookupTypeOrThrow(serializedTypeKey, ancestor: declaredType);
                    break;
                default:
                    throw new InvalidDataException(string.Format("Input stream is invalid: object indicator byte={0}.", indicatorByte));
            }

            object materializedInstance;
            var materializer = TryFindMaterializingResolver(declaredType, serializedType);

            if (materializer != null)
            {
                materializedInstance = materializer.Materialize(declaredType, serializedType);
            }
            else
            {
                var materializationType = GetDeserializationType(declaredType, serializedType);
                var creator = _readerWriterFactory.GetDefaultCreator(materializationType);
                materializedInstance = creator(_components);
            }

            var reader = _readerWriterFactory.GetReader(materializedInstance.GetType());
            reader(context, materializedInstance);

            return materializedInstance;
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        internal void WriteObject(Type declaredType, object obj, CompactSerializationContext context)
        {
            var output = context.Output;
            var dictionary = context.Dictionary;

            if (obj == null)
            {
                output.Write(ObjectIndicatorByte.Null);
                return;
            }

            var resolvedSerializationType = GetSerializationType(declaredType, obj);

            int typeKey;
            if (dictionary.ShouldWriteTypeKey(obj, declaredType, resolvedSerializationType, out typeKey))
            {
                output.Write(ObjectIndicatorByte.NotNullWithTypeKey);
                output.Write((Int16)typeKey);
            }
            else
            {
                output.Write(ObjectIndicatorByte.NotNull);
            }

            var writer = _readerWriterFactory.GetWriter(obj.GetType());
            writer(context, obj);
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private Type GetSerializationType(Type declaredType, object obj)
        {
            for (int i = 0 ; i < _typeResolvers.Count ; i++)
            {
                var serializationType = _typeResolvers[i].GetSerializationType(declaredType, obj);

                if (serializationType != declaredType)
                {
                    return serializationType;
                }
            }

            return declaredType;
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private Type GetDeserializationType(Type declaredType, Type serializedType)
        {
            for (int i = 0; i < _typeResolvers.Count; i++)
            {
                var deserializationType = _typeResolvers[i].GetDeserializationType(declaredType, serializedType);

                if (deserializationType != declaredType)
                {
                    return deserializationType;
                }
            }

            return declaredType;
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private IObjectTypeResolver TryFindMaterializingResolver(Type declaredType, Type serializedType)
        {
            for (int i = 0; i < _typeResolvers.Count; i++)
            {
                if (_typeResolvers[i].CanMaterialize(declaredType, serializedType))
                {
                    return _typeResolvers[i];
                }
            }

            return null;
        }
        
        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private class VoidTypeResolver : IObjectTypeResolver
        {
            public Type GetSerializationType(Type declaredType, object obj)
            {
                return declaredType;
            }

            //-------------------------------------------------------------------------------------------------------------------------------------------------

            public Type GetDeserializationType(Type declaredType, Type serializedType)
            {
                return declaredType;
            }

            //-------------------------------------------------------------------------------------------------------------------------------------------------

            public bool CanMaterialize(Type declaredType, Type serializedType)
            {
                return false;
            }

            //-------------------------------------------------------------------------------------------------------------------------------------------------

            public object Materialize(Type declaredType, Type serializedType)
            {
                throw new NotSupportedException();
            }
        }
    }
}
