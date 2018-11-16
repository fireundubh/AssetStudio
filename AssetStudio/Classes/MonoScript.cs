using System.Collections.Generic;
using System.Windows.Forms;
using AssetStudio.Extensions;
using AssetStudio.Properties;
using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public sealed class MonoScript : NamedObject
    {
        public int m_ExecutionOrder;
        public string m_ClassName;
        public string m_Namespace = "<root>";
        public string m_AssemblyName;
        public bool m_IsEditorScript;

        private const string scriptKey = "m_Script";

        public string BasePath
        {
            get
            {
                return this.m_Namespace == string.Empty ? this.m_ClassName : $"{this.m_Namespace}.{this.m_ClassName}";
            }
        }

        public string QualifiedPath
        {
            get
            {
                return this.m_Namespace == string.Empty ? $"{this.m_ClassName}.{this.m_Name}" : $"{this.m_Namespace}.{this.m_ClassName}.{this.m_Name}";
            }
        }

        public TreeNode RootNode
        {
            get
            {
                var rootNode = new TreeNode();

                rootNode.Nodes.Add(scriptKey, Resources.PPtr_MonoScript);

                if (this.version[0] > 3 || this.version[0] == 3 && this.version[1] >= 4)
                {
                    NodeHelper.AddKeyedChildNode(rootNode, scriptKey, ref this.m_ExecutionOrder, Resources.MonoScript_ExecutionOrder_Format);
                }

                NodeHelper.AddKeyedChildNode(rootNode, scriptKey, ref this.m_ClassName, Resources.MonoScript_ClassName_Format);

                if (this.version[0] >= 3)
                {
                    NodeHelper.AddKeyedChildNode(rootNode, scriptKey, ref this.m_Namespace, Resources.MonoScript_Namespace_Format);
                }

                NodeHelper.AddKeyedChildNode(rootNode, scriptKey, ref this.m_AssemblyName, Resources.MonoScript_AssemblyName_Format);

                if (this.version[0] < 2018 || this.version[0] == 2018 && this.version[1] < 2)
                {
                    NodeHelper.AddKeyedChildNode(rootNode, scriptKey, ref this.m_IsEditorScript, Resources.MonoScript_IsEditorScript_Format);
                }

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

        public MonoScript(ObjectReader reader) : base(reader)
        {
            if (this.version[0] > 3 || this.version[0] == 3 && this.version[1] >= 4) //3.4 and up
            {
                this.m_ExecutionOrder = reader.ReadInt32();
            }
            if (this.version[0] < 5) //5.0 down
            {
                uint m_PropertiesHash = reader.ReadUInt32();
            }
            else
            {
                byte[] m_PropertiesHash = reader.ReadBytes(16);
            }
            if (this.version[0] < 3) //3.0 down
            {
                string m_PathName = reader.ReadAlignedString();
            }
            this.m_ClassName = reader.ReadAlignedString();
            if (this.version[0] >= 3) //3.0 and up
            {
                this.m_Namespace = reader.ReadAlignedString();
            }
            this.m_AssemblyName = reader.ReadAlignedString();
            if (this.version[0] < 2018 || this.version[0] == 2018 && this.version[1] < 2) //2018.2 down
            {
                this.m_IsEditorScript = reader.ReadBoolean();
            }
        }
    }
}