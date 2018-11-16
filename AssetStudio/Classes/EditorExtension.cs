using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public abstract class EditorExtension : Object
    {
        protected EditorExtension(ObjectReader reader) : base(reader)
        {
            if (this.platform == BuildTarget.NoTarget)
            {
                PPtr m_PrefabParentObject = reader.ReadPPtr();
                PPtr m_PrefabInternal = reader.ReadPPtr();
            }
        }
    }
}