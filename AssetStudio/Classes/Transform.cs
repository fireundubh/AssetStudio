using System.Collections.Generic;
using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public class Transform : Component
    {
        public float[] m_LocalRotation;
        public float[] m_LocalPosition;
        public float[] m_LocalScale;
        public List<PPtr> m_Children;
        public PPtr m_Father;

        public Transform(ObjectReader reader) : base(reader)
        {
            this.m_LocalRotation = new[]
            {
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle()
            };
            this.m_LocalPosition = new[]
            {
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle()
            };
            this.m_LocalScale = new[]
            {
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle()
            };

            int m_ChildrenCount = reader.ReadInt32();

            this.m_Children = new List<PPtr>(m_ChildrenCount);

            for (var j = 0; j < m_ChildrenCount; j++)
            {
                this.m_Children.Add(reader.ReadPPtr());
            }

            this.m_Father = reader.ReadPPtr();
        }
    }
}