using System;
using System.IO;
using System.Linq;
using System.Text;
using AssetStudio.Extensions;
using dnlib.DotNet;

namespace AssetStudio.StudioClasses
{
    public class TypeReader
    {
        private static string Indent(int indentDepth, int additionalDepth = 0)
        {
            return new string('\t', indentDepth + additionalDepth);
        }

        public static void DumpType(TypeSig typeSig, StringBuilder sb, ObjectReader reader, string name, int currentDepth, bool isRoot = false)
        {
            TypeDef typeDef = typeSig.ToTypeDefOrRef().ResolveTypeDefThrow();

//			if (ScriptHelper.IsExcludedType(typeDef, typeSig))
//			{
//				return;
//			}

            if (ReadPrimitiveValue(reader, typeDef, typeSig, sb, name, currentDepth))
            {
                return;
            }

            if (ReadStringValue(reader, typeDef, typeSig, sb, name, currentDepth))
            {
                return;
            }

            if (ReadArraySigBaseValue(reader, typeDef, typeSig, sb, name, currentDepth))
            {
                return;
            }

            if (ReadGenericInstSigValue(reader, typeSig, sb, name, currentDepth, isRoot))
            {
                return;
            }

            if (GetUnityObjectPPtrValue(typeDef, sb, reader, name, currentDepth))
            {
                return;
            }

            if (ReadEnumValue(reader, typeDef, sb, name, currentDepth))
            {
                return;
            }

            if (currentDepth != -1 && !ScriptHelper.IsEngineType(typeDef) && !typeDef.IsSerializable)
            {
                return;
            }

            if (ReadRectValue(reader, typeDef, sb, name, currentDepth))
            {
                return;
            }

            if (ReadLayerMaskValue(reader, typeDef, sb, name, currentDepth))
            {
                return;
            }

            if (ReadAnimationCurveValue(reader, typeDef, sb, name, currentDepth))
            {
                return;
            }

            if (ReadGradientValue(reader, typeDef, sb, name, currentDepth))
            {
                return;
            }

            if (ReadRectOffsetName(reader, typeDef, sb, name, currentDepth))
            {
                return;
            }

            DumpClassOrValueType(typeDef, sb, reader, name, currentDepth);
        }

        private static void DumpClassOrValueType(TypeDef typeDef, StringBuilder sb, ObjectReader reader, string name, int indentDepth)
        {
            if (!typeDef.IsClass && !typeDef.IsValueType)
            {
                return;
            }

            if (name != null && indentDepth != -1)
            {
                sb.AppendLine(string.Format("{0}{1} {2}", Indent(indentDepth), typeDef.Name, name));
            }

            if (indentDepth == -1 && typeDef.BaseType.FullName != "UnityEngine.Object")
            {
                DumpType(typeDef.BaseType.ToTypeSig(), sb, reader, null, indentDepth, true);
            }

            if (indentDepth != -1 && typeDef.BaseType.FullName != "System.Object")
            {
                DumpType(typeDef.BaseType.ToTypeSig(), sb, reader, null, indentDepth, true);
            }

            foreach (FieldDef fieldDef in typeDef.Fields)
            {
                FieldAttributes access = fieldDef.Access & FieldAttributes.FieldAccessMask;

                if (access != FieldAttributes.Public)
                {
                    if (fieldDef.CustomAttributes.Any(x => x.TypeFullName.Contains("SerializeField")))
                    {
                        DumpType(fieldDef.FieldType, sb, reader, fieldDef.Name, indentDepth + 1);
                    }
                }
                else if ((fieldDef.Attributes & FieldAttributes.Static) == 0 && (fieldDef.Attributes & FieldAttributes.InitOnly) == 0 && (fieldDef.Attributes & FieldAttributes.NotSerialized) == 0)
                {
                    DumpType(fieldDef.FieldType, sb, reader, fieldDef.Name, indentDepth + 1);
                }
            }
        }

        public static object ReadAlignedPrimitiveValue(ObjectReader reader, TypeSig typeSig)
        {
            object value = null;

            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (typeSig.TypeName)
            {
                case "Boolean":
                    value = reader.ReadBoolean();
                    break;
                case "Byte":
                    value = reader.ReadByte();
                    break;
                case "SByte":
                    value = reader.ReadSByte();
                    break;
                case "Int16":
                    value = reader.ReadInt16();
                    break;
                case "UInt16":
                    value = reader.ReadUInt16();
                    break;
                case "Int32":
                    value = reader.ReadInt32();
                    break;
                case "UInt32":
                    value = reader.ReadUInt32();
                    break;
                case "Int64":
                    value = reader.ReadInt64();
                    break;
                case "UInt64":
                    value = reader.ReadUInt64();
                    break;
                case "Single":
                    value = reader.ReadSingle();
                    break;
                case "Double":
                    value = reader.ReadDouble();
                    break;
                case "Char":
                    value = reader.ReadChar();
                    break;
                default:
                    throw new NotSupportedException(string.Format("Primitive value not supported: {0}", typeSig.TypeName));
            }

            reader.AlignStream(4);

            return value;
        }

        private static bool ReadPrimitiveValue(ObjectReader reader, TypeDef typeDef, TypeSig typeSig, StringBuilder sb, string name, int indentDepth)
        {
            if (!typeSig.IsPrimitive)
            {
                return false;
            }

            object value = ReadAlignedPrimitiveValue(reader, typeSig);

            sb.AppendLine(string.Format("{0}{1} {2} = {3}", Indent(indentDepth), typeDef.Name, name, value));

            return true;
        }

        private static bool ReadStringValue(ObjectReader reader, TypeDef typeDef, TypeSig typeSig, StringBuilder sb, string name, int indentDepth)
        {
            if (typeSig.FullName != "System.String")
            {
                return false;
            }

            string str = reader.ReadAlignedString();
            sb.AppendLine(string.Format("{0}{1} {2} = \"{3}\"", Indent(indentDepth), typeDef.Name, name, str));

            return true;
        }

        private static bool ReadArrayValue(ObjectReader reader, TypeSig typeSig, StringBuilder sb, string name, int indentDepth, out int size)
        {
            size = reader.ReadInt32();

            sb.AppendLine(string.Format("{0}{1} {2}", Indent(indentDepth), typeSig.TypeName, name));
            sb.AppendLine(string.Format("{0}int size = {1}", Indent(indentDepth, 1), size));

            if (typeSig.TypeName == "GUIStyle[]")
            {
                sb.AppendLine(string.Format("{0}<truncated>", Indent(indentDepth, 2)));
                return true;
            }

            return false;
        }

        private static bool ReadArraySigBaseValue(ObjectReader reader, TypeDef typeDef, TypeSig typeSig, StringBuilder sb, string name, int indentDepth)
        {
            if (!(typeSig is ArraySigBase))
            {
                return false;
            }

            if (!typeDef.IsEnum && !ScriptHelper.IsBaseType(typeDef) && !ScriptHelper.IsAssignFromUnityObject(typeDef) && !ScriptHelper.IsEngineType(typeDef) && !typeDef.IsSerializable)
            {
                return true;
            }

            if (ReadArrayValue(reader, typeSig, sb, name, indentDepth, out int size))
            {
                return true;
            }

            for (var i = 0; i < size; i++)
            {
                sb.AppendLine(string.Format("{0}[{1}]", Indent(indentDepth, 2), i));
                DumpType(typeDef.ToTypeSig(), sb, reader, "data", indentDepth + 3);
            }

            return true;
        }

        private static bool ReadGenericInstSigValue(ObjectReader reader, TypeSig typeSig, StringBuilder sb, string name, int indentDepth, bool isRoot)
        {
            if (isRoot || !(typeSig is GenericInstSig genericInstSig))
            {
                return false;
            }

            if (genericInstSig.GenericArguments.Count != 1)
            {
                return true;
            }

            TypeDef type = genericInstSig.GenericArguments[0].ToTypeDefOrRef().ResolveTypeDefThrow();

            if (!type.IsEnum && !ScriptHelper.IsBaseType(type) && !ScriptHelper.IsAssignFromUnityObject(type) && !ScriptHelper.IsEngineType(type) && !type.IsSerializable)
            {
                return true;
            }

            if (ReadArrayValue(reader, typeSig, sb, name, indentDepth, out int size))
            {
                return true;
            }

            for (var i = 0; i < size; i++)
            {
                sb.AppendLine(string.Format("{0}[{1}]", Indent(indentDepth, 2), i));
                DumpType(genericInstSig.GenericArguments[0], sb, reader, "data", indentDepth + 3);
            }

            return true;
        }

        private static bool GetUnityObjectPPtrValue(TypeDef typeDef, StringBuilder sb, ObjectReader reader, string name, int indentDepth)
        {
            if (indentDepth == -1 || !ScriptHelper.IsAssignFromUnityObject(typeDef))
            {
                return false;
            }

            PPtr pptr = reader.ReadPPtr();

            sb.AppendLine(string.Format("{0}PPtr<{1}> {2} = {3}", Indent(indentDepth), typeDef.Name, name, pptr.ID));

            if (pptr.TryGetGameObject(out GameObject gameObject))
            {
                if (gameObject.reader.type != ClassIDType.MonoBehaviour)
                {
                    return true;
                }
            }

            if (gameObject == null)
            {
                return true;
            }

            string script = ScriptHelper.GetScriptString(gameObject.reader, indentDepth, false);

            string text = Indent(indentDepth, 1) + script.Replace(Environment.NewLine, Environment.NewLine + Indent(indentDepth, 1));
            sb.Append(text);

            return true;
        }

        private static bool ReadEnumValue(ObjectReader reader, TypeDef typeDef, StringBuilder sb, string name, int indentDepth)
        {
            if (!typeDef.IsEnum)
            {
                return false;
            }

            sb.AppendLine(string.Format("{0}{1} {2} = {3}", Indent(indentDepth), typeDef.Name, name, reader.ReadUInt32()));

            return true;
        }

        private static bool ReadRectValue(ObjectReader reader, TypeDef typeDef, StringBuilder sb, string name, int indentDepth)
        {
            if (typeDef.FullName != "UnityEngine.Rect")
            {
                return false;
            }

            sb.AppendLine(string.Format("{0}{1} {2}", Indent(indentDepth), typeDef.Name, name));
            float[] rect = reader.ReadSingleArray(4);

            return true;
        }

        private static bool ReadLayerMaskValue(ObjectReader reader, TypeDef typeDef, StringBuilder sb, string name, int indentDepth)
        {
            if (typeDef.FullName != "UnityEngine.LayerMask")
            {
                return false;
            }

            sb.AppendLine(string.Format("{0}{1} {2}", Indent(indentDepth), typeDef.Name, name));

            int value = reader.ReadInt32();

            return true;
        }

        private static bool ReadAnimationCurveValue(ObjectReader reader, TypeDef typeDef, StringBuilder sb, string name, int indentDepth)
        {
            if (typeDef.FullName != "UnityEngine.AnimationCurve")
            {
                return false;
            }

            sb.AppendLine(string.Format("{0}{1} {2}", Indent(indentDepth), typeDef.Name, name));

            var animationCurve = new AnimationCurve<float>(reader, reader.ReadSingle);

            return true;
        }

        private static bool ReadGradientValue(ObjectReader reader, TypeDef typeDef, StringBuilder sb, string name, int indentDepth)
        {
            if (typeDef.FullName != "UnityEngine.Gradient")
            {
                return false;
            }

            sb.AppendLine(string.Format("{0}{1} {2}", Indent(indentDepth), typeDef.Name, name));

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

            return true;
        }

        private static bool ReadRectOffsetName(ObjectReader reader, TypeDef typeDef, StringBuilder sb, string name, int indentDepth)
        {
            if (typeDef.FullName != "UnityEngine.RectOffset")
            {
                return false;
            }

            sb.AppendLine(string.Format("{0}{1} {2}", Indent(indentDepth), typeDef.Name, name));

            float left = reader.ReadSingle();
            float right = reader.ReadSingle();
            float top = reader.ReadSingle();
            float bottom = reader.ReadSingle();

            return true;
        }
    }
}