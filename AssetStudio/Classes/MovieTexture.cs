using AssetStudio.Extensions;
using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public sealed class MovieTexture : Texture
    {
        public byte[] m_MovieData;

        public MovieTexture(ObjectReader reader) : base(reader)
        {
            bool m_Loop = reader.ReadBoolean();
            reader.AlignStream(4);
            //PPtr<AudioClip>
            reader.ReadPPtr();
            this.m_MovieData = reader.ReadBytes(reader.ReadInt32());
        }
    }
}