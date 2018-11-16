using System.IO;
using AssetStudio.StudioClasses;

namespace AssetStudio.Extensions
{
    public static class StreamExtensions
    {
        public static void CopyTo(this Stream source, Stream destination, long size)
        {
            var buffer = new byte[Constants.BufferSize];

            for (long left = size; left > 0; left -= Constants.BufferSize)
            {
                int toRead;

                if (Constants.BufferSize < left)
                {
                    toRead = Constants.BufferSize;
                }
                else
                {
                    toRead = (int) left;
                }

                int read = source.Read(buffer, 0, toRead);
                destination.Write(buffer, 0, read);

                if (read != toRead)
                {
                    return;
                }
            }
        }
    }
}