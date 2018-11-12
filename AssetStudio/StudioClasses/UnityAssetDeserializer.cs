using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;

namespace AssetStudio.StudioClasses
{
    public partial class UnityAssetSerializer
    {
        public static readonly ModuleDef UnityEngineModuleDef = new ModuleDefUser("UnityEngine.dll");

        //public static readonly TypeRef ScriptableObjectTypeDef = new TypeRefUser(UnityEngineModuleDef, "UnityEngine", "ScriptableObject");

        //public static readonly TypeRef MonoBehaviourTypeDef = new TypeRefUser(UnityEngineModuleDef, "UnityEngine", "MonoBehaviour");

        public delegate object Deserializer(EndianBinaryReader reader, AssetsFile assetsFile);

        public TypeDef TypeDef
        {
            get;
        }

        public IEnumerable<FieldDef> SerializedFields
        {
            get;
        }

        public UnityAssetSerializer(TypeSig typeSig) : this(typeSig.TryGetTypeDef())
        {
        }

        public UnityAssetSerializer(ITypeDefOrRef typeRef) : this(typeRef.ResolveTypeDef())
        {
        }

        public UnityAssetSerializer(TypeDef typeDef)
        {
            this.TypeDef = typeDef ?? throw new ArgumentNullException(nameof(typeDef));

            this.SerializedFields = GetSerializedFields(typeDef).ToArray();
        }

        public static bool IsScriptableObjectOrMonoBehaviour(ITypeDefOrRef typeRef)
        {
            if (typeRef == null)
            {
                throw new ArgumentException(nameof(typeRef));
            }

            do
            {
                TypeDef typeDef = typeRef.ResolveTypeDef();
                if (typeDef == null)
                {
                    throw new NotImplementedException("Could not resolve type definition");
                }

                switch (typeDef.FullName)
                {
                    case "UnityEngine.MonoBehaviour":
                    case "UnityEngine.ScriptableObject":
                        return true;
                    default:
                        typeRef = typeDef.BaseType;
                        break;
                }
            }
            while (typeRef != null);

            return false;
        }

        // @formatter:off
        private static bool IsSerializedType(TypeSig typeSig)
            => SimpleDeserializers.ContainsKey(typeSig.FullName)
                || IsScriptableObjectOrMonoBehaviour(typeSig.ToTypeDefOrRef());
        // @formatter:on

        private static TypeSig Resolve(TypeSig typeSig)
            => typeSig.ToTypeDefOrRef().ResolveTypeDef().ToTypeSig();

        public static IEnumerable<FieldDef> GetSerializedFields(TypeDef typeDef)
        {
            if (typeDef == null)
            {
                throw new ArgumentNullException(nameof(typeDef));
            }

            // base type fields come first
            if (typeDef.BaseType != null)
            {
                foreach (FieldDef field in GetSerializedFields(typeDef.BaseType.ResolveTypeDef()))
                {
                    yield return field;
                }
            }

            foreach (FieldDef field in typeDef.Fields)
            {
                if (field.IsStatic || field.IsInitOnly || field.IsNotSerialized || field.IsLiteral)
                {
                    continue;
                }

                string typeName = field.FieldType.FullName;

                bool isSerialized = field.IsPublic;

                if (!isSerialized)
                {
                    CustomAttributeCollection attrs = field.CustomAttributes;
                    CustomAttribute serializedField = attrs?.Find("UnityEngine.SerializeField");
                    isSerialized = serializedField != null;
                }

                if (!isSerialized)
                {
                    continue;
                }

                TypeSig typeSig = field.FieldType;

                if (field.FieldType.IsArray)
                {
                    // resolve array element type
                    typeSig = Resolve(typeSig);
                }

                if (IsSerializedType(typeSig))
                {
                    yield return field;
                }
                else
                {
                    // TODO: maybe break down into simple fields?
                    throw new NotImplementedException($"Need to implement {typeName}");
                }
            }
        }

        private static Deserializer AsArray(Deserializer func)
        {
            return (reader, assets) =>
            {
                //reader.AlignStream(4 8 16); //TODO: alignment?
                int size = reader.ReadInt32();
                var array = new object[size];
                for (var i = 0; i < size; ++i)
                {
                    array[i] = func(reader, assets);
                }

                return array;
            };
        }

        public static IEnumerable<Deserializer> GetDeserializerSequence(IEnumerable<FieldDef> fields)
        {
            foreach (FieldDef field in fields)
            {
                TypeSig typeSig = field.FieldType;

                bool isArray = typeSig.IsArray;

                if (isArray)
                {
                    typeSig = Resolve(typeSig);
                }

                string typeName = typeSig.FullName;

                Deserializer func = null;

                if (!SimpleDeserializers.TryGetValue(typeName, out func))
                {
                    if (IsScriptableObjectOrMonoBehaviour(typeSig.ToTypeDefOrRef()))
                    {
                        func = (reader, assets) => assets.ReadPPtr();
                    }
                }

                if (func == null)
                {
                    // TODO: maybe break down into simple fields?
                    throw new NotImplementedException($"Need to implement {typeName}");
                }

                if (isArray)
                {
                    func = AsArray(func);
                }

                yield return func;
            }
        }

        public void Deserialize(EndianBinaryReader reader)
        {
        }
    }
}