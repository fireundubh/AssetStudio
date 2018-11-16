using System.Windows.Forms;
using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public class AssetItem : ListViewItem
    {
        public AssetsFile sourceFile;
        public ObjectReader reader;
        public long FullSize;
        public ClassIDType Type;
        public string TypeString;
        public string InfoText;
        public string UniqueID;
        public GameObject gameObject;

        public AssetItem(ObjectReader reader)
        {
            this.sourceFile = reader.assetsFile;
            this.reader = reader;
            this.FullSize = reader.byteSize;
            this.Type = reader.type;
            this.TypeString = this.Type.ToString();
        }
    }
}