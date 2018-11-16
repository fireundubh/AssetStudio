using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public sealed class TextAsset : NamedObject
    {
        public byte[] m_Script;

        public TextAsset(ObjectReader reader) : base(reader)
        {
            this.m_Script = reader.ReadBytes(reader.ReadInt32());
        }
    }
}