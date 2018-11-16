using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public abstract class Component : EditorExtension
    {
        public PPtr m_GameObject;

        protected Component(ObjectReader reader) : base(reader)
        {
            this.m_GameObject = reader.ReadPPtr();
        }
    }
}