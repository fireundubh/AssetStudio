using SharpDX;
using System;
using System.Collections.Generic;
using AssetStudio.Extensions;
using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public class SpriteAtlasData
    {
        public PPtr texture;
        public PPtr alphaTexture;
        public System.Drawing.RectangleF textureRect;
        public Vector2 textureRectOffset;
        public Vector2 atlasRectOffset;
        public Vector4 uvTransform;
        public float downscaleMultiplier;
        public SpriteSettings settingsRaw;

        public SpriteAtlasData(ObjectReader reader)
        {
            int[] version = reader.version;
            this.texture = reader.ReadPPtr();
            this.alphaTexture = reader.ReadPPtr();
            this.textureRect = reader.ReadRectangleF();
            this.textureRectOffset = reader.ReadVector2();
            if (version[0] > 2017 || (version[0] == 2017 && version[1] >= 2)) //2017.2 and up
            {
                this.atlasRectOffset = reader.ReadVector2();
            }
            this.uvTransform = reader.ReadVector4();
            this.downscaleMultiplier = reader.ReadSingle();
            this.settingsRaw = new SpriteSettings(reader);
        }
    }

    public sealed class SpriteAtlas : NamedObject
    {
        public Dictionary<Tuple<Guid, long>, SpriteAtlasData> m_RenderDataMap;

        public SpriteAtlas(ObjectReader reader) : base(reader)
        {
            int m_PackedSpritesSize = reader.ReadInt32();
            for (var i = 0; i < m_PackedSpritesSize; i++)
            {
                reader.ReadPPtr(); //PPtr<Sprite> data
            }

            int m_PackedSpriteNamesToIndexSize = reader.ReadInt32();
            for (var i = 0; i < m_PackedSpriteNamesToIndexSize; i++)
            {
                reader.ReadAlignedString();
            }

            int m_RenderDataMapSize = reader.ReadInt32();
            this.m_RenderDataMap = new Dictionary<Tuple<Guid, long>, SpriteAtlasData>(m_RenderDataMapSize);
            for (var i = 0; i < m_RenderDataMapSize; i++)
            {
                var first = new Guid(reader.ReadBytes(16));
                long second = reader.ReadInt64();
                var value = new SpriteAtlasData(reader);
                this.m_RenderDataMap.Add(new Tuple<Guid, long>(first, second), value);
            }
            //string m_Tag
            //bool m_IsVariant
        }
    }
}