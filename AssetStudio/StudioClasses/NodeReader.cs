using System;
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

            // TypeReader equivalent: ReadPrimitiveValue
            if (typeSig.IsPrimitive)
            {
                ReadPrimitiveNode(rootNode, reader, typeDef, typeSig, name, isRoot, isArray, arrayIndex, out TreeNode node);

                yield return node;
                yield break;
            }

            // TypeReader equivalent: ReadStringValue
            if (typeSig.FullName == "System.String")
            {
                ReadStringNode(rootNode, reader, typeDef, typeSig, name, isRoot, isArray, arrayIndex, out TreeNode node);

                yield return node;
                yield break;
            }

            // skip
            if (typeSig.FullName == "System.Object")
            {
                yield break;
            }

            // skip
            if (typeDef.IsDelegate)
            {
                yield break;
            }

            // TypeReader equivalent: ReadArraySigBaseValue
            if (typeSig is ArraySigBase)
            {
                foreach (TreeNode node in NodeDumpArray(typeDef, typeSig, assetsFile, name, reader, rootNode).AsEnumerable())
                {
                    yield return node;
                }

                yield break;
            }

            // TypeReader equivalent: ReadGenericInstSigValue
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

            // TypeReader equivalent: GetUnityObjectPPtrValue
            if (!isRoot && Studio.IsAssignFromUnityObject(typeDef))
            {
                PPtr pptr = assetsFile.ReadPPtr();

                string nodeText = !isArray ? string.Format("{0} {1} = {2}", typeDef.Name, name, pptr.ID) : string.Format("[{0}] {1} {2} = {3}", arrayIndex, typeDef.Name, name, pptr.ID);

                var node = new TreeNode
                {
                    Name = name,
                    Text = nodeText,
                    Tag = typeSig.ElementType
                };

                rootNode.Nodes.Add(node);

                yield return node;
                yield break;
            }

            // TypeReader equivalent: ReadEnumValue
            if (typeDef.IsEnum)
            {
                uint value = reader.ReadUInt32();

                string nodeText = !isArray ? string.Format("{0} {1} = {2}", typeDef.Name, name, value) : string.Format("[{0}] {1} {2} = {3}", arrayIndex, typeDef.Name, name, value);

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

            if (!isRoot && !Studio.IsEngineType(typeDef) && !typeDef.IsSerializable)
            {
                yield break;
            }

            // TypeReader equivalent: ReadRectValue
            if (typeDef.FullName == "UnityEngine.Rect")
            {
                float[] value = reader.ReadSingleArray(4);

                string nodeText = !isArray ? string.Format("{0} {1} = {2}", typeDef.Name, name, value) : string.Format("[{0}] {1} {2} = {3}", arrayIndex, typeDef.Name, name, value);

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

            // TypeReader equivalent: ReadLayerMaskValue
            if (typeDef.FullName == "UnityEngine.LayerMask")
            {
                int value = reader.ReadInt32();

                string nodeText = !isArray ? string.Format("{0} {1} = {2}", typeDef.Name, name, value) : string.Format("[{0}] {1} {2} = {3}", arrayIndex, typeDef.Name, name, value);

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

            // TypeReader equivalent: ReadGradientValue
            if (typeDef.FullName == "UnityEngine.Gradient")
            {
                if (assetsFile.version[0] == 5 && assetsFile.version[1] < 5)
                {
                    reader.Position += 68;
                }
                else if (assetsFile.version[0] == 5 && assetsFile.version[1] < 6)
                {
                    reader.Position += 72;
                }
                else
                {
                    reader.Position += 168;
                }

                string nodeText = !isArray ? string.Format("{0} {1}", typeDef.Name, name) : string.Format("[{0}] {1} {2}", arrayIndex, typeDef.Name, name);

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

            // TypeReader equivalent: ReadRectOffsetName
            if (typeDef.FullName == "UnityEngine.RectOffset")
            {
                float left = reader.ReadSingle();
                float right = reader.ReadSingle();
                float top = reader.ReadSingle();
                float bottom = reader.ReadSingle();

                string nodeText = !isArray ? string.Format("{0} {1}", typeDef.Name, name) : string.Format("[{0}] {1} {2}", arrayIndex, typeDef.Name, name);

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

            // TypeReader equivalent: DumpClassOrValueType
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

        private static void ReadStringNode(TreeNode rootNode, EndianBinaryReader reader, TypeDef typeDef, TypeSig typeSig, string name, bool isRoot, bool isArray, int arrayIndex, out TreeNode node)
        {
            string str = reader.ReadAlignedString();

            string nodeText = !isArray ? $"{typeDef.Name} {name} = \"{str}\"" : $"[{arrayIndex}] {typeDef.Name} {name} = \"{str}\"";

            node = new TreeNode
            {
                Name = name,
                Text = nodeText,
                Tag = typeSig.ElementType
            };

            if (!isRoot)
            {
                rootNode.Nodes.Add(node);
            }
        }

        private static void ReadPrimitiveNode(TreeNode rootNode, EndianBinaryReader reader, TypeDef typeDef, TypeSig typeSig, string name, bool isRoot, bool isArray, int arrayIndex, out TreeNode node)
        {
            object value = TypeReader.ReadAlignedPrimitiveValue(reader, typeSig);

            string nodeText = !isArray ? $"{typeDef.Name} {name} = {value}" : $"[{arrayIndex}] {typeDef.Name} {name} = {value}";

            node = new TreeNode
            {
                Name = name,
                Text = nodeText,
                Tag = typeSig.ElementType
            };

            if (!isRoot)
            {
                rootNode.Nodes.Add(node);
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
