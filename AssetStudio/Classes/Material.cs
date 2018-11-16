using AssetStudio.Extensions;
using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public class TexEnv
    {
        public string name;
        public PPtr m_Texture;
        public float[] m_Scale;
        public float[] m_Offset;
    }

    public class strFloatPair
    {
        public string first;
        public float second;
    }

    public class strColorPair
    {
        public string first;
        public float[] second;
    }

    public sealed class Material : NamedObject
    {
        public PPtr m_Shader;
        public string[] m_ShaderKeywords;
        public int m_CustomRenderQueue;
        public TexEnv[] m_TexEnvs;
        public strFloatPair[] m_Floats;
        public strColorPair[] m_Colors;

        public Material(ObjectReader reader) : base(reader)
        {
            this.m_Shader = reader.ReadPPtr();

            if (reader.version[0] == 4 && (reader.version[1] >= 2 || reader.version[1] == 1 && reader.buildType[0] != "a"))
            {
                this.m_ShaderKeywords = new string[reader.ReadInt32()];

                for (var i = 0; i < this.m_ShaderKeywords.Length; i++)
                {
                    this.m_ShaderKeywords[i] = reader.ReadAlignedString();
                }
            }
            else if (reader.version[0] >= 5) //5.0 and up
            {
                this.m_ShaderKeywords = new[]
                {
                    reader.ReadAlignedString()
                };

                uint m_LightmapFlags = reader.ReadUInt32();

                if (reader.version[0] == 5 && reader.version[1] >= 6 || reader.version[0] > 5) //5.6.0 and up
                {
                    bool m_EnableInstancingVariants = reader.ReadBoolean();
                    //var m_DoubleSidedGI = a_Stream.ReadBoolean();//2017.x
                    reader.AlignStream();
                }
            }

            if (reader.version[0] > 4 || reader.version[0] == 4 && reader.version[1] >= 3)
            {
                this.m_CustomRenderQueue = reader.ReadInt32();
            }

            if (reader.version[0] == 5 && reader.version[1] >= 1 || reader.version[0] > 5) //5.1 and up
            {
                var stringTagMap = new string[reader.ReadInt32()][];

                for (var i = 0; i < stringTagMap.Length; i++)
                {
                    stringTagMap[i] = new[]
                    {
                        reader.ReadAlignedString(),
                        reader.ReadAlignedString()
                    };
                }
            }

            //disabledShaderPasses
            if (reader.version[0] == 5 && reader.version[1] >= 6 || reader.version[0] > 5) //5.6.0 and up
            {
                int size = reader.ReadInt32();

                for (var i = 0; i < size; i++)
                {
                    reader.ReadAlignedString();
                }
            }

            //m_SavedProperties
            this.m_TexEnvs = new TexEnv[reader.ReadInt32()];

            for (var i = 0; i < this.m_TexEnvs.Length; i++)
            {
                var m_TexEnv = new TexEnv()
                {
                    name = reader.ReadAlignedString(),
                    m_Texture = reader.ReadPPtr(),
                    m_Scale = new[]
                    {
                        reader.ReadSingle(),
                        reader.ReadSingle()
                    },
                    m_Offset = new[]
                    {
                        reader.ReadSingle(),
                        reader.ReadSingle()
                    }
                };

                this.m_TexEnvs[i] = m_TexEnv;
            }

            this.m_Floats = new strFloatPair[reader.ReadInt32()];

            for (var i = 0; i < this.m_Floats.Length; i++)
            {
                var m_Float = new strFloatPair()
                {
                    first = reader.ReadAlignedString(),
                    second = reader.ReadSingle()
                };

                this.m_Floats[i] = m_Float;
            }

            this.m_Colors = new strColorPair[reader.ReadInt32()];

            for (var i = 0; i < this.m_Colors.Length; i++)
            {
                var m_Color = new strColorPair()
                {
                    first = reader.ReadAlignedString(),
                    second = new[]
                    {
                        reader.ReadSingle(),
                        reader.ReadSingle(),
                        reader.ReadSingle(),
                        reader.ReadSingle()
                    }
                };

                this.m_Colors[i] = m_Color;
            }
        }
    }
}