using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using AssetStudio.Properties;
using dnlib.DotNet;

namespace AssetStudio.StudioClasses
{
    public static class NodeHelper
    {
        private static void AddChildToList(string text, List<string> list)
        {
            list.Add(string.Concat("\t", text));
        }

        public static List<string> ToStringList(TreeNode rootNode)
        {
            var strings = new List<string>();

            foreach (TreeNode node in rootNode.Nodes)
            {
                if (node.Parent != null)
                {
                    strings.Add(node.Text);

                    foreach (TreeNode childNode in node.Nodes)
                    {
                        AddChildToList(childNode.Text, strings);
                    }
                }
                else
                {
                    AddChildToList(node.Text, strings);
                }
            }

            strings.Add(Environment.NewLine);

            return strings;
        }

        public static void CreatePointerNode(TreeNode rootNode, string typeName, string pointerName, PPtr pointerObject, out TreeNode node)
        {
            node = rootNode.Nodes.Add(pointerName, string.Format(Resources.PPtr_Generic_Format, typeName, pointerName));
            NodeHelper.AddKeyedChildNode(rootNode, pointerName, ref pointerObject.m_FileID, Resources.PPtr_FileID_Format);
            NodeHelper.AddKeyedChildNode(rootNode, pointerName, ref pointerObject.m_PathID, Resources.PPtr_PathID_Format);
        }

        public static void CreatePointerNode(TreeNode rootNode, TypeDef typeDef, string pointerName, PPtr pointerObject, out TreeNode node)
        {
            node = rootNode.Nodes.Add(pointerName, string.Format(Resources.PPtr_Generic_Format, typeDef.Name, pointerName));
            NodeHelper.AddKeyedChildNode(rootNode, pointerName, ref pointerObject.m_FileID, Resources.PPtr_FileID_Format);
            NodeHelper.AddKeyedChildNode(rootNode, pointerName, ref pointerObject.m_PathID, Resources.PPtr_PathID_Format);
        }

        public static TreeNode BuildNode(string name, string nodeText, ElementType tag)
        {
            return new TreeNode
            {
                Name = name,
                Text = nodeText,
                Tag = tag,
                ToolTipText = name
            };
        }

        public static void CreateKeyNode(TreeNode rootNode, TypeDef typeDef, TypeSig typeSig, string name, bool isRoot, bool isArray, int arrayIndex, out TreeNode node)
        {
            string nodeText = !isArray ? string.Format("{0} {1}", typeDef.Name, name) : string.Format("[{0}] {1} {2}", arrayIndex, typeDef.Name, name);

            node = BuildNode(name, nodeText, typeSig.ElementType);

            if (!isRoot)
            {
                rootNode.Nodes.Add(node);
            }
        }

        public static void CreateValueNode(TreeNode rootNode, TypeDef typeDef, TypeSig typeSig, string name, object value, bool isRoot, bool isArray, int arrayIndex, out TreeNode node)
        {
            string nodeText = !isArray ? string.Format("{0} {1} = {2}", typeDef.Name, name, value) : string.Format("[{0}] {1} {2} = {3}", arrayIndex, typeDef.Name, name, value);

            node = BuildNode(name, nodeText, typeSig.ElementType);

            if (!isRoot)
            {
                rootNode.Nodes.Add(node);
            }
        }

        public static TreeNode AddKeyedNode<T>(TreeNode rootNode, ref T nodeText, string nodeTextFormat = null)
        {
            // TODO: confusing var names
            string text = nodeTextFormat == null ? nodeText.ToString() : string.Format(nodeTextFormat, nodeText);
            return rootNode.Nodes.Add(nameof(nodeText), text);
        }

        public static TreeNode AddKeyedChildNode<T>(TreeNode rootNode, string parentKey, ref T childText, string childTextFormat)
        {
            TreeNode parentNode = rootNode.Nodes.Find(parentKey, true).FirstOrDefault();

            if (parentNode != null)
            {
                return parentNode.Nodes.Add(string.Concat(parentKey, ".", nameof(childText)), string.Format(childTextFormat, childText));
            }

            Logging.LoggingHelper.LogWarn(string.Format("Call to AddKeyedChildNode could not find parentNode with key {0}", parentKey));

            return rootNode.Nodes.Add(string.Concat(parentKey, ".", nameof(childText)), string.Format(childTextFormat, childText));
        }
    }
}