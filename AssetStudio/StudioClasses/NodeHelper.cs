﻿using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace AssetStudio.StudioClasses
{
	public class NodeHelper
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

			strings.Add("\n");

			return strings;
		}

		public static void AddKeyedNode(TreeNode rootNode, ref string nodeText, string nodeTextFormat = null)
		{
			if (nodeTextFormat == null)
			{
				rootNode.Nodes.Add(nameof(nodeText), nodeText);
			}
			else
			{
				rootNode.Nodes.Add(nameof(nodeText), string.Format(nodeTextFormat, nodeText));
			}
		}

		public static void AddKeyedNode(TreeNode rootNode, ref int nodeText, string nodeTextFormat = null)
		{
			if (nodeTextFormat == null)
			{
				rootNode.Nodes.Add(nameof(nodeText), nodeText.ToString());
			}
			else
			{
				rootNode.Nodes.Add(nameof(nodeText), string.Format(nodeTextFormat, nodeText));
			}
		}

		public static void AddKeyedNode(TreeNode rootNode, ref long nodeText, string nodeTextFormat = null)
		{
			if (nodeTextFormat == null)
			{
				rootNode.Nodes.Add(nameof(nodeText), nodeText.ToString());
			}
			else
			{
				rootNode.Nodes.Add(nameof(nodeText), string.Format(nodeTextFormat, nodeText));
			}
		}

		public static void AddKeyedNode(TreeNode rootNode, ref bool nodeText, string nodeTextFormat = null)
		{
			if (nodeTextFormat == null)
			{
				rootNode.Nodes.Add(nameof(nodeText), nodeText.ToString());
			}
			else
			{
				rootNode.Nodes.Add(nameof(nodeText), string.Format(nodeTextFormat, nodeText));
			}
		}

		public static void AddKeyedNode(TreeNode rootNode, ref byte nodeText, string nodeTextFormat = null)
		{
			if (nodeTextFormat == null)
			{
				rootNode.Nodes.Add(nameof(nodeText), nodeText.ToString());
			}
			else
			{
				rootNode.Nodes.Add(nameof(nodeText), string.Format(nodeTextFormat, nodeText));
			}
		}

		public static void AddKeyedChildNode(TreeNode rootNode, string parentKey, ref string childText, string childTextFormat)
		{
			TreeNode parentNode = rootNode.Nodes.Find(parentKey, true).FirstOrDefault();
			parentNode?.Nodes.Add(string.Concat(parentKey, ".", nameof(childText)), string.Format(childTextFormat, childText));
		}

		public static void AddKeyedChildNode(TreeNode rootNode, string parentKey, ref int childText, string childTextFormat)
		{
			TreeNode parentNode = rootNode.Nodes.Find(parentKey, true).FirstOrDefault();
			parentNode?.Nodes.Add(string.Concat(parentKey, ".", nameof(childText)), string.Format(childTextFormat, childText));
		}

		public static void AddKeyedChildNode(TreeNode rootNode, string parentKey, ref long childText, string childTextFormat)
		{
			TreeNode parentNode = rootNode.Nodes.Find(parentKey, true).FirstOrDefault();
			parentNode?.Nodes.Add(string.Concat(parentKey, ".", nameof(childText)), string.Format(childTextFormat, childText));
		}

		public static void AddKeyedChildNode(TreeNode rootNode, string parentKey, ref bool childText, string childTextFormat)
		{
			TreeNode parentNode = rootNode.Nodes.Find(parentKey, true).FirstOrDefault();
			parentNode?.Nodes.Add(string.Concat(parentKey, ".", nameof(childText)), string.Format(childTextFormat, childText));
		}

		public static void AddKeyedChildNode(TreeNode rootNode, string parentKey, ref byte childText, string childTextFormat)
		{
			TreeNode parentNode = rootNode.Nodes.Find(parentKey, true).FirstOrDefault();
			parentNode?.Nodes.Add(string.Concat(parentKey, ".", nameof(childText)), string.Format(childTextFormat, childText));
		}
	}
}
