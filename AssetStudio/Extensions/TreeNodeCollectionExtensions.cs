using System;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace AssetStudio.Extensions
{
    public static class TreeNodeCollectionExtensions
    {
        // TreeNode.MAX_TREENODES_OPS
        internal const int MAX_TREENODES_OPS = 200;

        public static void AddRange(this TreeNodeCollection targetNodes, TreeNodeCollection sourceNodes, TreeView treeView = null)
        {
            if (sourceNodes == null)
            {
                throw new ArgumentNullException(nameof(sourceNodes));
            }

            if (sourceNodes.Count == 0)
            {
                return;
            }

            bool shouldOptimize = treeView != null && sourceNodes.Count > MAX_TREENODES_OPS;

            if (shouldOptimize)
            {
                treeView.BeginUpdate();
            }

            foreach (TreeNode sourceNode in sourceNodes)
            {
                targetNodes.Add(sourceNode);
            }

            if (shouldOptimize)
            {
                treeView.EndUpdate();
            }
        }
    }
}
