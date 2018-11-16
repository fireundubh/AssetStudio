using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public sealed class MeshFilter : Component
    {
        public long preloadIndex;
        public PPtr m_Mesh;

        public MeshFilter(ObjectReader reader) : base(reader)
        {
            this.m_Mesh = reader.ReadPPtr();
        }
    }
}