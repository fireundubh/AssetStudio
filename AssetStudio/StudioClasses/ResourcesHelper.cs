using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace AssetStudio
{
    public static class ResourcesHelper
    {
        public static byte[] GetData(string path, string sourceFilePath, long offset, int size)
        {
            string resourceFileName = Path.GetFileName(path);

            Debug.Assert(resourceFileName != null, nameof(resourceFileName) + " != null");

            if (Studio.resourceFileReaders.TryGetValue(resourceFileName.ToUpper(), out EndianBinaryReader reader))
            {
                reader.Position = offset;
                return reader.ReadBytes(size);
            }

            string resourceFilePath = Path.GetDirectoryName(sourceFilePath) + "\\" + resourceFileName;

            if (!File.Exists(resourceFilePath))
            {
                string sourceDirectoryName = Path.GetDirectoryName(sourceFilePath) ?? throw new InvalidOperationException();
                string[] findFiles = Directory.GetFiles(sourceDirectoryName, resourceFileName, SearchOption.AllDirectories);

                if (findFiles.Length > 0)
                {
                    resourceFilePath = findFiles[0];
                }
            }

            if (File.Exists(resourceFilePath))
            {
                using (var resourceReader = new BinaryReader(File.OpenRead(resourceFilePath)))
                {
                    resourceReader.BaseStream.Position = offset;
                    return resourceReader.ReadBytes(size);
                }
            }

            MessageBox.Show(string.Format("Cannot find the resource file: {0}", resourceFileName));
            return null;
        }
    }
}