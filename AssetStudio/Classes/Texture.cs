using AssetStudio.Extensions;
using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public abstract class Texture : NamedObject
    {
        protected Texture(ObjectReader reader) : base(reader)
        {
            if (this.version[0] > 2017 || this.version[0] == 2017 && this.version[1] >= 3) //2017.3 and up
            {
                int m_ForcedFallbackFormat = reader.ReadInt32();
                bool m_DownscaleFallback = reader.ReadBoolean();
                reader.AlignStream(4);
            }
        }
    }
}