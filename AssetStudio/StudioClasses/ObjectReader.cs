using System;
using System.Linq;
using System.Text;

namespace AssetStudio.StudioClasses
{
    public class ObjectReader : EndianBinaryReader
    {
        public AssetsFile assetsFile;
        public long m_PathID;
        public uint byteStart;
        public uint byteSize;
        public ClassIDType type;
        public SerializedType serializedType;
        public BuildTarget platform;
        private uint m_Version;

        public int[] version => this.assetsFile.version;
        public string[] buildType => this.assetsFile.buildType;

        public string exportName; //TODO Remove it

        public ObjectReader(EndianBinaryReader reader, AssetsFile assetsFile, ObjectInfo objectInfo) : base(reader.BaseStream, reader.endian)
        {
            this.assetsFile = assetsFile;
            this.m_PathID = objectInfo.m_PathID;
            this.byteStart = objectInfo.byteStart;
            this.byteSize = objectInfo.byteSize;
            if (Enum.IsDefined(typeof(ClassIDType), objectInfo.classID))
            {
                this.type = (ClassIDType) objectInfo.classID;
            }
            else
            {
                this.type = ClassIDType.UnknownType;
            }
            this.serializedType = objectInfo.serializedType;
            this.platform = assetsFile.m_TargetPlatform;
            this.m_Version = assetsFile.header.m_Version;
        }

        public void Reset()
        {
            this.Position = this.byteStart;
        }

        public string Dump()
        {
            this.Reset();
            if (this.serializedType?.m_Nodes != null)
            {
                var sb = new StringBuilder();
                TypeTreeHelper.ReadTypeString(sb, this.serializedType.m_Nodes, this);
                return sb.ToString();
            }
            return null;
        }

        public bool HasStructMember(string name)
        {
            return this.serializedType?.m_Nodes != null && this.serializedType.m_Nodes.Any(x => x.m_Name == name);
        }

        public PPtr ReadPPtr()
        {
            return new PPtr
            {
                m_FileID = this.ReadInt32(),
                m_PathID = this.m_Version < 14 ? this.ReadInt32() : this.ReadInt64(),
                assetsFile = this.assetsFile
            };
        }

        public byte[] GetRawData()
        {
            this.Reset();
            return this.ReadBytes((int) this.byteSize);
        }
    }
}