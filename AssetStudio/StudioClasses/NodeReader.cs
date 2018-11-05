using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using AssetStudio.Extensions;
using dnlib.DotNet;

namespace AssetStudio.StudioClasses
{
    public static class NodeReader
    {
        public static IEnumerator<TreeNode> DumpNode(TypeSig typeSig, AssetsFile assetsFile, string name, TreeNode rootNode, bool isArray = false, int arrayIndex = 0)
        {
            bool isRoot = rootNode == null;
            TypeDef typeDef = typeSig.ToTypeDefOrRef().ResolveTypeDefThrow();

            EndianBinaryReader reader = assetsFile.reader;

            if (Studio.IsExcludedType(typeDef, typeSig))
            {
                yield break;
            }

            if (typeSig.IsPrimitive)
            {
                object value = TypeReader.ReadAlignedPrimitiveValue(reader, typeSig);

                string nodeText = !isArray ? $"{typeDef.Name} {name} = {value}" : $"[{arrayIndex}] {typeDef.Name} {name} = {value}";

                var node = new TreeNode
                {
                    Name = name,
                    Text = nodeText,
                    Tag = typeSig.ElementType
                };
                
                if (!isRoot)
                {
                    rootNode.Nodes.Add(node);
                }

                yield return node;
                yield break;
            }

            if (typeSig.FullName == "System.String")
            {
                string str = reader.ReadAlignedString();

                string nodeText = !isArray ? $"{typeDef.Name} {name} = \"{str}\"" : $"[{arrayIndex}] {typeDef.Name} {name} = \"{str}\"";

                var node = new TreeNode
                {
                    Name = name,
                    Text = nodeText,
                    Tag = typeSig.ElementType
                };
                
                if (!isRoot)
                {
                    rootNode.Nodes.Add(node);
                }

                yield return node;
                yield break;
            }

            if (typeSig is ArraySigBase)
            {
                foreach (TreeNode node in NodeDumpArray(typeDef, typeSig, assetsFile, name, reader, rootNode).AsEnumerable())
                {
                    yield return node;
                }

                yield break;
            }

            if (!isRoot && typeSig is GenericInstSig genericInstSig)
            {
                if (genericInstSig.GenericArguments.Count != 1)
                {
                    // TODO
                    yield break;
                }
                
                TypeSig genTypeSig = genericInstSig.GenericArguments[0];
                TypeDef type = genTypeSig.ToTypeDefOrRef().ResolveTypeDefThrow();

                foreach (TreeNode node in NodeDumpArray(type, genTypeSig, assetsFile, name, reader, rootNode).AsEnumerable())
                {
                    yield return node;
                }

                yield break;
            }

            if (!typeDef.IsClass && !typeDef.IsValueType)
            {
                // TODO
                yield break;
            }

            foreach (TreeNode node in DumpNodeObject(assetsFile, name, typeDef, rootNode).AsEnumerable())
            {
                yield return node;
            }
        }

        private static IEnumerator<TreeNode> NodeDumpArray(TypeDef type, TypeSig typeSig, AssetsFile assetsFile, string name, EndianBinaryReader reader, TreeNode rootNode)
        {
            bool isRoot = rootNode == null;
            if (!type.IsEnum && !Studio.IsBaseType(type) && !Studio.IsAssignFromUnityObject(type) && !Studio.IsEngineType(type) && !type.IsSerializable)
            {
                yield break;
            }
            
            int size = reader.ReadInt32();

            string nodeText = $"{typeSig.TypeName} {name}";
                
            var arrayNode = new TreeNode
            {
                Name = name,
                Text = nodeText,
                Tag = typeSig.ElementType
            };
                
            if (!isRoot)
            {
                rootNode.Nodes.Add(arrayNode);
            }

            yield return arrayNode;

            TreeNode arraySizeNode = NodeHelper.AddKeyedChildNode(arrayNode, name, ref size, "int size = {0}");

            bool readArrayNode = typeSig.TypeName == "GUIStyle[]";

            if (readArrayNode)
            {
                yield break;
            }
                
            for (var i = 0; i < size; i++)
            {
                foreach (TreeNode node in DumpNode(typeSig, assetsFile, "data", arraySizeNode, isArray: true, arrayIndex: i).AsEnumerable())
                {
                    yield return node;
                }
            }
        }

        private static IEnumerator<TreeNode> DumpNodeObject(AssetsFile assetsFile, string name, TypeDef typeDef, TreeNode rootNode)
        {
            bool isRoot = rootNode == null;
            if (name != null)
            {
                string nodeText = $"{typeDef.Name} {name}";
                var node = new TreeNode
                {
                    Name = name,
                    Text = nodeText,
                    Tag = typeDef.ToTypeSig().ElementType
                };
                if (!isRoot)
                {
                    rootNode.Nodes.Add(node);
                }

                yield return node;
                rootNode = node;
            }

            if (isRoot && typeDef.BaseType.FullName != "UnityEngine.Object")
            {
                foreach (TreeNode node in DumpNode(typeDef.BaseType.ToTypeSig(), assetsFile, null, rootNode, true).AsEnumerable())
                {
                    yield return node;
                }
            }

            if (!isRoot && typeDef.BaseType.FullName != "System.Object")
            {
                foreach (TreeNode node in DumpNode(typeDef.BaseType.ToTypeSig(), assetsFile, null, rootNode, true).AsEnumerable())
                {
                    yield return node;
                }
            }

            foreach (TreeNode node in DumpNodeFields(assetsFile, typeDef, rootNode).AsEnumerable())
            {
                yield return node;
            }
        }

        private static IEnumerator<TreeNode> DumpNodeFields(AssetsFile assetsFile, TypeDef typeDef, TreeNode rootNode)
        {
            foreach (FieldDef fieldDef in typeDef.Fields)
            {
                FieldAttributes access = fieldDef.Access & FieldAttributes.FieldAccessMask;

                if (access != FieldAttributes.Public)
                {
                    if (!fieldDef.CustomAttributes.Any(x => x.TypeFullName.Contains("SerializeField")))
                    {
                        continue;
                    }
                    
                    foreach (TreeNode node in DumpNode(fieldDef.FieldType, assetsFile, fieldDef.Name, rootNode).AsEnumerable())
                    {
                        yield return node;
                    }
                }
                else if ((fieldDef.Attributes & FieldAttributes.Static) == 0 && (fieldDef.Attributes & FieldAttributes.InitOnly) == 0 && (fieldDef.Attributes & FieldAttributes.NotSerialized) == 0)
                {
                    foreach (TreeNode node in DumpNode(fieldDef.FieldType, assetsFile, fieldDef.Name, rootNode).AsEnumerable())
                    {
                        yield return node;
                    }
                }
            }
        }
    }
}
