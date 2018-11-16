using System.Collections.Generic;
using AssetStudio.Extensions;
using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public sealed class GameObject : EditorExtension
    {
        public List<PPtr> m_Components;
        public string m_Name;
        public PPtr m_Transform;
        public PPtr m_MeshRenderer;
        public PPtr m_MeshFilter;
        public PPtr m_SkinnedMeshRenderer;
        public PPtr m_Animator;

        public GameObject(ObjectReader reader) : base(reader)
        {
            int m_Component_size = reader.ReadInt32();
            this.m_Components = new List<PPtr>(m_Component_size);

            for (var j = 0; j < m_Component_size; j++)
            {
                if (reader.version[0] == 5 && reader.version[1] >= 5 || reader.version[0] > 5) //5.5.0 and up
                {
                    this.m_Components.Add(reader.ReadPPtr());
                }
                else
                {
                    int first = reader.ReadInt32();
                    this.m_Components.Add(reader.ReadPPtr());
                }
            }

            int m_Layer = reader.ReadInt32();
            this.m_Name = reader.ReadAlignedString();
        }
    }
}