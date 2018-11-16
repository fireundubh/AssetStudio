using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public class FormController
    {
        public static Action<Dictionary<ObjectReader, AssetItem>, bool, bool> BuildAssetList;
        public static Action<Dictionary<ObjectReader, AssetItem>> BuildTreeStructure;
        public static Action<Dictionary<ObjectReader, AssetItem>> BuildClassStructures;

        public static Action<string> StatusStripUpdate;

        public static Action<int> ProgressBarIncrementMaximum;
        public static Action ProgressBarPerformStep;
        public static Action<int> ProgressBarReset;
        public static Action<int> ProgressBarSetValue;

//        bool[] fileDataParams =
//        {
//            this.dontLoadAssetsMenuItem.Checked,
//            this.displayAll.Checked,
//            this.dontBuildHierarchyMenuItem.Checked,
//            this.buildClassStructuresMenuItem.Checked,
//            this.displayOriginalName.Checked
//        };

        public static void GenerateAssetData(params bool[] parameters)
        {
            var tempDic = new Dictionary<ObjectReader, AssetItem>();

            // first loop - read asset data & create list
            if (parameters[0])
            {
                int assetsFileCount = Studio.assetsFileList.Sum(x => x.ObjectReaders.Values.Count);

                ProgressBarReset(assetsFileCount);

                StatusStripUpdate("Building asset list...");

                BuildAssetList(tempDic, parameters[1], parameters[4]);
            }

            // second loop - build tree structure
            if (parameters[2])
            {
                int gameObjectCount = Studio.assetsFileList.Sum(x => x.GameObjects.Values.Count);

                ProgressBarReset(gameObjectCount);

                if (gameObjectCount > 0)
                {
                    StatusStripUpdate("Building tree structure...");

                    BuildTreeStructure(tempDic);
                }
            }

            tempDic.Clear();

            // third loop - build list of class structures
            if (parameters[3])
            {
                StatusStripUpdate("Building class structures...");

                BuildClassStructures(tempDic);
            }
        }

        public static void LoadFileData(params bool[] parameters)
        {
            if (Studio.assetsFileList.Count == 0)
            {
                StatusStripUpdate("No file was loaded.");
                return;
            }

            GenerateAssetData(!parameters[0], parameters[1], !parameters[2], parameters[3], parameters[4]);
        }

        private static void PopulateListView(ListView listView, Dictionary<string, SortedDictionary<int, TypeTreeItem>> allTypeMap)
        {
            if (listView == null)
            {
                return;
            }

            listView.BeginUpdate();

            foreach (KeyValuePair<string, SortedDictionary<int, TypeTreeItem>> version in allTypeMap)
            {
                var versionGroup = new ListViewGroup(version.Key);

                listView.Groups.Add(versionGroup);

                foreach (KeyValuePair<int, TypeTreeItem> uclass in version.Value)
                {
                    uclass.Value.Group = versionGroup;
                    listView.Items.Add(uclass.Value);
                }
            }

            listView.EndUpdate();
        }

        private static void PopulateTreeView(TreeView treeView)
        {
            if (treeView == null)
            {
                return;
            }

            treeView.BeginUpdate();

            if (Studio.treeNodeCollection != null)
            {
                GameObjectTreeNode[] treeNodeArray = Studio.treeNodeCollection.ToArray<GameObjectTreeNode>();

                treeView.Nodes.AddRange(treeNodeArray.ToArray<TreeNode>());

                foreach (TreeNode node in treeView.Nodes)
                {
                    node.HideCheckBox();
                }
            }

            treeView.EndUpdate();
        }

        private static void PopulateDropDownMenu(ToolStripItemCollection toolStripItemCollection, List<AssetItem> exportableAssets, EventHandler menuItemClickEventHandler)
        {
            List<ClassIDType> classTypes = exportableAssets.Select(x => x.Type).Distinct().OrderBy(x => x.ToString()).ToList();

            foreach (ClassIDType classType in classTypes)
            {
                string classTypeName = classType.ToString();

                var toolStripMenuItem = new ToolStripMenuItem
                {
                    CheckOnClick = true,
                    Name = classTypeName,
                    Size = new Size(180, 22),
                    Text = classTypeName
                };

                toolStripMenuItem.Click += menuItemClickEventHandler;

                toolStripItemCollection.Add(toolStripMenuItem);
            }
        }

        public static void SetUpAssetControls(Form owner, ListView assetListView, ListView classesListView, TreeView sceneTreeView, params bool[] parameters)
        {
            string productNameText = !string.IsNullOrEmpty(Studio.productName) ? Studio.productName : "no productName";

            owner.Text = string.Format("AssetStudio - {0} - {1} - {2}", productNameText, Studio.assetsFileList[0].unityVersion, Studio.assetsFileList[0].platformStr);

            // this.dontLoadAssetsMenuItem.Checked
            if (!parameters[0])
            {
                assetListView.VirtualListSize = Studio.visibleAssets.Count;

                // will only work if ListView is visible
                // this.ResizeAssetListColumns();
            }

            // this.dontBuildHierarchyMenuItem.Checked
            if (!parameters[2])
            {
                PopulateTreeView(sceneTreeView);
            }

            // this.buildClassStructuresMenuItem.Checked
            if (parameters[3])
            {
                PopulateListView(classesListView, Studio.AllTypeMap);
            }
        }

        public static void SetUpDropDownMenu(ToolStripItemCollection dropDownItems, EventHandler dropDownItemEvent)
        {
            PopulateDropDownMenu(dropDownItems, Studio.exportableAssets, dropDownItemEvent);

            ((ToolStripMenuItem) dropDownItems["allToolStripMenuItem"]).Checked = true;
        }
    }
}