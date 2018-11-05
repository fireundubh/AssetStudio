using System;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace AssetStudio
{
    public class AssetPreloadData : ListViewItem, IDisposable
    {
        public AssetsFile sourceFile;
        public long m_PathID;
        public uint Offset;
        public uint Size;
        public long FullSize;
        public SerializedType serializedType;
        public ClassIDType Type;
        public string TypeString;
        public string InfoText;
        public string uniqueID;
        public GameObject gameObject;

        public AssetPreloadData(AssetsFile assetsFile, ObjectInfo objectInfo, string uniqueID)
        {
            sourceFile = assetsFile;
            m_PathID = objectInfo.m_PathID;
            Offset = objectInfo.byteStart;
            Size = objectInfo.byteSize;
            FullSize = objectInfo.byteSize;
            serializedType = objectInfo.serializedType;

            if (Enum.IsDefined(typeof(ClassIDType), objectInfo.classID))
            {
                Type = (ClassIDType)objectInfo.classID;
                TypeString = Type.ToString();
            }
            else
            {
                Type = ClassIDType.UnknownType;
                TypeString = $"UnknownType {objectInfo.classID}";
            }

            this.uniqueID = uniqueID;
        }

        public EndianBinaryReader InitReader()
        {
            EndianBinaryReader reader = sourceFile.reader;
            reader.Position = Offset;
            return reader;
        }

        public string Dump()
        {
            EndianBinaryReader reader = InitReader();

            if (this.serializedType?.m_Nodes == null)
            {
                return null;
            }

            var sb = new StringBuilder();
            TypeTreeHelper.ReadTypeString(sb, this.serializedType.m_Nodes, reader);

            return sb.ToString();
        }

        public bool HasStructMember(string name)
        {
            return serializedType?.m_Nodes != null && serializedType.m_Nodes.Any(x => x.m_Name == name);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.sourceFile?.Dispose();
            }
        }
    }
}
