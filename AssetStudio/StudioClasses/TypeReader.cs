using System;
using AssetStudio.Extensions;
using dnlib.DotNet;

namespace AssetStudio.StudioClasses
{
    public class TypeReader
    {
        public static object ReadAlignedPrimitiveValue(ObjectReader reader, TypeSig typeSig)
        {
            object value;

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

            reader.AlignStream();

            return value;
        }
    }
}