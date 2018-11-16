using System;
using System.Drawing;
using AssetStudio.Extensions;
using AssetStudio.StudioClasses;
using SharpDX;
using RectangleF = System.Drawing.RectangleF;

namespace AssetStudio
{
    public enum SpritePackingRotation
    {
        kSPRNone = 0,
        kSPRFlipHorizontal = 1,
        kSPRFlipVertical = 2,
        kSPRRotate180 = 3,
        kSPRRotate90 = 4
    };

    public enum SpritePackingMode
    {
        kSPMTight = 0,
        kSPMRectangle
    };

    public class SpriteSettings
    {
        public uint settingsRaw;

        public uint packed;
        public SpritePackingMode packingMode;
        public SpritePackingRotation packingRotation;

        public SpriteSettings(ObjectReader reader)
        {
            this.settingsRaw = reader.ReadUInt32();

            this.packed = this.settingsRaw & 1; //1
            this.packingMode = (SpritePackingMode) ((this.settingsRaw >> 1) & 1); //1
            this.packingRotation = (SpritePackingRotation) ((this.settingsRaw >> 2) & 0xf); //4

            //meshType = (settingsRaw >> 6) & 1; //1
            //reserved
        }
    }

    public sealed class Sprite : NamedObject
    {
        public RectangleF m_Rect;
        public float m_PixelsToUnits;
        public Vector2 m_Pivot;
        public Tuple<Guid, long> m_RenderDataKey;
        public PPtr texture;
        public PPtr m_SpriteAtlas;
        public RectangleF textureRect;
        public SpriteSettings settingsRaw;
        public PointF[][] m_PhysicsShape; //Vector2[][]

        public Sprite(ObjectReader reader) : base(reader)
        {
            //Rectf m_Rect
            this.m_Rect = reader.ReadRectangleF();

            //Vector2f m_Offset
            reader.Position += 8;

            if (this.version[0] > 4 || this.version[0] == 4 && this.version[1] >= 5) //4.5 and up
            {
                //Vector4f m_Border
                reader.Position += 16;
            }

            this.m_PixelsToUnits = reader.ReadSingle();

            if (this.version[0] > 5 || this.version[0] == 5 && this.version[1] > 4 || this.version[0] == 5 && this.version[1] == 4 && this.version[2] >= 2) //5.4.2 and up
            {
                //Vector2f m_Pivot
                this.m_Pivot = reader.ReadVector2();
            }

            uint m_Extrude = reader.ReadUInt32();
            if (this.version[0] > 5 || this.version[0] == 5 && this.version[1] >= 3) //5.3 and up
            {
                bool m_IsPolygon = reader.ReadBoolean();
                reader.AlignStream();
            }

            if (this.version[0] >= 2017) //2017 and up
            {
                //pair m_RenderDataKey
                var first = new Guid(reader.ReadBytes(16));
                long second = reader.ReadInt64();
                this.m_RenderDataKey = new Tuple<Guid, long>(first, second);

                //vector m_AtlasTags
                int size = reader.ReadInt32();
                for (var i = 0; i < size; i++)
                {
                    string data = reader.ReadAlignedString();
                }

                //PPtr<SpriteAtlas> m_SpriteAtlas
                this.m_SpriteAtlas = reader.ReadPPtr();
            }

            //SpriteRenderData m_RD
            //  PPtr<Texture2D> texture
            this.texture = reader.ReadPPtr();

            //  PPtr<Texture2D> alphaTexture
            if (this.version[0] > 5 || this.version[0] == 5 && this.version[1] >= 2) //5.2 and up
            {
                PPtr alphaTexture = reader.ReadPPtr();
            }

            if (this.version[0] > 5 || this.version[0] == 5 && this.version[1] >= 6) //5.6 and up
            {
                //  vector m_SubMeshes
                int size = reader.ReadInt32();

                //      SubMesh data
                if (this.version[0] > 2017 || this.version[0] == 2017 && this.version[1] >= 3) //2017.3 and up
                {
                    reader.Position += 48 * size;
                }
                else
                {
                    reader.Position += 44 * size;
                }

                //  vector m_IndexBuffer
                size = reader.ReadInt32();
                reader.Position += size; //UInt8 data
                reader.AlignStream();

                //  VertexData m_VertexData
                if (this.version[0] < 2018) //2018 down
                {
                    int m_CurrentChannels = reader.ReadInt32();
                }

                uint m_VertexCount = reader.ReadUInt32();

                //      vector m_Channels
                size = reader.ReadInt32();
                reader.Position += size * 4; //ChannelInfo data
                //      TypelessData m_DataSize
                size = reader.ReadInt32();
                reader.Position += size; //UInt8 data
                reader.AlignStream();

                if (this.version[0] >= 2018) //2018 and up
                {
                    //	vector m_Bindpose
                    //			Matrix4x4f data
                    size = reader.ReadInt32();
                    reader.Position += size * 64;

                    if (this.version[0] == 2018 && this.version[1] < 2) //2018.2 down
                    {
                        //	vector m_SourceSkin
                        //			BoneWeights4 data
                        size = reader.ReadInt32();
                        reader.Position += size * 32;
                    }
                }
            }
            else
            {
                //  vector vertices
                int size = reader.ReadInt32();

                for (var i = 0; i < size; i++)
                {
                    //SpriteVertex data
                    reader.Position += 12; //Vector3f pos

                    if (this.version[0] < 4 || this.version[0] == 4 && this.version[1] <= 3) //4.3 and down
                    {
                        reader.Position += 8; //Vector2f uv
                    }
                }

                //  vector indices
                size = reader.ReadInt32();
                reader.Position += 2 * size; //UInt16 data
                reader.AlignStream();
            }

            //  Rectf textureRect
            this.textureRect = reader.ReadRectangleF();

            //  Vector2f textureRectOffset
            reader.Position += 8;

            //  Vector2f atlasRectOffset - 5.6 and up
            if (this.version[0] > 5 || this.version[0] == 5 && this.version[1] >= 6) //5.6 and up
            {
                reader.Position += 8;
            }

            //  unsigned int settingsRaw
            this.settingsRaw = new SpriteSettings(reader);

            //  Vector4f uvTransform - 4.5 and up
            if (this.version[0] > 4 || this.version[0] == 4 && this.version[1] >= 5) //4.5 and up
            {
                reader.Position += 16;
            }

            if (this.version[0] >= 2017) //2017 and up
            {
                //  float downscaleMultiplier - 2017 and up
                reader.Position += 4;
                //vector m_PhysicsShape - 2017 and up
                int m_PhysicsShape_size = reader.ReadInt32();
                this.m_PhysicsShape = new PointF[m_PhysicsShape_size][];

                for (var i = 0; i < m_PhysicsShape_size; i++)
                {
                    int data_size = reader.ReadInt32();
                    //Vector2f
                    this.m_PhysicsShape[i] = new PointF[data_size];

                    for (var j = 0; j < data_size; j++)
                    {
                        this.m_PhysicsShape[i][j] = new PointF(reader.ReadSingle(), reader.ReadSingle());
                    }
                }
            }
            //vector m_Bones 2018 and up
        }
    }
}