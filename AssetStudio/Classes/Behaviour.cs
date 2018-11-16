using AssetStudio.Extensions;
using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public abstract class Behaviour : Component
    {
        public byte m_Enabled;

        protected Behaviour(ObjectReader reader) : base(reader)
        {
            this.m_Enabled = reader.ReadByte();
            reader.AlignStream(4);
        }
    }
}