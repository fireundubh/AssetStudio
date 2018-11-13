using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

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

            throw new NullReferenceException(string.Format("Call to AddKeyedChildNode could not find parentNode with key {0}", parentKey));
        }
    }
}
