using System.Collections.Generic;
using AssetStudio.Extensions;
using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public sealed class SkinnedMeshRenderer : Renderer
    {
        public PPtr m_Mesh;
        public PPtr[] m_Bones;
        public List<float> m_BlendShapeWeights;

        public SkinnedMeshRenderer(ObjectReader reader) : base(reader)
        {
            int m_Quality = reader.ReadInt32();
            bool m_UpdateWhenOffscreen = reader.ReadBoolean();
            bool m_SkinNormals = reader.ReadBoolean(); //3.1.0 and below

            reader.AlignStream(4);

            if (this.version[0] == 2 && this.version[1] < 6) //2.6 down
            {
                PPtr m_DisableAnimationWhenOffscreen = reader.ReadPPtr();
            }

            this.m_Mesh = reader.ReadPPtr();

            this.m_Bones = new PPtr[reader.ReadInt32()];

            for (var b = 0; b < this.m_Bones.Length; b++)
            {
                this.m_Bones[b] = reader.ReadPPtr();
            }

            if (this.version[0] < 3)
            {
                int m_BindPose = reader.ReadInt32();
                reader.Position += m_BindPose * 16 * 4; //Matrix4x4f
            }
            else
            {
                //4.3 and up
                if (this.version[0] > 4 || this.version[0] == 4 && this.version[1] >= 3)
                {
                    int numBSWeights = reader.ReadInt32();

                    this.m_BlendShapeWeights = new List<float>(numBSWeights);

                    for (var i = 0; i < numBSWeights; i++)
                    {
                        this.m_BlendShapeWeights.Add(reader.ReadSingle());
                    }
                }
            }
        }
    }
}