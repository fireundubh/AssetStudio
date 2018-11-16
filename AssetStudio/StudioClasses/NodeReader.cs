using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using AssetStudio.Extensions;
using dnlib.DotNet;
using static AssetStudio.Logging.LoggingHelper;

namespace AssetStudio.StudioClasses
{
    public static class NodeReader
    {
        public static IEnumerator<TreeNode> DumpNode(TreeNode rootNode, ObjectReader reader, TypeSig typeSig, string name, bool isArray = false, int arrayIndex = 0)
        {
            bool isRoot = rootNode == null;
            TypeDef typeDef = typeSig.ToTypeDefOrRef().ResolveTypeDefThrow();

            // TypeReader equivalent: ReadPrimitiveValue
            if (typeSig.IsPrimitive)
            {
                object value = TypeReader.ReadAlignedPrimitiveValue(reader, typeSig);

                CreateValueNode(rootNode, typeDef, typeSig, name, value, false, isArray, arrayIndex, out TreeNode node);

                yield return node;
                yield break;
            }

            // TypeReader equivalent: ReadStringValue
            if (typeSig.FullName == "System.String")
            {
                string value = reader.ReadAlignedString();

                CreateValueNode(rootNode, typeDef, typeSig, name, string.Concat("\"", value, "\""), false, isArray, arrayIndex, out TreeNode node);

                yield return node;
                yield break;
            }

            // TODO: add type support
            if (typeDef.FullName == "UnityEngine.Font")
            {
                LogWarn($"typeSig.FullName == \"{typeSig.FullName}\"");
                yield break;
            }

            // TODO: add type support
            if (typeDef.FullName == "UnityEngine.GUIStyle")
            {
                LogWarn($"typeSig.FullName == \"{typeSig.FullName}\"");
                yield break;
            }

            // TODO: add type support, or okay to skip?
            if (typeSig.FullName == "System.Object")
            {
                LogWarn("typeSig.FullName == \"System.Object\"");
                yield break;
            }

            // TODO: add type support, or okay to skip?
            if (typeDef.IsDelegate)
            {
                LogWarn("typeDef.IsDelegate");
                yield break;
            }

            // TypeReader equivalent: ReadArraySigBaseValue
            if (typeSig is ArraySigBase)
            {
                foreach (TreeNode node in NodeDumpArray(rootNode, reader, typeDef, typeSig, name).AsEnumerable())
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
                    LogWarn("genericInstSig.GenericArguments.Count != 1");
                    yield break;
                }

                TypeSig genTypeSig = genericInstSig.GenericArguments[0];
                TypeDef type = genTypeSig.ToTypeDefOrRef().ResolveTypeDefThrow();

                foreach (TreeNode node in NodeDumpArray(rootNode, reader, type, genTypeSig, name).AsEnumerable())
                {
                    yield return node;
                }

                yield break;
            }

            // TypeReader equivalent: GetUnityObjectPPtrValue
            if (!isRoot && ScriptHelper.IsAssignFromUnityObject(typeDef))
            {
                PPtr pptr = reader.ReadPPtr();

                CreateValueNode(rootNode, typeDef, typeSig, name, pptr.ID, false, isArray, arrayIndex, out TreeNode node);

                yield return node;
                yield break;
            }

            // TypeReader equivalent: ReadEnumValue
            if (typeDef.IsEnum)
            {
                uint value = reader.ReadUInt32();

                CreateValueNode(rootNode, typeDef, typeSig, name, value, isRoot, isArray, arrayIndex, out TreeNode node);

                yield return node;
                yield break;
            }

            if (!isRoot && !ScriptHelper.IsEngineType(typeDef) && !typeDef.IsSerializable)
            {
                LogWarn("!isRoot && !Studio.IsEngineType(typeDef) && !typeDef.IsSerializable");
                yield break;
            }

            // TypeReader equivalent: ReadRectValue
            if (typeDef.FullName == "UnityEngine.Rect")
            {
                float[] value = reader.ReadSingleArray(4);

                CreateValueNode(rootNode, typeDef, typeSig, name, value, isRoot, isArray, arrayIndex, out TreeNode node);

                yield return node;
                yield break;
            }

            // TypeReader equivalent: ReadLayerMaskValue
            if (typeDef.FullName == "UnityEngine.LayerMask")
            {
                int value = reader.ReadInt32();

                CreateValueNode(rootNode, typeDef, typeSig, name, value, isRoot, isArray, arrayIndex, out TreeNode node);

                yield return node;
                yield break;
            }

            // TypeReader equivalent: ReadGradientValue
            if (typeDef.FullName == "UnityEngine.Gradient")
            {
                if (reader.version[0] == 5 && reader.version[1] < 5)
                {
                    reader.Position += 68;
                }
                else if (reader.version[0] == 5 && reader.version[1] < 6)
                {
                    reader.Position += 72;
                }
                else
                {
                    reader.Position += 168;
                }

                CreateKeyNode(rootNode, typeDef, typeSig, name, isRoot, isArray, arrayIndex, out TreeNode node);

                yield return node;
                yield break;
            }

            // TypeReader equivalent: ReadRectOffsetName
            if (typeDef.FullName == "UnityEngine.RectOffset")
            {
                LogWarn($"typeDef.FullName == \"{typeDef.FullName}\"");

                int m_Left = reader.ReadInt32();
                int m_Right = reader.ReadInt32();
                int m_Top = reader.ReadInt32();
                int m_Bottom = reader.ReadInt32();

                CreateKeyNode(rootNode, typeDef, typeSig, name, isRoot, isArray, arrayIndex, out TreeNode node);

                yield return node;
                yield break;
            }

            // TypeReader equivalent: DumpClassOrValueType
            if (!typeDef.IsClass && !typeDef.IsValueType)
            {
                // TODO
                LogWarn("!typeDef.IsClass && !typeDef.IsValueType");
                yield break;
            }

            foreach (TreeNode node in DumpNodeObject(rootNode, reader, typeDef, name).AsEnumerable())
            {
                yield return node;
            }
        }

        private static TreeNode BuildNode(string name, string nodeText, ElementType tag)
        {
            return new TreeNode
            {
                Name = name,
                Text = nodeText,
                Tag = tag
            };
        }

        private static void CreateKeyNode(TreeNode rootNode, TypeDef typeDef, TypeSig typeSig, string name, bool isRoot, bool isArray, int arrayIndex, out TreeNode node)
        {
            string nodeText = !isArray ? string.Format("{0} {1}", typeDef.Name, name) : string.Format("[{0}] {1} {2}", arrayIndex, typeDef.Name, name);

            node = BuildNode(name, nodeText, typeSig.ElementType);

            if (!isRoot)
            {
                rootNode.Nodes.Add(node);
            }
        }

        private static void CreateValueNode(TreeNode rootNode, TypeDef typeDef, TypeSig typeSig, string name, object value, bool isRoot, bool isArray, int arrayIndex, out TreeNode node)
        {
            string nodeText = !isArray ? string.Format("{0} {1} = {2}", typeDef.Name, name, value) : string.Format("[{0}] {1} {2} = {3}", arrayIndex, typeDef.Name, name, value);

            node = BuildNode(name, nodeText, typeSig.ElementType);

            if (!isRoot)
            {
                rootNode.Nodes.Add(node);
            }
        }

        private static IEnumerator<TreeNode> NodeDumpArray(TreeNode rootNode, ObjectReader reader, TypeDef type, TypeSig typeSig, string name)
        {
            bool isRoot = rootNode == null;
            if (!type.IsEnum && !ScriptHelper.IsBaseType(type) && !ScriptHelper.IsAssignFromUnityObject(type) && !ScriptHelper.IsEngineType(type) && !type.IsSerializable)
            {
                yield break;
            }

            int size = reader.ReadInt32();

            string nodeText = string.Format("{0} {1}", typeSig.TypeName, name);

            TreeNode arrayNode = BuildNode(name, nodeText, typeSig.ElementType);

            if (!isRoot)
            {
                rootNode.Nodes.Add(arrayNode);
            }

            yield return arrayNode;

            TreeNode arraySizeNode = NodeHelper.AddKeyedChildNode(arrayNode, name, ref size, "int size = {0}");

            for (var i = 0; i < size; i++)
            {
                foreach (TreeNode node in DumpNode(arraySizeNode, reader, typeSig, "data", isArray: true, arrayIndex: i).AsEnumerable())
                {
                    yield return node;
                }
            }
        }

        private static IEnumerator<TreeNode> DumpNodeObject(TreeNode rootNode, ObjectReader reader, TypeDef typeDef, string name)
        {
            bool isRoot = rootNode == null;

            if (name != null)
            {
                string nodeText = $"{typeDef.Name} {name}";

                TreeNode node = BuildNode(name, nodeText, typeDef.ToTypeSig().ElementType);

                if (!isRoot)
                {
                    rootNode.Nodes.Add(node);
                }

                yield return node;
                rootNode = node;
            }

            if (isRoot && typeDef.BaseType.FullName != "UnityEngine.Object")
            {
                foreach (TreeNode node in DumpNode(rootNode, reader, typeDef.BaseType.ToTypeSig(), null, true).AsEnumerable())
                {
                    yield return node;
                }
            }

            if (!isRoot && typeDef.BaseType.FullName != "System.Object")
            {
                foreach (TreeNode node in DumpNode(rootNode, reader, typeDef.BaseType.ToTypeSig(), null, true).AsEnumerable())
                {
                    yield return node;
                }
            }

            foreach (TreeNode node in DumpNodeFields(rootNode, reader, typeDef).AsEnumerable())
            {
                yield return node;
            }
        }

        private static IEnumerator<TreeNode> DumpNodeFields(TreeNode rootNode, ObjectReader reader, TypeDef typeDef)
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

                    foreach (TreeNode node in DumpNode(rootNode, reader, fieldDef.FieldType, fieldDef.Name).AsEnumerable())
                    {
                        yield return node;
                    }
                }
                else if ((fieldDef.Attributes & FieldAttributes.Static) == 0 && (fieldDef.Attributes & FieldAttributes.InitOnly) == 0 && (fieldDef.Attributes & FieldAttributes.NotSerialized) == 0)
                {
                    foreach (TreeNode node in DumpNode(rootNode, reader, fieldDef.FieldType, fieldDef.Name).AsEnumerable())
                    {
                        yield return node;
                    }
                }
            }
        }
    }
}