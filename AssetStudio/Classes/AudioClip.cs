using AssetStudio.Extensions;
using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public sealed class AudioClip : NamedObject
    {
        public int m_Format;
        public AudioType m_Type;
        public bool m_3D;
        public bool m_UseHardware;

        //version 5
        public int m_LoadType;
        public int m_Channels;
        public int m_Frequency;
        public int m_BitsPerSample;
        public float m_Length;
        public bool m_IsTrackerFormat;
        public int m_SubsoundIndex;
        public bool m_PreloadAudioData;
        public bool m_LoadInBackground;
        public bool m_Legacy3D;
        public AudioCompressionFormat m_CompressionFormat;

        public string m_Source;
        public long m_Offset;
        public long m_Size;
        public byte[] m_AudioData;

        public AudioClip(ObjectReader reader, bool readData) : base(reader)
        {
            if (this.version[0] < 5)
            {
                this.m_Format = reader.ReadInt32();
                this.m_Type = (AudioType) reader.ReadInt32();
                this.m_3D = reader.ReadBoolean();
                this.m_UseHardware = reader.ReadBoolean();

                reader.AlignStream();

                if (this.version[0] >= 4 || this.version[0] == 3 && this.version[1] >= 2) //3.2.0 to 5
                {
                    int m_Stream = reader.ReadInt32();
                    this.m_Size = reader.ReadInt32();

                    long tsize = this.m_Size % 4 != 0 ? this.m_Size + 4 - this.m_Size % 4 : this.m_Size;

                    if (reader.byteSize + reader.byteStart - reader.Position != tsize)
                    {
                        this.m_Offset = reader.ReadInt32();
                        this.m_Source = this.sourceFile.filePath + ".resS";
                    }
                }
                else
                {
                    this.m_Size = reader.ReadInt32();
                }
            }
            else
            {
                this.m_LoadType = reader.ReadInt32();
                this.m_Channels = reader.ReadInt32();
                this.m_Frequency = reader.ReadInt32();
                this.m_BitsPerSample = reader.ReadInt32();
                this.m_Length = reader.ReadSingle();
                this.m_IsTrackerFormat = reader.ReadBoolean();

                reader.AlignStream();

                this.m_SubsoundIndex = reader.ReadInt32();
                this.m_PreloadAudioData = reader.ReadBoolean();
                this.m_LoadInBackground = reader.ReadBoolean();
                this.m_Legacy3D = reader.ReadBoolean();

                reader.AlignStream();

                this.m_3D = this.m_Legacy3D;

                this.m_Source = reader.ReadAlignedString();
                this.m_Offset = reader.ReadInt64();
                this.m_Size = reader.ReadInt64();
                this.m_CompressionFormat = (AudioCompressionFormat) reader.ReadInt32();
            }

            if (!readData)
            {
                return;
            }

            if (!string.IsNullOrEmpty(this.m_Source))
            {
                this.m_AudioData = ResourcesHelper.GetData(this.m_Source, this.sourceFile.filePath, this.m_Offset, (int) this.m_Size);
            }
            else
            {
                if (this.m_Size > 0)
                {
                    this.m_AudioData = reader.ReadBytes((int) this.m_Size);
                }
            }
        }
    }

    public enum AudioType
    {
        UNKNOWN,
        ACC,
        AIFF,
        IT = 10,
        MOD = 12,
        MPEG,
        OGGVORBIS,
        S3M = 17,
        WAV = 20,
        XM,
        XMA,
        VAG,
        AUDIOQUEUE
    }

    public enum AudioCompressionFormat
    {
        PCM,
        Vorbis,
        ADPCM,
        MP3,
        VAG,
        HEVAG,
        XMA,
        AAC,
        GCADPCM,
        ATRAC9
    }
}