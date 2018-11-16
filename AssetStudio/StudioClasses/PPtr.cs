using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public class PPtr
    {
        public int m_FileID;
        public long m_PathID;

        //custom
        public AssetsFile assetsFile;
        public int index = -2; //-2 - Prepare, -1 - Missing

        public string ID
        {
            get
            {
                return string.Format("{{m_FileID: {0}, m_PathID: {1}}}", this.m_FileID, this.m_PathID);
            }
        }

        private bool TryGetAssetsFile(out AssetsFile result)
        {
            result = null;
            if (this.m_FileID == 0)
            {
                result = this.assetsFile;
                return true;
            }

            if (this.m_FileID > 0 && this.m_FileID - 1 < this.assetsFile.m_Externals.Count)
            {
                if (this.index == -2)
                {
                    FileIdentifier m_External = this.assetsFile.m_Externals[this.m_FileID - 1];
                    string name = m_External.fileName.ToUpper();
                    if (!Studio.assetsFileIndexCache.TryGetValue(name, out this.index))
                    {
                        this.index = Studio.assetsFileList.FindIndex(x => x.upperFileName == name);
                        Studio.assetsFileIndexCache.Add(name, this.index);
                    }
                }

                if (this.index >= 0)
                {
                    result = Studio.assetsFileList[this.index];
                    return true;
                }
            }

            return false;
        }

        public bool TryGet(out ObjectReader result)
        {
            result = null;
            if (this.TryGetAssetsFile(out AssetsFile sourceFile))
            {
                if (sourceFile.ObjectReaders.TryGetValue(this.m_PathID, out result))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetTransform(out Transform m_Transform)
        {
            if (this.TryGetAssetsFile(out AssetsFile sourceFile))
            {
                if (sourceFile.Transforms.TryGetValue(this.m_PathID, out m_Transform))
                {
                    return true;
                }
            }

            m_Transform = null;
            return false;
        }

        public bool TryGetGameObject(out GameObject m_GameObject)
        {
            if (this.TryGetAssetsFile(out AssetsFile sourceFile))
            {
                if (sourceFile.GameObjects.TryGetValue(this.m_PathID, out m_GameObject))
                {
                    return true;
                }
            }

            m_GameObject = null;
            return false;
        }
    }
}