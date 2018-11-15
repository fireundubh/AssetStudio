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

			    rootNode.Nodes.Add(gameObjectKey, Resources.PPtr_GameObject);
			    NodeHelper.AddKeyedChildNode(rootNode, gameObjectKey, ref this.m_GameObject.m_FileID, Resources.PPtr_FileID_Format);
			    NodeHelper.AddKeyedChildNode(rootNode, gameObjectKey, ref this.m_GameObject.m_PathID, Resources.PPtr_PathID_Format);

			    NodeHelper.AddKeyedNode(rootNode, ref this.m_Enabled, Resources.Behaviour_Enabled_Format);

			    rootNode.Nodes.Add(scriptKey, Resources.PPtr_MonoScript);
			    NodeHelper.AddKeyedChildNode(rootNode, scriptKey, ref this.m_Script.m_FileID, Resources.PPtr_FileID_Format);
			    NodeHelper.AddKeyedChildNode(rootNode, scriptKey, ref this.m_Script.m_PathID, Resources.PPtr_PathID_Format);

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

	    public MonoBehaviour(AssetPreloadData preloadData) : base(preloadData)
        {
            m_Script = sourceFile.ReadPPtr();
            m_Name = reader.ReadAlignedString();
        }
    }
}
