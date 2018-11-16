using AssetStudio.Extensions;
using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public sealed class VideoClip : NamedObject
    {
        public byte[] m_VideoData;
        public string m_OriginalPath;
        public string m_Source;
        public ulong m_Size;

        public VideoClip(ObjectReader reader, bool readData) : base(reader)
        {
            this.m_OriginalPath = reader.ReadAlignedString();
            uint m_ProxyWidth = reader.ReadUInt32();
            uint m_ProxyHeight = reader.ReadUInt32();
            uint Width = reader.ReadUInt32();
            uint Height = reader.ReadUInt32();
            if (this.sourceFile.version[0] >= 2017) //2017.x and up
            {
                uint m_PixelAspecRatioNum = reader.ReadUInt32();
                uint m_PixelAspecRatioDen = reader.ReadUInt32();
            }
            double m_FrameRate = reader.ReadDouble();
            ulong m_FrameCount = reader.ReadUInt64();
            int m_Format = reader.ReadInt32();
            //m_AudioChannelCount
            int size = reader.ReadInt32();
            reader.Position += size * 2;
            reader.AlignStream(4);
            //m_AudioSampleRate
            size = reader.ReadInt32();
            reader.Position += size * 4;
            //m_AudioLanguage
            size = reader.ReadInt32();
            for (int i = 0; i < size; i++)
            {
                reader.ReadAlignedString();
            }
            //StreamedResource m_ExternalResources
            this.m_Source = reader.ReadAlignedString();
            ulong m_Offset = reader.ReadUInt64();
            this.m_Size = reader.ReadUInt64();
            bool m_HasSplitAlpha = reader.ReadBoolean();

            if (!readData)
            {
                return;
            }

            if (!string.IsNullOrEmpty(this.m_Source))
            {
                this.m_VideoData = ResourcesHelper.GetData(this.m_Source, this.sourceFile.filePath, (long) m_Offset, (int) this.m_Size);
            }
            else
            {
                if (this.m_Size > 0)
                {
                    this.m_VideoData = reader.ReadBytes((int) this.m_Size);
                }
            }
        }
    }
}