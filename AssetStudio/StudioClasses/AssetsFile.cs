using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AssetStudio.Extensions;
using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public class AssetsFile : IDisposable
    {
        public EndianBinaryReader reader;
        public string filePath;
        public string parentPath;
        public string fileName;
        public string upperFileName;

        public int[] version =
        {
            0,
            0,
            0,
            0
        };

        public string[] buildType;
        public string platformStr;
        public bool valid;
        public Dictionary<long, ObjectReader> ObjectReaders = new Dictionary<long, ObjectReader>();
        public Dictionary<long, GameObject> GameObjects = new Dictionary<long, GameObject>();
        public Dictionary<long, Transform> Transforms = new Dictionary<long, Transform>();

        //class SerializedFile
        public SerializedFileHeader header;
        private EndianType m_FileEndianess;
        public string unityVersion = "2.5.0f5";
        public BuildTarget m_TargetPlatform = BuildTarget.UnknownPlatform;
        private bool m_EnableTypeTree = true;
        public List<SerializedType> m_Types;
        public Dictionary<long, ObjectInfo> m_Objects;
        private List<LocalSerializedObjectIdentifier> m_ScriptTypes;
        public List<FileIdentifier> m_Externals;

        public AssetsFile(string fullName, EndianBinaryReader reader)
        {
            this.reader = reader;
            this.filePath = fullName;
            this.fileName = Path.GetFileName(fullName);
            this.upperFileName = this.fileName.ToUpper();
            try
            {
                //SerializedFile::ReadHeader
                this.header = new SerializedFileHeader();
                this.header.m_MetadataSize = reader.ReadUInt32();
                this.header.m_FileSize = reader.ReadUInt32();
                this.header.m_Version = reader.ReadUInt32();
                this.header.m_DataOffset = reader.ReadUInt32();

                if (this.header.m_Version >= 9)
                {
                    this.header.m_Endianess = reader.ReadByte();
                    this.header.m_Reserved = reader.ReadBytes(3);
                    this.m_FileEndianess = (EndianType) this.header.m_Endianess;
                }
                else
                {
                    reader.Position = this.header.m_FileSize - this.header.m_MetadataSize;
                    this.m_FileEndianess = (EndianType) reader.ReadByte();
                }

                //SerializedFile::ReadMetadata
                if (this.m_FileEndianess == EndianType.LittleEndian)
                {
                    reader.endian = EndianType.LittleEndian;
                }
                if (this.header.m_Version >= 7)
                {
                    this.unityVersion = reader.ReadStringToNull();
                }
                if (this.header.m_Version >= 8)
                {
                    this.m_TargetPlatform = (BuildTarget) reader.ReadInt32();
                    if (!Enum.IsDefined(typeof(BuildTarget), this.m_TargetPlatform))
                    {
                        this.m_TargetPlatform = BuildTarget.UnknownPlatform;
                    }
                }
                this.platformStr = this.m_TargetPlatform.ToString();
                if (this.header.m_Version >= 13)
                {
                    this.m_EnableTypeTree = reader.ReadBoolean();
                }

                //Read types
                int typeCount = reader.ReadInt32();
                this.m_Types = new List<SerializedType>(typeCount);
                for (var i = 0; i < typeCount; i++)
                {
                    this.m_Types.Add(this.ReadSerializedType());
                }

                if (this.header.m_Version >= 7 && this.header.m_Version < 14)
                {
                    int bigIDEnabled = reader.ReadInt32();
                }

                //Read Objects
                int objectCount = reader.ReadInt32();

                this.m_Objects = new Dictionary<long, ObjectInfo>(objectCount);
                for (var i = 0; i < objectCount; i++)
                {
                    var objectInfo = new ObjectInfo();
                    if (this.header.m_Version < 14)
                    {
                        objectInfo.m_PathID = reader.ReadInt32();
                    }
                    else
                    {
                        reader.AlignStream();
                        objectInfo.m_PathID = reader.ReadInt64();
                    }
                    objectInfo.byteStart = reader.ReadUInt32();
                    objectInfo.byteStart += this.header.m_DataOffset;
                    objectInfo.byteSize = reader.ReadUInt32();
                    objectInfo.typeID = reader.ReadInt32();
                    if (this.header.m_Version < 16)
                    {
                        objectInfo.classID = reader.ReadUInt16();
                        objectInfo.serializedType = this.m_Types.Find(x => x.classID == objectInfo.typeID);
                        objectInfo.isDestroyed = reader.ReadUInt16();
                    }
                    else
                    {
                        SerializedType type = this.m_Types[objectInfo.typeID];
                        objectInfo.serializedType = type;
                        objectInfo.classID = type.classID;
                    }
                    if (this.header.m_Version == 15 || this.header.m_Version == 16)
                    {
                        byte stripped = reader.ReadByte();
                    }
                    this.m_Objects.Add(objectInfo.m_PathID, objectInfo);

                    //Create Reader
                    var objectReader = new ObjectReader(reader, this, objectInfo);
                    this.ObjectReaders.Add(objectReader.m_PathID, objectReader);

                    #region read BuildSettings to get version for version 2.x files

                    if (objectReader.type == ClassIDType.BuildSettings && this.header.m_Version == 6)
                    {
                        long nextAsset = reader.Position;

                        var buildSettings = new BuildSettings(objectReader);
                        this.unityVersion = buildSettings.m_Version;

                        reader.Position = nextAsset;
                    }

                    #endregion
                }

                if (this.header.m_Version >= 11)
                {
                    int scriptCount = reader.ReadInt32();
                    this.m_ScriptTypes = new List<LocalSerializedObjectIdentifier>(scriptCount);
                    for (var i = 0; i < scriptCount; i++)
                    {
                        var m_ScriptType = new LocalSerializedObjectIdentifier();
                        m_ScriptType.localSerializedFileIndex = reader.ReadInt32();
                        if (this.header.m_Version < 14)
                        {
                            m_ScriptType.localIdentifierInFile = reader.ReadInt32();
                        }
                        else
                        {
                            reader.AlignStream();
                            m_ScriptType.localIdentifierInFile = reader.ReadInt64();
                        }
                        this.m_ScriptTypes.Add(m_ScriptType);
                    }
                }

                int externalsCount = reader.ReadInt32();
                this.m_Externals = new List<FileIdentifier>(externalsCount);
                for (var i = 0; i < externalsCount; i++)
                {
                    var m_External = new FileIdentifier();
                    if (this.header.m_Version >= 6)
                    {
                        string tempEmpty = reader.ReadStringToNull();
                    }
                    if (this.header.m_Version >= 5)
                    {
                        m_External.guid = new Guid(reader.ReadBytes(16));
                        m_External.type = reader.ReadInt32();
                    }
                    m_External.pathName = reader.ReadStringToNull();
                    m_External.fileName = Path.GetFileName(m_External.pathName);
                    this.m_Externals.Add(m_External);
                }

                if (this.header.m_Version >= 5)
                {
                    //var userInformation = reader.ReadStringToNull();
                }

                this.buildType = Regex.Replace(this.unityVersion, @"\d", "").
                    Split(new[]
                    {
                        "."
                    }, StringSplitOptions.RemoveEmptyEntries);
                int firstVersion = int.Parse(this.unityVersion.Split('.')[0]);
                this.version = Regex.Matches(this.unityVersion, @"\d").Cast<Match>().Select(m => int.Parse(m.Value)).ToArray();
                if (firstVersion > 5) //2017 and up
                {
                    var nversion = new int[this.version.Length - 3];
                    nversion[0] = firstVersion;
                    Array.Copy(this.version, 4, nversion, 1, this.version.Length - 4);
                    this.version = nversion;
                }

                this.valid = true;
            }
            catch
            {
            }
        }

        private SerializedType ReadSerializedType()
        {
            var type = new SerializedType();

            type.classID = this.reader.ReadInt32();

            if (this.header.m_Version >= 16)
            {
                type.m_IsStrippedType = this.reader.ReadBoolean();
            }

            if (this.header.m_Version >= 17)
            {
                type.m_ScriptTypeIndex = this.reader.ReadInt16();
            }

            if (this.header.m_Version >= 13)
            {
                if ((this.header.m_Version < 16 && type.classID < 0) || (this.header.m_Version >= 16 && type.classID == 114))
                {
                    type.m_ScriptID = this.reader.ReadBytes(16); //Hash128
                }
                type.m_OldTypeHash = this.reader.ReadBytes(16); //Hash128
            }

            if (this.m_EnableTypeTree)
            {
                var typeTree = new List<TypeTreeNode>();
                if (this.header.m_Version >= 12 || this.header.m_Version == 10)
                {
                    this.ReadTypeTree5(typeTree);
                }
                else
                {
                    this.ReadTypeTree(typeTree);
                }

                type.m_Nodes = typeTree;
            }

            return type;
        }

        private void ReadTypeTree(List<TypeTreeNode> typeTree, int depth = 0)
        {
            var typeTreeNode = new TypeTreeNode();
            typeTree.Add(typeTreeNode);
            typeTreeNode.m_Level = depth;
            typeTreeNode.m_Type = this.reader.ReadStringToNull();
            typeTreeNode.m_Name = this.reader.ReadStringToNull();
            typeTreeNode.m_ByteSize = this.reader.ReadInt32();
            if (this.header.m_Version == 2)
            {
                int variableCount = this.reader.ReadInt32();
            }
            if (this.header.m_Version != 3)
            {
                typeTreeNode.m_Index = this.reader.ReadInt32();
            }
            typeTreeNode.m_IsArray = this.reader.ReadInt32();
            typeTreeNode.m_Version = this.reader.ReadInt32();
            if (this.header.m_Version != 3)
            {
                typeTreeNode.m_MetaFlag = this.reader.ReadInt32();
            }

            int childrenCount = this.reader.ReadInt32();
            for (var i = 0; i < childrenCount; i++)
            {
                this.ReadTypeTree(typeTree, depth + 1);
            }
        }

        private void ReadTypeTree5(List<TypeTreeNode> typeTree)
        {
            int numberOfNodes = this.reader.ReadInt32();
            int stringBufferSize = this.reader.ReadInt32();

            this.reader.Position += numberOfNodes * 24;
            using (var stringBufferReader = new BinaryReader(new MemoryStream(this.reader.ReadBytes(stringBufferSize))))
            {
                this.reader.Position -= numberOfNodes * 24 + stringBufferSize;
                for (var i = 0; i < numberOfNodes; i++)
                {
                    var typeTreeNode = new TypeTreeNode();
                    typeTree.Add(typeTreeNode);
                    typeTreeNode.m_Version = this.reader.ReadUInt16();
                    typeTreeNode.m_Level = this.reader.ReadByte();
                    typeTreeNode.m_IsArray = this.reader.ReadBoolean() ? 1 : 0;

                    ushort m_TypeStrOffset = this.reader.ReadUInt16();
                    ushort temp = this.reader.ReadUInt16();
                    if (temp == 0)
                    {
                        stringBufferReader.BaseStream.Position = m_TypeStrOffset;
                        typeTreeNode.m_Type = stringBufferReader.ReadStringToNull();
                    }
                    else
                    {
                        typeTreeNode.m_Type = CommonString.StringBuffer.ContainsKey(m_TypeStrOffset) ? CommonString.StringBuffer[m_TypeStrOffset] : m_TypeStrOffset.ToString();
                    }

                    ushort m_NameStrOffset = this.reader.ReadUInt16();
                    temp = this.reader.ReadUInt16();
                    if (temp == 0)
                    {
                        stringBufferReader.BaseStream.Position = m_NameStrOffset;
                        typeTreeNode.m_Name = stringBufferReader.ReadStringToNull();
                    }
                    else
                    {
                        typeTreeNode.m_Name = CommonString.StringBuffer.ContainsKey(m_NameStrOffset) ? CommonString.StringBuffer[m_NameStrOffset] : m_NameStrOffset.ToString();
                    }

                    typeTreeNode.m_ByteSize = this.reader.ReadInt32();
                    typeTreeNode.m_Index = this.reader.ReadInt32();
                    typeTreeNode.m_MetaFlag = this.reader.ReadInt32();
                }
                this.reader.Position += stringBufferSize;
            }
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
                this.reader?.Dispose();
            }
        }
    }
}