using AssetStudio.Extensions;
using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public class StaticBatchInfo
    {
        public ushort firstSubMesh;
        public ushort subMeshCount;
    }

    public abstract class Renderer : Component
    {
        public PPtr[] m_Materials;
        public StaticBatchInfo m_StaticBatchInfo;
        public uint[] m_SubsetIndices;

        protected Renderer(ObjectReader reader) : base(reader)
        {
            if (this.version[0] < 5)
            {
                bool m_Enabled = reader.ReadBoolean();
                byte m_CastShadows = reader.ReadByte();
                bool m_ReceiveShadows = reader.ReadBoolean();
                byte m_LightmapIndex = reader.ReadByte();
            }
            else
            {
                bool m_Enabled = reader.ReadBoolean();

                reader.AlignStream(4);

                byte m_CastShadows = reader.ReadByte();
                bool m_ReceiveShadows = reader.ReadBoolean();

                reader.AlignStream(4);

                if (this.version[0] >= 2018) //2018 and up
                {
                    uint m_RenderingLayerMask = reader.ReadUInt32();
                }

                ushort m_LightmapIndex = reader.ReadUInt16();
                ushort m_LightmapIndexDynamic = reader.ReadUInt16();
            }

            if (this.version[0] >= 3)
            {
                reader.Position += 16; //Vector4f m_LightmapTilingOffset
            }

            if (this.version[0] >= 5)
            {
                reader.Position += 16; //Vector4f m_LightmapTilingOffsetDynamic
            }

            this.m_Materials = new PPtr[reader.ReadInt32()];

            for (var m = 0; m < this.m_Materials.Length; m++)
            {
                this.m_Materials[m] = reader.ReadPPtr();
            }

            if (this.version[0] < 3)
            {
                reader.Position += 16; //m_LightmapTilingOffset vector4d
            }
            else
            {
                if (reader.version[0] == 5 && reader.version[1] >= 5 || reader.version[0] > 5) //5.5.0 and up
                {
                    this.m_StaticBatchInfo = new StaticBatchInfo
                    {
                        firstSubMesh = reader.ReadUInt16(),
                        subMeshCount = reader.ReadUInt16()
                    };
                }
                else
                {
                    int numSubsetIndices = reader.ReadInt32();
                    this.m_SubsetIndices = reader.ReadUInt32Array(numSubsetIndices);
                }

                PPtr m_StaticBatchRoot = reader.ReadPPtr();

                if (this.version[0] == 5 && this.version[1] >= 4 || this.version[0] > 5) //5.4.0 and up
                {
                    PPtr m_ProbeAnchor = reader.ReadPPtr();
                    PPtr m_LightProbeVolumeOverride = reader.ReadPPtr();
                }
                else if (this.version[0] >= 4 || this.version[0] == 3 && this.version[1] >= 5) //3.5 - 5.3
                {
                    bool m_UseLightProbes = reader.ReadBoolean();

                    reader.AlignStream(4);

                    if (this.version[0] == 5) //5.0 and up
                    {
                        int m_ReflectionProbeUsage = reader.ReadInt32();
                    }

                    PPtr m_LightProbeAnchor = reader.ReadPPtr();
                }

                if (this.version[0] >= 5 || this.version[0] == 4 && this.version[1] >= 3) //4.3 and up
                {
                    if (this.version[0] == 4 && this.version[1] == 3) //4.3
                    {
                        int m_SortingLayer = reader.ReadInt16();
                    }
                    else
                    {
                        int m_SortingLayerID = reader.ReadInt32();
                        //SInt16 m_SortingOrder 5.6 and up
                    }

                    int m_SortingOrder = reader.ReadInt16();

                    reader.AlignStream(4);
                }
            }
        }
    }
}