using System.Collections.Generic;
using AssetStudio.Extensions;
using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public sealed class AssetBundle : NamedObject
    {
        public class AssetInfo
        {
            public int preloadIndex;
            public int preloadSize;
            public PPtr asset;
        }

        public class ContainerData
        {
            public string first;
            public AssetInfo second;
        }

        public List<ContainerData> m_Container = new List<ContainerData>();

        public AssetBundle(ObjectReader reader) : base(reader)
        {
            int size = reader.ReadInt32();

            for (var i = 0; i < size; i++)
            {
                reader.ReadPPtr();
            }

            size = reader.ReadInt32();

            for (var i = 0; i < size; i++)
            {
                var temp = new ContainerData
                {
                    first = reader.ReadAlignedString(),

                    second = new AssetInfo
                    {
                        preloadIndex = reader.ReadInt32(),
                        preloadSize = reader.ReadInt32(),
                        asset = reader.ReadPPtr()
                    }
                };

                this.m_Container.Add(temp);
            }
        }
    }
}