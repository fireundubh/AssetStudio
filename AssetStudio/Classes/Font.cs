using AssetStudio.Extensions;
using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public sealed class Font : NamedObject
    {
        public byte[] m_FontData;

        public Font(ObjectReader reader) : base(reader)
        {
            if (this.version[0] == 5 && this.version[1] >= 5 || this.version[0] > 5) //5.5 and up
            {
                float m_LineSpacing = reader.ReadSingle();
                PPtr m_DefaultMaterial = reader.ReadPPtr();
                float m_FontSize = reader.ReadSingle();
                PPtr m_Texture = reader.ReadPPtr();
                int m_AsciiStartOffset = reader.ReadInt32();
                float m_Tracking = reader.ReadSingle();
                int m_CharacterSpacing = reader.ReadInt32();
                int m_CharacterPadding = reader.ReadInt32();
                int m_ConvertCase = reader.ReadInt32();
                int m_CharacterRects_size = reader.ReadInt32();
                for (var i = 0; i < m_CharacterRects_size; i++)
                {
                    reader.Position += 44; //CharacterInfo data 41
                }
                int m_KerningValues_size = reader.ReadInt32();
                for (var i = 0; i < m_KerningValues_size; i++)
                {
                    reader.Position += 8;
                }
                float m_PixelScale = reader.ReadSingle();
                int m_FontData_size = reader.ReadInt32();
                if (m_FontData_size > 0)
                {
                    this.m_FontData = reader.ReadBytes(m_FontData_size);
                }
            }
            else
            {
                int m_AsciiStartOffset = reader.ReadInt32();

                if (this.version[0] <= 3)
                {
                    int m_FontCountX = reader.ReadInt32();
                    int m_FontCountY = reader.ReadInt32();
                }

                float m_Kerning = reader.ReadSingle();
                float m_LineSpacing = reader.ReadSingle();

                if (this.version[0] <= 3)
                {
                    int m_PerCharacterKerning_size = reader.ReadInt32();
                    for (var i = 0; i < m_PerCharacterKerning_size; i++)
                    {
                        int first = reader.ReadInt32();
                        float second = reader.ReadSingle();
                    }
                }
                else
                {
                    int m_CharacterSpacing = reader.ReadInt32();
                    int m_CharacterPadding = reader.ReadInt32();
                }

                int m_ConvertCase = reader.ReadInt32();
                PPtr m_DefaultMaterial = reader.ReadPPtr();

                int m_CharacterRects_size = reader.ReadInt32();
                for (var i = 0; i < m_CharacterRects_size; i++)
                {
                    int index = reader.ReadInt32();
                    //Rectf uv
                    float uvx = reader.ReadSingle();
                    float uvy = reader.ReadSingle();
                    float uvwidth = reader.ReadSingle();
                    float uvheight = reader.ReadSingle();
                    //Rectf vert
                    float vertx = reader.ReadSingle();
                    float verty = reader.ReadSingle();
                    float vertwidth = reader.ReadSingle();
                    float vertheight = reader.ReadSingle();
                    float width = reader.ReadSingle();

                    if (this.version[0] >= 4)
                    {
                        bool flipped = reader.ReadBoolean();
                        reader.AlignStream(4);
                    }
                }

                PPtr m_Texture = reader.ReadPPtr();

                int m_KerningValues_size = reader.ReadInt32();
                for (var i = 0; i < m_KerningValues_size; i++)
                {
                    int pairfirst = reader.ReadInt16();
                    int pairsecond = reader.ReadInt16();
                    float second = reader.ReadSingle();
                }

                if (this.version[0] <= 3)
                {
                    bool m_GridFont = reader.ReadBoolean();
                    reader.AlignStream(4);
                }
                else
                {
                    float m_PixelScale = reader.ReadSingle();
                }

                int m_FontData_size = reader.ReadInt32();
                if (m_FontData_size > 0)
                {
                    this.m_FontData = reader.ReadBytes(m_FontData_size);
                }
            }
        }
    }
}