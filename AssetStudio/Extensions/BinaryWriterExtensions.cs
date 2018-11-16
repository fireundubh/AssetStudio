using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AssetStudio.StudioClasses;

namespace AssetStudio.Extensions
{
    public static class BinaryWriterExtensions
    {
        private static void WriteArray<T>(Action<T> del, IEnumerable<T> array)
        {
            foreach (T item in array)
            {
                del(item);
            }
        }

        public static void Write(this BinaryWriter writer, uint[] array)
        {
            WriteArray(writer.Write, array);
        }

        public static void AlignStream(this BinaryWriter writer)
        {
            long pos = writer.BaseStream.Position;
            long mod = pos % Constants.ByteAlignment;

            if (mod != 0)
            {
                writer.Write(new byte[Constants.ByteAlignment - mod]);
            }
        }

        public static void WriteAlignedString(this BinaryWriter writer, string str)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(str);

            writer.Write(bytes.Length);
            writer.Write(bytes);
            writer.AlignStream();
        }
    }
}