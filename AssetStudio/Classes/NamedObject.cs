using AssetStudio.Extensions;
using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public class NamedObject : EditorExtension
    {
        public string m_Name;

        public NamedObject(ObjectReader reader) : base(reader)
        {
            this.m_Name = reader.ReadAlignedString();
        }
    }
}