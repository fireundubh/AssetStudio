using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;
using AssetStudio.Properties;
using AssetStudio.StudioClasses;

namespace AssetStudio
{
    static class Exporter
    {
        public static bool ExportTexture2D(ObjectReader reader, string exportPathName, bool flip)
        {
            var m_Texture2D = new Texture2D(reader, true);

            if (m_Texture2D.image_data == null || m_Texture2D.image_data.Length == 0)
            {
                return false;
            }

            var converter = new Texture2DConverter(m_Texture2D);
            var convertTexture = (bool) Settings.Default["convertTexture"];

            if (convertTexture)
            {
                Bitmap bitmap = converter.ConvertToBitmap(flip);

                if (bitmap == null)
                {
                    return false;
                }

                ImageFormat format = null;

                var ext = (string) Settings.Default["convertType"];

                switch (ext)
                {
                    case "BMP":
                        format = ImageFormat.Bmp;
                        break;
                    case "PNG":
                        format = ImageFormat.Png;
                        break;
                    case "JPEG":
                        format = ImageFormat.Jpeg;
                        break;
                }

                string exportFullName = Path.Combine(exportPathName, string.Concat(reader.exportName, ".", ext.ToLower()));

                if (ExportFileExists(exportFullName))
                {
                    return false;
                }

                bitmap.Save(exportFullName, format ?? throw new InvalidOperationException("format was null"));
                bitmap.Dispose();

                return true;
            }
            else
            {
                string exportFullName = Path.Combine(exportPathName, string.Concat(reader.exportName, converter.GetExtensionName()));

                if (ExportFileExists(exportFullName))
                {
                    return false;
                }

                File.WriteAllBytes(exportFullName, converter.ConvertToContainer());

                return true;
            }
        }

        public static bool ExportAudioClip(ObjectReader reader, string exportPath)
        {
            var m_AudioClip = new AudioClip(reader, true);

            if (m_AudioClip.m_AudioData == null)
            {
                return false;
            }

            var convertAudio = (bool) Settings.Default["convertAudio"];
            var converter = new AudioClipConverter(m_AudioClip);

            if (convertAudio && converter.IsFMODSupport)
            {
                string exportFullName = Path.Combine(exportPath, string.Concat(reader.exportName, ".wav"));

                if (ExportFileExists(exportFullName))
                {
                    return false;
                }

                byte[] buffer = converter.ConvertToWav();

                if (buffer == null)
                {
                    return false;
                }

                File.WriteAllBytes(exportFullName, buffer);
            }
            else
            {
                string exportFullName = exportPath + reader.exportName + converter.GetExtensionName();

                if (ExportFileExists(exportFullName))
                {
                    return false;
                }

                File.WriteAllBytes(exportFullName, m_AudioClip.m_AudioData);
            }
            return true;
        }

        public static bool ExportShader(ObjectReader reader, string exportPath)
        {
            var m_Shader = new Shader(reader);

            string exportFullName = Path.Combine(exportPath, string.Concat(reader.exportName, ".shader"));

            if (ExportFileExists(exportFullName))
            {
                return false;
            }

            string str = ShaderConverter.Convert(m_Shader);
            File.WriteAllText(exportFullName, str ?? "Serialized Shader can't be read");

            return true;
        }

        public static bool ExportTextAsset(ObjectReader reader, string exportPath)
        {
            var m_TextAsset = new TextAsset(reader);

            string exportFullName = Path.Combine(exportPath, string.Concat(reader.exportName, ".txt"));

            if (ExportFileExists(exportFullName))
            {
                return false;
            }

            File.WriteAllBytes(exportFullName, m_TextAsset.m_Script);

            return true;
        }

        public static string GetExportMonoScriptPath(ObjectReader reader, string exportPath, string fileExtension = ".txt")
        {
            var m_Script = new MonoScript(reader);

            if (m_Script.m_Namespace == string.Empty)
            {
                return Path.Combine(exportPath, m_Script.m_ClassName, string.Concat(reader.exportName, fileExtension));
            }

            return Path.Combine(exportPath, m_Script.m_Namespace, m_Script.m_ClassName, string.Concat(reader.exportName, fileExtension));
        }

        public static string GetExportMonoBehaviourPath(ObjectReader reader, string exportPath, string fileExtension = ".txt")
        {
            string exportFullName = Path.Combine(exportPath, string.Concat(reader.exportName, fileExtension));

            var m_MonoBehaviour = new MonoBehaviour(reader);

            if (!m_MonoBehaviour.m_Script.TryGet(out ObjectReader script))
            {
                return exportFullName;
            }

            var m_Script = new MonoScript(script);

            if (m_Script.m_Namespace == string.Empty)
            {
                return Path.Combine(exportPath, m_Script.m_ClassName, string.Concat(reader.exportName, fileExtension));
            }

            return Path.Combine(exportPath, m_Script.m_Namespace, m_Script.m_ClassName, string.Concat(reader.exportName, fileExtension));
        }

        public static bool ExportFont(ObjectReader reader, string exportPath)
        {
            var m_Font = new Font(reader);

            if (m_Font.m_FontData == null)
            {
                return false;
            }

            var extension = ".ttf";

            if (m_Font.m_FontData[0] == 79 && m_Font.m_FontData[1] == 84 && m_Font.m_FontData[2] == 84 && m_Font.m_FontData[3] == 79)
            {
                extension = ".otf";
            }

            string exportFullName = Path.Combine(exportPath, string.Concat(reader.exportName, extension));

            if (ExportFileExists(exportFullName))
            {
                return false;
            }

            File.WriteAllBytes(exportFullName, m_Font.m_FontData);

            return true;
        }

        public static bool ExportMesh(ObjectReader reader, string exportPath)
        {
            var m_Mesh = new Mesh(reader);

            if (m_Mesh.m_VertexCount <= 0)
            {
                return false;
            }

            string exportFullName = Path.Combine(exportPath, string.Concat(reader.exportName, ".obj"));

            if (ExportFileExists(exportFullName))
            {
                return false;
            }

            var sb = new StringBuilder();

            sb.AppendLine("g " + m_Mesh.m_Name);

            #region Vertices

            if (m_Mesh.m_Vertices == null || m_Mesh.m_Vertices.Length == 0)
            {
                return false;
            }

            var c = 3;
            if (m_Mesh.m_Vertices.Length == m_Mesh.m_VertexCount * 4)
            {
                c = 4;
            }

            for (var v = 0; v < m_Mesh.m_VertexCount; v++)
            {
                sb.AppendFormat("v {0} {1} {2}\r\n", -m_Mesh.m_Vertices[v * c], m_Mesh.m_Vertices[v * c + 1], m_Mesh.m_Vertices[v * c + 2]);
            }

            #endregion

            #region UV

            if (m_Mesh.m_UV1 != null && m_Mesh.m_UV1.Length == m_Mesh.m_VertexCount * 2)
            {
                for (var v = 0; v < m_Mesh.m_VertexCount; v++)
                {
                    sb.AppendFormat("vt {0} {1}\r\n", m_Mesh.m_UV1[v * 2], m_Mesh.m_UV1[v * 2 + 1]);
                }
            }
            else if (m_Mesh.m_UV2 != null && m_Mesh.m_UV2.Length == m_Mesh.m_VertexCount * 2)
            {
                for (var v = 0; v < m_Mesh.m_VertexCount; v++)
                {
                    sb.AppendFormat("vt {0} {1}\r\n", m_Mesh.m_UV2[v * 2], m_Mesh.m_UV2[v * 2 + 1]);
                }
            }

            #endregion

            #region Normals

            if (m_Mesh.m_Normals != null && m_Mesh.m_Normals.Length > 0)
            {
                if (m_Mesh.m_Normals.Length == m_Mesh.m_VertexCount * 3)
                {
                    c = 3;
                }
                else if (m_Mesh.m_Normals.Length == m_Mesh.m_VertexCount * 4)
                {
                    c = 4;
                }
                for (var v = 0; v < m_Mesh.m_VertexCount; v++)
                {
                    sb.AppendFormat("vn {0} {1} {2}\r\n", -m_Mesh.m_Normals[v * c], m_Mesh.m_Normals[v * c + 1], m_Mesh.m_Normals[v * c + 2]);
                }
            }

            #endregion

            #region Face

            var sum = 0;
            for (var i = 0; i < m_Mesh.m_SubMeshes.Count; i++)
            {
                sb.AppendLine($"g {m_Mesh.m_Name}_{i}");
                var indexCount = (int) m_Mesh.m_SubMeshes[i].indexCount;
                int end = sum + indexCount / 3;
                for (int f = sum; f < end; f++)
                {
                    sb.AppendFormat("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\r\n", m_Mesh.m_Indices[f * 3 + 2] + 1, m_Mesh.m_Indices[f * 3 + 1] + 1, m_Mesh.m_Indices[f * 3] + 1);
                }
                sum = end;
            }

            #endregion

            sb.Replace("NaN", "0");
            File.WriteAllText(exportFullName, sb.ToString());
            return true;
        }

        public static bool ExportVideoClip(ObjectReader reader, string exportPath)
        {
            var m_VideoClip = new VideoClip(reader, true);

            if (m_VideoClip.m_VideoData == null)
            {
                return false;
            }

            string exportFullName = Path.Combine(exportPath, string.Concat(reader.exportName, Path.GetExtension(m_VideoClip.m_OriginalPath)));

            if (ExportFileExists(exportFullName))
            {
                return false;
            }

            File.WriteAllBytes(exportFullName, m_VideoClip.m_VideoData);

            return true;
        }

        public static bool ExportMovieTexture(ObjectReader reader, string exportPath)
        {
            var m_MovieTexture = new MovieTexture(reader);

            string exportFullName = Path.Combine(exportPath, string.Concat(reader.exportName, ".ogv"));

            if (ExportFileExists(exportFullName))
            {
                return false;
            }

            File.WriteAllBytes(exportFullName, m_MovieTexture.m_MovieData);

            return true;
        }

        public static bool ExportSprite(ObjectReader reader, string exportPath)
        {
            ImageFormat format = null;

            var type = (string) Settings.Default["convertType"];

            switch (type)
            {
                case "BMP":
                    format = ImageFormat.Bmp;
                    break;
                case "PNG":
                    format = ImageFormat.Png;
                    break;
                case "JPEG":
                    format = ImageFormat.Jpeg;
                    break;
            }

            string exportFullName = Path.Combine(exportPath, string.Concat(reader.exportName, ".", type.ToLower()));

            if (ExportFileExists(exportFullName))
            {
                return false;
            }

            Bitmap bitmap = SpriteHelper.GetImageFromSprite(new Sprite(reader));

            if (bitmap == null || format == null)
            {
                return false;
            }

            bitmap.Save(exportFullName, format);
            bitmap.Dispose();

            return true;
        }

        public static bool ExportRawFile(ObjectReader reader, string exportPath, string fileExtension = ".dat")
        {
            string exportFullName = Path.Combine(exportPath, string.Concat(reader.exportName, fileExtension));

            switch (reader.type)
            {
                case ClassIDType.MonoBehaviour:
                    exportFullName = GetExportMonoBehaviourPath(reader, exportPath, fileExtension);
                    break;
                case ClassIDType.MonoScript:
                    exportFullName = GetExportMonoScriptPath(reader, exportPath, fileExtension);
                    break;
            }

            if (ExportFileExists(exportFullName))
            {
                return false;
            }

            byte[] bytes = reader.GetRawData();

            File.WriteAllBytes(exportFullName, bytes);

            return true;
        }

        private static bool ExportFileExists(string filePath)
        {
            if (File.Exists(filePath))
            {
                string fileName = Path.GetFileName(filePath);

                DialogResult result = MessageBox.Show(string.Format(Resources.Exporter_FileExistsPrompt_Text, fileName), Resources.Exporter_FileExistsPrompt_Caption, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (result == DialogResult.No)
                {
                    return true;
                }

                File.Delete(filePath);
            }

            string directoryPath = Path.GetDirectoryName(filePath);

            if (directoryPath != null)
            {
                Directory.CreateDirectory(directoryPath);
            }

            return false;
        }

        public static bool ExportAnimator(ObjectReader animator, string exportPath, List<AssetItem> animationList = null)
        {
            var m_Animator = new Animator(animator);

            ModelConverter convert = animationList != null ? new ModelConverter(m_Animator, animationList) : new ModelConverter(m_Animator);

            exportPath = Path.Combine(exportPath, string.Concat(Studio.FixFileName(animator.exportName), ".fbx"));

            return ModelConverter(convert, exportPath);
        }

        public static bool ExportGameObject(GameObject gameObject, string exportPath, List<AssetItem> animationList = null)
        {
            ModelConverter convert = animationList != null ? new ModelConverter(gameObject, animationList) : new ModelConverter(gameObject);

            exportPath = Path.Combine(exportPath, string.Concat(Studio.FixFileName(gameObject.m_Name), ".fbx"));

            return ModelConverter(convert, exportPath);
        }

        private static bool ModelConverter(ModelConverter convert, string exportPath)
        {
            var eulerFilter = (bool) Settings.Default["EulerFilter"];
            var filterPrecision = (float) (decimal) Settings.Default["filterPrecision"];
            var allFrames = (bool) Settings.Default["allFrames"];
            var allBones = (bool) Settings.Default["allBones"];
            var skins = (bool) Settings.Default["skins"];
            var boneSize = (int) (decimal) Settings.Default["boneSize"];
            var scaleFactor = (float) (decimal) Settings.Default["scaleFactor"];
            var flatInbetween = (bool) Settings.Default["flatInbetween"];
            var fbxVersion = (int) Settings.Default["fbxVersion"];
            var fbxFormat = (int) Settings.Default["fbxFormat"];

            Fbx.Exporter.Export(exportPath, convert, eulerFilter, filterPrecision, allFrames, allBones, skins, boneSize, scaleFactor, flatInbetween, fbxVersion, fbxFormat == 1);

            return true;
        }
    }
}