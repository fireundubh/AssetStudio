using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public abstract class Object
    {
        protected AssetsFile sourceFile;
        public ObjectReader reader;
        public int[] version;
        protected string[] buildType;
        public BuildTarget platform;

        protected Object(ObjectReader reader)
        {
            this.reader = reader;
            reader.Reset();

            this.sourceFile = reader.assetsFile;
            this.version = this.sourceFile.version;
            this.buildType = this.sourceFile.buildType;
            this.platform = this.sourceFile.m_TargetPlatform;

            if (this.platform == BuildTarget.NoTarget)
            {
                uint m_ObjectHideFlags = reader.ReadUInt32();
            }
        }
    }
}