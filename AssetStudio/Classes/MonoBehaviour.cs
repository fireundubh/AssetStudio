using System.Collections.Generic;
using System.Windows.Forms;
using AssetStudio.Extensions;
using AssetStudio.Properties;
using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public sealed class MonoBehaviour : Behaviour
    {
        public PPtr m_Script;
        public string m_Name;

        private const string gameObjectKey = "m_GameObject";
        private const string scriptKey = "m_Script";

        public TreeNode RootNode
        {
            get
            {
                var rootNode = new TreeNode();

                NodeHelper.CreatePointerNode(rootNode, "GameObject", gameObjectKey, this.m_GameObject, out TreeNode _);

                NodeHelper.AddKeyedNode(rootNode, ref this.m_Enabled, Resources.Behaviour_Enabled_Format);

                NodeHelper.CreatePointerNode(rootNode, "MonoScript", scriptKey, this.m_Script, out TreeNode _);

                NodeHelper.AddKeyedNode(rootNode, ref this.m_Name, Resources.NamedObject_Name_Format);

                return rootNode;
            }
        }

        public List<string> RootNodeText
        {
            get
            {
                return NodeHelper.ToStringList(this.RootNode);
            }
        }

        public MonoBehaviour(ObjectReader reader) : base(reader)
        {
            this.m_Script = reader.ReadPPtr();
            this.m_Name = reader.ReadAlignedString();
        }
    }
}