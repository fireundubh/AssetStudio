using AssetStudio.Extensions;
using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public sealed class BuildSettings : Object
    {
        public string m_Version;

        public BuildSettings(ObjectReader reader) : base(reader)
        {
            int levelsNum = this.reader.ReadInt32();

            for (var i = 0; i < levelsNum; i++)
            {
                string level = this.reader.ReadAlignedString();
            }

            bool hasRenderTexture = this.reader.ReadBoolean();
            bool hasPROVersion = this.reader.ReadBoolean();
            bool hasPublishingRights = this.reader.ReadBoolean();
            bool hasShadows = this.reader.ReadBoolean();

            this.m_Version = this.reader.ReadAlignedString();
        }
    }
}