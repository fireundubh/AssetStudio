using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using AssetStudio.StudioClasses;

namespace AssetStudio
{
    static class SpriteHelper
    {
        public static Bitmap GetImageFromSprite(Sprite m_Sprite)
        {
            if (m_Sprite.m_SpriteAtlas != null && m_Sprite.m_SpriteAtlas.TryGet(out ObjectReader objectReader))
            {
                var m_SpriteAtlas = new SpriteAtlas(objectReader);
                if (m_SpriteAtlas.m_RenderDataMap.TryGetValue(m_Sprite.m_RenderDataKey, out SpriteAtlasData spriteAtlasData) && spriteAtlasData.texture.TryGet(out objectReader))
                {
                    return CutImage(objectReader, spriteAtlasData.textureRect, m_Sprite, spriteAtlasData.settingsRaw);
                }
            }
            else
            {
                if (m_Sprite.texture.TryGet(out objectReader))
                {
                    return CutImage(objectReader, m_Sprite.textureRect, m_Sprite, m_Sprite.settingsRaw);
                }
            }
            return null;
        }

        private static Bitmap CutImage(ObjectReader texture2DAsset, RectangleF textureRect, Sprite m_Sprite, SpriteSettings settingsRaw)
        {
            var texture2D = new Texture2DConverter(new Texture2D(texture2DAsset, true));
            Bitmap originalImage = texture2D.ConvertToBitmap(false);

            if (originalImage == null)
            {
                return null;
            }

            using (originalImage)
            {
                Bitmap spriteImage = originalImage.Clone(textureRect, PixelFormat.Format32bppArgb);

                //RotateAndFlip
                var flipType = RotateFlipType.RotateNoneFlipNone;

                switch (settingsRaw.packingRotation)
                {
                    case SpritePackingRotation.kSPRFlipHorizontal:
                        flipType = RotateFlipType.RotateNoneFlipX;
                        break;
                    case SpritePackingRotation.kSPRFlipVertical:
                        flipType = RotateFlipType.RotateNoneFlipY;
                        break;
                    case SpritePackingRotation.kSPRRotate180:
                        flipType = RotateFlipType.Rotate180FlipNone;
                        break;
                    case SpritePackingRotation.kSPRRotate90:
                        flipType = RotateFlipType.Rotate270FlipNone;
                        break;
                }

                spriteImage.RotateFlip(flipType);

                // TODO Tight
                // * 2017之前没有PhysicsShape
                // * 5.6之前使用vertices
                // * 5.6需要使用VertexData
                if (settingsRaw.packingMode == SpritePackingMode.kSPMTight && m_Sprite.m_PhysicsShape?.Length > 0) //Tight
                {
                    try
                    {
                        using (var brush = new TextureBrush(spriteImage))
                        {
                            using (var path = new GraphicsPath())
                            {
                                foreach (PointF[] p in m_Sprite.m_PhysicsShape)
                                {
                                    path.AddPolygon(p);
                                }

                                using (var matr = new Matrix())
                                {
                                    matr.Translate(m_Sprite.m_Rect.Width * m_Sprite.m_Pivot.X, m_Sprite.m_Rect.Height * m_Sprite.m_Pivot.Y);
                                    matr.Scale(m_Sprite.m_PixelsToUnits, m_Sprite.m_PixelsToUnits);
                                    path.Transform(matr);

                                    var bitmap = new Bitmap((int) textureRect.Width, (int) textureRect.Height);

                                    using (Graphics graphic = Graphics.FromImage(bitmap))
                                    {
                                        graphic.FillPath(brush, path);
                                        bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);

                                        return bitmap;
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        spriteImage = originalImage.Clone(textureRect, PixelFormat.Format32bppArgb);
                        spriteImage.RotateFlip(RotateFlipType.RotateNoneFlipY);

                        return spriteImage;
                    }
                }

                //Rectangle
                spriteImage.RotateFlip(RotateFlipType.RotateNoneFlipY);

                return spriteImage;
            }
        }
    }
}