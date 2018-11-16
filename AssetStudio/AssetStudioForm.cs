using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Text;
using AssetStudio.Extensions;
using AssetStudio.Properties;
using AssetStudio.StudioClasses;
using dnlib.DotNet;
using FMOD;
using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace AssetStudio
{
    internal partial class AssetStudioForm : Form
    {
        public FormController formController;

        public ProgressBarManager progressBarManager;

        private AssetItem lastSelectedItem;
        private AssetItem lastLoadedAsset;

        private FMOD.System system;
        private Sound sound;
        private Channel channel;
        private SoundGroup masterSoundGroup;
        private MODE loopMode = MODE.LOOP_OFF;
        private uint FMODlenms;
        private float FMODVolume = 0.8f;

        private Bitmap imageTexture;

        #region GLControl

        private bool glControlLoaded;
        private int mdx, mdy;
        private bool lmdown, rmdown;
        private int pgmID, pgmColorID, pgmBlackID;
        private int attributeVertexPosition;
        private int attributeNormalDirection;
        private int attributeVertexColor;
        private int uniformModelMatrix;
        private int uniformViewMatrix;
        private int uniformProjMatrix;
        private int vao;
        private Vector3[] vertexData;
        private Vector3[] normalData;
        private Vector3[] normal2Data;
        private Vector4[] colorData;
        private Matrix4 modelMatrixData;
        private Matrix4 viewMatrixData;
        private Matrix4 projMatrixData;
        private int[] indiceData;
        private int wireFrameMode;
        private int shadeMode;
        private int normalMode;

        #endregion

        //asset list sorting helpers
        private int firstSortColumn = -1;
        private int secondSortColumn;
        private bool reverseSort;
        private bool enableFiltering;

        //tree search
        private int nextGObject;
        private List<GameObjectTreeNode> treeSrcResults = new List<GameObjectTreeNode>();

        [DllImport("gdi32.dll")]
        private static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, [In] ref uint pcFonts);

        public AssetStudioForm()
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

            this.InitializeComponent();

            this.displayOriginalName.Checked = (bool) Settings.Default["displayOriginalName"];
            this.displayAll.Checked = (bool) Settings.Default["displayAll"];
            this.displayInfo.Checked = (bool) Settings.Default["displayInfo"];
            this.enableLiveSearch.Checked = (bool) Settings.Default["enableLiveSearch"];
            this.enablePreview.Checked = (bool) Settings.Default["enablePreview"];
            this.openAfterExport.Checked = (bool) Settings.Default["openAfterExport"];
            this.assetGroupOptions.SelectedIndex = (int) Settings.Default["assetGroupOption"];

            this.FMODinit();

            // UI
            this.formController = new FormController();
            this.progressBarManager = new ProgressBarManager();

            // programmatically replace ProgressBar with CustomProgressBar because the VS designer is still dumb
            this.progressBarPanel.Controls.Clear();
            this.progressBarPanel.Controls.Add(this.progressBarManager.progressBar);

            // TODO: this is beyond dumb. need to find a better way.
            FormController.BuildAssetList = Studio.BuildAssetList;
            FormController.BuildTreeStructure = Studio.BuildTreeStructure;
            FormController.BuildClassStructures = Studio.BuildClassStructures;

            FormController.ProgressBarIncrementMaximum = this.progressBarManager.IncrementMaximum;
            FormController.ProgressBarPerformStep = this.progressBarManager.PerformStep;
            FormController.ProgressBarReset = this.progressBarManager.Reset;
            FormController.ProgressBarSetValue = this.progressBarManager.SetValue;

            Studio.ProgressBarIncrementMaximum = this.progressBarManager.IncrementMaximum;
            Studio.ProgressBarPerformStep = this.progressBarManager.PerformStep;
            Studio.ProgressBarReset = this.progressBarManager.Reset;
            Studio.ProgressBarSetValue = this.progressBarManager.SetValue;

            FormController.StatusStripUpdate = this.StatusStripUpdate;
            Studio.StatusStripUpdate = this.StatusStripUpdate;
        }

        private void loadFile_Click(object sender, EventArgs e)
        {
            if (this.openFileDialog1.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            this.ResetForm();

            ThreadPool.QueueUserWorkItem(state =>
            {
                Studio.mainPath = Path.GetDirectoryName(this.openFileDialog1.FileNames[0]);
                Importer.MergeSplitAssets(Studio.mainPath);

                string[] readFile = Studio.ProcessingSplitFiles(this.openFileDialog1.FileNames.ToList());

                foreach (string i in readFile)
                {
                    Importer.importFiles.Add(i);
                    Importer.importFilesHash.Add(Path.GetFileName(i)?.ToUpper());
                }

                this.progressBarManager.Reset(Importer.importFiles.Count);

                //use a for loop because list size can change
                for (var f = 0; f < Importer.importFiles.Count; f++)
                {
                    Importer.LoadFile(Importer.importFiles[f]);
                    this.progressBarManager.PerformStep();
                }

                Importer.importFilesHash.Clear();
                Importer.assetsfileListHash.Clear();

                bool[] fileDataParams =
                {
                    this.dontLoadAssetsMenuItem.Checked,
                    this.displayAll.Checked,
                    this.dontBuildHierarchyMenuItem.Checked,
                    this.buildClassStructuresMenuItem.Checked,
                    this.displayOriginalName.Checked
                };

                this.InvokeIfRequired(() => FormController.LoadFileData(fileDataParams));
                this.AsyncInvokeIfRequired(() => FormController.SetUpAssetControls(this, this.assetListView, this.classesListView, this.sceneTreeView, fileDataParams));
                this.AsyncInvokeIfRequired(() => FormController.SetUpDropDownMenu(this.filterTypeToolStripMenuItem.DropDownItems, this.typeToolStripMenuItem_Click));

                this.StatusStripUpdate(string.Format("Finished loading {0} files with {1} exportable assets.", Studio.assetsFileList.Count, this.assetListView.Items.Count));
            });
        }

        private void loadFolder_Click(object sender, EventArgs e)
        {
            var openFolderDialog1 = new OpenFolderDialog();
            if (openFolderDialog1.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            this.ResetForm();

            ThreadPool.QueueUserWorkItem(state =>
            {
                Studio.mainPath = openFolderDialog1.Folder;

                Importer.MergeSplitAssets(Studio.mainPath);

                List<string> files = Directory.GetFiles(Studio.mainPath, "*.*", SearchOption.AllDirectories).ToList();

                string[] readFile = Studio.ProcessingSplitFiles(files);

                foreach (string i in readFile)
                {
                    Importer.importFiles.Add(i);
                    Importer.importFilesHash.Add(Path.GetFileName(i));
                }

                this.progressBarManager.Reset(Importer.importFiles.Count);

                //use a for loop because list size can change
                for (var f = 0; f < Importer.importFiles.Count; f++)
                {
                    Importer.LoadFile(Importer.importFiles[f]);
                    this.progressBarManager.PerformStep();
                }

                Importer.importFilesHash.Clear();
                Importer.assetsfileListHash.Clear();

                bool[] fileDataParams =
                {
                    this.dontLoadAssetsMenuItem.Checked,
                    this.displayAll.Checked,
                    this.dontBuildHierarchyMenuItem.Checked,
                    this.buildClassStructuresMenuItem.Checked,
                    this.displayOriginalName.Checked
                };

                FormController.LoadFileData(fileDataParams);
            });
        }

        private void extractFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var openBundleDialog = new OpenFileDialog
            {
                Filter = "All types|*.*",
                FilterIndex = 1,
                RestoreDirectory = true,
                Multiselect = true
            };

            if (openBundleDialog.ShowDialog() == DialogResult.OK)
            {
                this.progressBarManager.Reset(openBundleDialog.FileNames.Length);
                Studio.ExtractFile(openBundleDialog.FileNames);
            }

            openBundleDialog.Dispose();
        }

        private void extractFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var openFolderDialog = new OpenFolderDialog();
            if (openFolderDialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            string[] files = Directory.GetFiles(openFolderDialog.Folder, "*.*", SearchOption.AllDirectories);
            this.progressBarManager.Reset(files.Length);
            Studio.ExtractFile(files);
        }

        private void typeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var typeItem = (ToolStripMenuItem) sender;

            if (typeItem != this.allToolStripMenuItem)
            {
                this.allToolStripMenuItem.Checked = false;
            }
            else if (this.allToolStripMenuItem.Checked)
            {
                foreach (ToolStripMenuItem dropDownItem in this.filterTypeToolStripMenuItem.DropDownItems)
                {
                    if (dropDownItem != this.allToolStripMenuItem)
                    {
                        dropDownItem.Checked = false;
                    }
                }
            }
            else
            {
                // allow resetting check states by selecting the All menu item
                if (typeItem == this.allToolStripMenuItem)
                {
                    this.allToolStripMenuItem.Checked = !this.allToolStripMenuItem.Checked;
                }
            }

            // reset All menu item check state when no items are checked
            bool anyItemsChecked = this.filterTypeToolStripMenuItem.DropDownItems.Cast<ToolStripMenuItem>().Any(x => x.Checked);

            if (!anyItemsChecked)
            {
                this.allToolStripMenuItem.Checked = true;
            }

            this.FilterAssetList();
        }

        private void AssetStudioForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.Alt && e.KeyCode == Keys.D)
            {
                this.debugMenuItem.Visible = !this.debugMenuItem.Visible;
                this.buildClassStructuresMenuItem.Checked = this.debugMenuItem.Visible;
                this.dontLoadAssetsMenuItem.Checked = this.debugMenuItem.Visible;
                this.dontBuildHierarchyMenuItem.Checked = this.debugMenuItem.Visible;

                if (this.tabControl1.TabPages.Contains(this.tabPage3))
                {
                    this.tabControl1.TabPages.Remove(this.tabPage3);
                }
                else
                {
                    this.tabControl1.TabPages.Add(this.tabPage3);
                }
            }

            if (!this.glControl1.Visible)
            {
                return;
            }

            if (!e.Control)
            {
                return;
            }

            switch (e.KeyCode)
            {
                case Keys.W:
                    if (e.Control) //Toggle WireFrame
                    {
                        this.wireFrameMode = (this.wireFrameMode + 1) % 3;
                        this.glControl1.Invalidate();
                    }
                    break;
                case Keys.S:
                    if (e.Control) //Toggle Shade
                    {
                        this.shadeMode = (this.shadeMode + 1) % 2;
                        this.glControl1.Invalidate();
                    }
                    break;
                case Keys.N:
                    if (e.Control) //Normal mode
                    {
                        this.normalMode = (this.normalMode + 1) % 2;
                        this.createVAO();
                        this.glControl1.Invalidate();
                    }
                    break;
            }
        }

        private void dontLoadAssetsMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            if (this.dontLoadAssetsMenuItem.Checked)
            {
                this.dontBuildHierarchyMenuItem.Checked = true;
                this.dontBuildHierarchyMenuItem.Enabled = false;
            }
            else
            {
                this.dontBuildHierarchyMenuItem.Enabled = true;
            }
        }

        private void exportClassStructuresMenuItem_Click(object sender, EventArgs e)
        {
            if (Studio.AllTypeMap.Count <= 0)
            {
                return;
            }

            var saveFolderDialog1 = new OpenFolderDialog();

            if (saveFolderDialog1.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            this.progressBarManager.Reset(Studio.AllTypeMap.Count);

            string savePath = saveFolderDialog1.Folder;

            foreach (KeyValuePair<string, SortedDictionary<int, TypeTreeItem>> version in Studio.AllTypeMap)
            {
                if (version.Value.Count > 0)
                {
                    string versionPath = savePath + "\\" + version.Key;

                    Directory.CreateDirectory(versionPath);

                    foreach (KeyValuePair<int, TypeTreeItem> uclass in version.Value)
                    {
                        string saveFile = string.Format("{0}\\{1} {2}.txt", versionPath, uclass.Key, uclass.Value.Text);
                        using (var TXTwriter = new StreamWriter(saveFile))
                            TXTwriter.Write(uclass.Value.ToString());
                    }
                }

                this.progressBarManager.PerformStep();
            }

            this.StatusStripUpdate("Finished exporting class structures");

            this.progressBarManager.SetValue(0);
        }

        private void enablePreview_Check(object sender, EventArgs e)
        {
            if (this.lastLoadedAsset != null)
            {
                switch (this.lastLoadedAsset.Type)
                {
                    case ClassIDType.Texture2D:
                    case ClassIDType.Sprite:
                        if (this.enablePreview.Checked && this.imageTexture != null)
                        {
                            this.previewPanel.BackgroundImage = this.imageTexture;
                        }
                        else
                        {
                            this.previewPanel.BackgroundImage = Resources.preview;
                            this.previewPanel.BackgroundImageLayout = ImageLayout.Center;
                        }
                        break;
                    case ClassIDType.Shader:
                    case ClassIDType.TextAsset:
                    case ClassIDType.MonoBehaviour:
                        this.textPreviewBox.Visible = !this.textPreviewBox.Visible;
                        break;
                    case ClassIDType.Font:
                        this.fontPreviewBox.Visible = !this.fontPreviewBox.Visible;
                        break;
                    case ClassIDType.AudioClip:
                        this.FMODpanel.Visible = !this.FMODpanel.Visible;

                        if (this.sound != null && this.channel != null)
                        {
                            RESULT result = this.channel.isPlaying(out bool playing);
                            if (result == RESULT.OK && playing)
                            {
                                result = this.channel.stop();
                                this.FMODreset();
                            }
                        }
                        else if (this.FMODpanel.Visible)
                        {
                            this.PreviewAsset(this.lastLoadedAsset);
                        }
                        break;
                }
            }
            else if (this.lastSelectedItem != null && this.enablePreview.Checked)
            {
                this.lastLoadedAsset = this.lastSelectedItem;
                this.PreviewAsset(this.lastLoadedAsset);
            }

            Settings.Default["enablePreview"] = this.enablePreview.Checked;
            Settings.Default.Save();
        }

        private void displayAssetInfo_Check(object sender, EventArgs e)
        {
            if (this.displayInfo.Checked && this.assetInfoLabel.Text != null)
            {
                this.assetInfoLabel.Visible = true;
            }
            else
            {
                this.assetInfoLabel.Visible = false;
            }

            Settings.Default["displayInfo"] = this.displayInfo.Checked;
            Settings.Default.Save();
        }

        private void MenuItem_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default[((ToolStripMenuItem) sender).Name] = ((ToolStripMenuItem) sender).Checked;
            Settings.Default.Save();
        }

        private void assetGroupOptions_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.Default["assetGroupOption"] = ((ToolStripComboBox) sender).SelectedIndex;
            Settings.Default.Save();
        }

        private void showExpOpt_Click(object sender, EventArgs e)
        {
            using (var exportOpt = new ExportOptions())
                exportOpt.ShowDialog();
        }

        private void assetListView_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            e.Item = Studio.visibleAssets[e.ItemIndex];
        }

        private void tabPageSelected(object sender, TabControlEventArgs e)
        {
            switch (e.TabPageIndex)
            {
                case 0:
                    this.treeSearch.Select();
                    break;
                case 1:
                    this.ResizeAssetListColumns(); //required because the ListView is not visible on app launch
                    this.classPreviewPanel.Visible = false;
                    this.previewPanel.Visible = true;
                    this.listSearch.Select();
                    break;
                case 2:
                    this.previewPanel.Visible = false;
                    this.classPreviewPanel.Visible = true;
                    break;
            }
        }

        private void treeSearch_Enter(object sender, EventArgs e)
        {
            if (this.treeSearch.Text != " Search ")
            {
                return;
            }

            this.treeSearch.Text = "";
            this.treeSearch.ForeColor = SystemColors.WindowText;
        }

        private void treeSearch_Leave(object sender, EventArgs e)
        {
            if (this.treeSearch.Text != "")
            {
                return;
            }

            this.treeSearch.Text = " Search ";
            this.treeSearch.ForeColor = SystemColors.GrayText;
        }

        private void treeSearch_TextChanged(object sender, EventArgs e)
        {
            this.treeSrcResults.Clear();
            this.nextGObject = 0;
        }

        private void treeSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            if (this.treeSrcResults.Count == 0)
            {
                foreach (GameObjectTreeNode node in Studio.treeNodeDictionary.Values)
                {
                    if (node.Text.IndexOf(this.treeSearch.Text, StringComparison.CurrentCultureIgnoreCase) >= 0)
                    {
                        this.treeSrcResults.Add(node);
                    }
                }
            }

            if (this.treeSrcResults.Count > 0)
            {
                if (this.nextGObject >= this.treeSrcResults.Count)
                {
                    this.nextGObject = 0;
                }

                this.treeSrcResults[this.nextGObject].EnsureVisible();
                this.sceneTreeView.SelectedNode = this.treeSrcResults[this.nextGObject];
                this.nextGObject++;
            }
        }

        private void sceneTreeView_AfterCheck(object sender, TreeViewEventArgs e)
        {
            foreach (TreeNode childNode in e.Node.Nodes)
            {
                childNode.Checked = e.Node.Checked;
            }
        }

        private void ResizeAssetListColumns()
        {
            // TODO: defer to user preferences for column widths
            this.assetListView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
        }

        private void tabPage2_Resize(object sender, EventArgs e)
        {
            this.ResizeAssetListColumns();
        }

        private void listSearch_Enter(object sender, EventArgs e)
        {
            if (this.listSearch.Text != " Filter ")
            {
                return;
            }

            this.listSearch.Text = "";
            this.listSearch.ForeColor = SystemColors.WindowText;
            this.enableFiltering = true;
        }

        private void listSearch_Leave(object sender, EventArgs e)
        {
            if (this.listSearch.Text != "")
            {
                return;
            }

            this.enableFiltering = false;
            this.listSearch.Text = " Filter ";
            this.listSearch.ForeColor = SystemColors.GrayText;
        }

        private void ListSearchTextChanged(object sender, EventArgs e)
        {
            if (this.enableFiltering && this.enableLiveSearch.Checked)
            {
                this.FilterAssetList();
            }
        }

        [SuppressMessage("ReSharper", "StringCompareToIsCultureSpecific")]
        private void assetListView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (this.firstSortColumn != e.Column)
            {
                //sorting column has been changed
                this.reverseSort = false;
                this.secondSortColumn = this.firstSortColumn;
            }
            else
            {
                this.reverseSort = !this.reverseSort;
            }

            this.firstSortColumn = e.Column;

            this.assetListView.BeginUpdate();
            this.assetListView.SelectedIndices.Clear();

            switch (e.Column)
            {
                case 0:
                    Studio.visibleAssets.Sort(delegate(AssetItem a, AssetItem b)
                    {
                        int xdiff = this.reverseSort ? b.Text.CompareTo(a.Text) : a.Text.CompareTo(b.Text);
                        if (xdiff != 0)
                        {
                            return xdiff;
                        }
                        return this.secondSortColumn == 1 ? a.TypeString.CompareTo(b.TypeString) : a.FullSize.CompareTo(b.FullSize);
                    });
                    break;
                case 1:
                    Studio.visibleAssets.Sort(delegate(AssetItem a, AssetItem b)
                    {
                        int xdiff = this.reverseSort ? b.TypeString.CompareTo(a.TypeString) : a.TypeString.CompareTo(b.TypeString);
                        if (xdiff != 0)
                        {
                            return xdiff;
                        }
                        return this.secondSortColumn == 2 ? a.FullSize.CompareTo(b.FullSize) : a.Text.CompareTo(b.Text);
                    });
                    break;
                case 2:
                    Studio.visibleAssets.Sort(delegate(AssetItem a, AssetItem b)
                    {
                        int xdiff = this.reverseSort ? b.FullSize.CompareTo(a.FullSize) : a.FullSize.CompareTo(b.FullSize);
                        if (xdiff != 0)
                        {
                            return xdiff;
                        }
                        return this.secondSortColumn == 1 ? a.TypeString.CompareTo(b.TypeString) : a.Text.CompareTo(b.Text);
                    });
                    break;
            }

            this.assetListView.EndUpdate();

            this.ResizeAssetListColumns();
        }

        private static void UnloadModules()
        {
            foreach (string key in ScriptHelper.LoadedModuleDic.Keys)
            {
                ModuleDef module = ScriptHelper.LoadedModuleDic[key];
                module.Dispose();
            }

            ScriptHelper.LoadedModuleDic.Clear();

            ScriptHelper.moduleContext = null;
        }

        private void selectAsset(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            UnloadModules();

            this.previewPanel.BackgroundImage = Resources.preview;
            this.previewPanel.BackgroundImageLayout = ImageLayout.Center;
            this.assetInfoLabel.Visible = false;
            this.assetInfoLabel.Text = null;
            this.textPreviewBox.Visible = false;
            this.monoPreviewBox.Visible = false;
            this.fontPreviewBox.Visible = false;
            this.FMODpanel.Visible = false;
            this.glControl1.Visible = false;
            this.lastLoadedAsset = null;
            this.StatusStripUpdate("");

            this.FMODreset();

            this.lastSelectedItem = (AssetItem) e.Item;

            if (!e.IsSelected)
            {
                return;
            }

            if (this.enablePreview.Checked)
            {
                this.lastLoadedAsset = this.lastSelectedItem;
                this.PreviewAsset(this.lastLoadedAsset);
            }

            if (this.displayInfo.Checked && this.assetInfoLabel.Text != null) //only display the label if asset has info text
            {
                this.assetInfoLabel.Text = this.lastSelectedItem.InfoText;
                this.assetInfoLabel.Visible = true;
            }
        }

        private void classesListView_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (e.IsSelected)
            {
                this.classTextBox.Text = ((TypeTreeItem) this.classesListView.SelectedItems[0]).ToString();
            }
        }

        private void PreviewAsset(AssetItem asset)
        {
            ObjectReader reader = asset.reader;

            switch (asset.Type)
            {
                case ClassIDType.Texture2D:
                {
                    this.PreviewAsset_Texture2D(asset);
                    break;
                }
                case ClassIDType.AudioClip:
                {
                    this.PreviewAsset_AudioClip(asset);
                    break;
                }
                case ClassIDType.Shader:
                {
                    this.PreviewAsset_Shader(asset);
                    break;
                }
                case ClassIDType.TextAsset:
                {
                    this.PreviewAsset_TextAsset(asset);
                    break;
                }
                case ClassIDType.MonoScript:
                    ScriptHelper.AddNodes(this.monoPreviewBox, reader, false);
                    break;
                case ClassIDType.MonoBehaviour:
                    ScriptHelper.AddNodes(this.monoPreviewBox, reader, true);
                    break;
                case ClassIDType.Font:
                {
                    this.PreviewAsset_Font(reader);
                    break;
                }
                case ClassIDType.Mesh:
                    if (this.PreviewAsset_Mesh(reader))
                    {
                        return;
                    }
                    break;
                case ClassIDType.VideoClip:
                case ClassIDType.MovieTexture:
                    this.StatusStripUpdate("Only supported export.");
                    break;
                case ClassIDType.Sprite:
                    this.PreviewAsset_Sprite(asset);
                    break;
                case ClassIDType.Animator:
                    this.StatusStripUpdate("Can be exported to FBX file.");
                    break;
                case ClassIDType.AnimationClip:
                    this.StatusStripUpdate("Can be exported with Animator or objects");
                    break;
                default:
                    this.PreviewAsset_Default(reader);
                    break;
            }

            switch (asset.Type)
            {
                case ClassIDType.MonoBehaviour:
                case ClassIDType.MonoScript:
                    this.monoPreviewBox.Visible = true;
                    this.textPreviewBox.Visible = false;
                    break;
                default:
                    this.monoPreviewBox.Nodes.Clear();
                    this.monoPreviewBox.Visible = false;
                    break;
            }
        }

        private void PreviewAsset_Default(ObjectReader asset)
        {
            string str = asset.Dump();

            if (str != null)
            {
                this.textPreviewBox.Text = str;
                this.textPreviewBox.Visible = true;
            }
            else
            {
                this.StatusStripUpdate("Only supported export the raw file.");
            }
        }

        private void PreviewAsset_AudioClip(AssetItem asset)
        {
            var m_AudioClip = new AudioClip(asset.reader, true);

            //Info
            asset.InfoText = "Compression format: ";
            if (m_AudioClip.version[0] < 5)
            {
                switch (m_AudioClip.m_Type)
                {
                    case AudioType.ACC:
                        asset.InfoText += "Acc";
                        break;
                    case AudioType.AIFF:
                        asset.InfoText += "AIFF";
                        break;
                    case AudioType.IT:
                        asset.InfoText += "Impulse tracker";
                        break;
                    case AudioType.MOD:
                        asset.InfoText += "Protracker / Fasttracker MOD";
                        break;
                    case AudioType.MPEG:
                        asset.InfoText += "MP2/MP3 MPEG";
                        break;
                    case AudioType.OGGVORBIS:
                        asset.InfoText += "Ogg vorbis";
                        break;
                    case AudioType.S3M:
                        asset.InfoText += "ScreamTracker 3";
                        break;
                    case AudioType.WAV:
                        asset.InfoText += "Microsoft WAV";
                        break;
                    case AudioType.XM:
                        asset.InfoText += "FastTracker 2 XM";
                        break;
                    case AudioType.XMA:
                        asset.InfoText += "Xbox360 XMA";
                        break;
                    case AudioType.VAG:
                        asset.InfoText += "PlayStation Portable ADPCM";
                        break;
                    case AudioType.AUDIOQUEUE:
                        asset.InfoText += "iPhone";
                        break;
                    default:
                        asset.InfoText += "Unknown";
                        break;
                }
            }
            else
            {
                switch (m_AudioClip.m_CompressionFormat)
                {
                    case AudioCompressionFormat.PCM:
                        asset.InfoText += "PCM";
                        break;
                    case AudioCompressionFormat.Vorbis:
                        asset.InfoText += "Vorbis";
                        break;
                    case AudioCompressionFormat.ADPCM:
                        asset.InfoText += "ADPCM";
                        break;
                    case AudioCompressionFormat.MP3:
                        asset.InfoText += "MP3";
                        break;
                    case AudioCompressionFormat.VAG:
                        asset.InfoText += "PlayStation Portable ADPCM";
                        break;
                    case AudioCompressionFormat.HEVAG:
                        asset.InfoText += "PSVita ADPCM";
                        break;
                    case AudioCompressionFormat.XMA:
                        asset.InfoText += "Xbox360 XMA";
                        break;
                    case AudioCompressionFormat.AAC:
                        asset.InfoText += "AAC";
                        break;
                    case AudioCompressionFormat.GCADPCM:
                        asset.InfoText += "Nintendo 3DS/Wii DSP";
                        break;
                    case AudioCompressionFormat.ATRAC9:
                        asset.InfoText += "PSVita ATRAC9";
                        break;
                    default:
                        asset.InfoText += "Unknown";
                        break;
                }
            }

            if (m_AudioClip.m_AudioData == null)
            {
                return;
            }

            var exinfo = new CREATESOUNDEXINFO();

            exinfo.cbsize = Marshal.SizeOf(exinfo);
            exinfo.length = (uint) m_AudioClip.m_Size;

            RESULT result = this.system.createSound(m_AudioClip.m_AudioData, MODE.OPENMEMORY | this.loopMode, ref exinfo, out this.sound);
            if (this.ERRCHECK(result))
            {
                return;
            }

            result = this.sound.getSubSound(0, out Sound subsound);
            if (result == RESULT.OK)
            {
                this.sound = subsound;
            }

            result = this.sound.getLength(out this.FMODlenms, TIMEUNIT.MS);
            if (this.ERRCHECK(result))
            {
                return;
            }

            result = this.system.playSound(this.sound, null, true, out this.channel);
            if (this.ERRCHECK(result))
            {
                return;
            }

            this.FMODpanel.Visible = true;

            result = this.channel.getFrequency(out float frequency);
            if (this.ERRCHECK(result))
            {
                return;
            }

            this.FMODinfoLabel.Text = frequency + " Hz";
            this.FMODtimerLabel.Text = $"0:0.0 / {this.FMODlenms / 1000 / 60}:{this.FMODlenms / 1000 % 60}.{this.FMODlenms / 10 % 100}";
        }

        private void PreviewAsset_Font(ObjectReader asset)
        {
            var m_Font = new Font(asset);

            if (m_Font.m_FontData != null)
            {
                IntPtr data = Marshal.AllocCoTaskMem(m_Font.m_FontData.Length);
                Marshal.Copy(m_Font.m_FontData, 0, data, m_Font.m_FontData.Length);

                // We HAVE to do this to register the font to the system (Weird .NET bug !)
                uint cFonts = 0;

                IntPtr re = AddFontMemResourceEx(data, (uint) m_Font.m_FontData.Length, IntPtr.Zero, ref cFonts);

                if (re != IntPtr.Zero)
                {
                    using (var pfc = new PrivateFontCollection())
                    {
                        pfc.AddMemoryFont(data, m_Font.m_FontData.Length);
                        Marshal.FreeCoTaskMem(data);

                        if (pfc.Families.Length <= 0)
                        {
                            return;
                        }

                        this.fontPreviewBox.SelectionStart = 0;
                        this.fontPreviewBox.SelectionLength = 80;
                        this.fontPreviewBox.SelectionFont = new System.Drawing.Font(pfc.Families[0], 16, FontStyle.Regular);
                        this.fontPreviewBox.SelectionStart = 81;
                        this.fontPreviewBox.SelectionLength = 56;
                        this.fontPreviewBox.SelectionFont = new System.Drawing.Font(pfc.Families[0], 12, FontStyle.Regular);
                        this.fontPreviewBox.SelectionStart = 138;
                        this.fontPreviewBox.SelectionLength = 56;
                        this.fontPreviewBox.SelectionFont = new System.Drawing.Font(pfc.Families[0], 18, FontStyle.Regular);
                        this.fontPreviewBox.SelectionStart = 195;
                        this.fontPreviewBox.SelectionLength = 56;
                        this.fontPreviewBox.SelectionFont = new System.Drawing.Font(pfc.Families[0], 24, FontStyle.Regular);
                        this.fontPreviewBox.SelectionStart = 252;
                        this.fontPreviewBox.SelectionLength = 56;
                        this.fontPreviewBox.SelectionFont = new System.Drawing.Font(pfc.Families[0], 36, FontStyle.Regular);
                        this.fontPreviewBox.SelectionStart = 309;
                        this.fontPreviewBox.SelectionLength = 56;
                        this.fontPreviewBox.SelectionFont = new System.Drawing.Font(pfc.Families[0], 48, FontStyle.Regular);
                        this.fontPreviewBox.SelectionStart = 366;
                        this.fontPreviewBox.SelectionLength = 56;
                        this.fontPreviewBox.SelectionFont = new System.Drawing.Font(pfc.Families[0], 60, FontStyle.Regular);
                        this.fontPreviewBox.SelectionStart = 423;
                        this.fontPreviewBox.SelectionLength = 55;
                        this.fontPreviewBox.SelectionFont = new System.Drawing.Font(pfc.Families[0], 72, FontStyle.Regular);
                        this.fontPreviewBox.Visible = true;
                    }

                    return;
                }
            }

            this.StatusStripUpdate("Unsupported font for preview. Try to export.");
        }

        private bool PreviewAsset_Mesh(ObjectReader asset)
        {
            var m_Mesh = new Mesh(asset);

            if (m_Mesh.m_VertexCount > 0)
            {
                this.viewMatrixData = Matrix4.CreateRotationY(-(float) Math.PI / 4) * Matrix4.CreateRotationX(-(float) Math.PI / 6);

                if (this.CalculateMeshVertices(m_Mesh, out int count))
                {
                    return true;
                }

                float[] min = this.CalculateMeshBoundingMin(m_Mesh, count, out float[] max);

                this.CalculateMeshMatrix(max, min);

                this.CalculateMeshIndices(m_Mesh);

                this.CalculateMeshNormals(m_Mesh, count);

                this.CalculateMeshColors(m_Mesh);

                this.glControl1.Visible = true;
                this.createVAO();
            }

            this.StatusStripUpdate("Using OpenGL Version: " + GL.GetString(StringName.Version) + "\n" + "'Mouse Left'=Rotate | 'Mouse Right'=Move | 'Mouse Wheel'=Zoom \n" + "'Ctrl W'=Wireframe | 'Ctrl S'=Shade | 'Ctrl N'=ReNormal ");
            return false;
        }

        private bool CalculateMeshVertices(Mesh m_Mesh, out int count)
        {
            if (m_Mesh.m_Vertices == null || m_Mesh.m_Vertices.Length == 0)
            {
                this.StatusStripUpdate("Mesh can't be previewed.");
                count = 0;
                return true;
            }

            count = 3;
            if (m_Mesh.m_Vertices.Length == m_Mesh.m_VertexCount * 4)
            {
                count = 4;
            }

            this.vertexData = new Vector3[m_Mesh.m_VertexCount];
            return false;
        }

        private float[] CalculateMeshBoundingMin(Mesh m_Mesh, int count, out float[] max)
        {
            var min = new float[3];
            max = new float[3];

            for (var i = 0; i < 3; i++)
            {
                min[i] = m_Mesh.m_Vertices[i];
                max[i] = m_Mesh.m_Vertices[i];
            }

            for (var v = 0; v < m_Mesh.m_VertexCount; v++)
            {
                for (var i = 0; i < 3; i++)
                {
                    min[i] = Math.Min(min[i], m_Mesh.m_Vertices[v * count + i]);
                    max[i] = Math.Max(max[i], m_Mesh.m_Vertices[v * count + i]);
                }

                this.vertexData[v] = new Vector3(m_Mesh.m_Vertices[v * count], m_Mesh.m_Vertices[v * count + 1], m_Mesh.m_Vertices[v * count + 2]);
            }

            return min;
        }

        private void CalculateMeshMatrix(float[] max, float[] min)
        {
            Vector3 dist = Vector3.One, offset = Vector3.Zero;

            for (var i = 0; i < 3; i++)
            {
                dist[i] = max[i] - min[i];
                offset[i] = (max[i] + min[i]) / 2;
            }

            float d = Math.Max(1e-5f, dist.Length);
            this.modelMatrixData = Matrix4.CreateTranslation(-offset) * Matrix4.CreateScale(2f / d);
        }

        private void CalculateMeshIndices(Mesh m_Mesh)
        {
            this.indiceData = new int[m_Mesh.m_Indices.Count];

            for (var i = 0; i < m_Mesh.m_Indices.Count; i = i + 3)
            {
                this.indiceData[i] = (int) m_Mesh.m_Indices[i];
                this.indiceData[i + 1] = (int) m_Mesh.m_Indices[i + 1];
                this.indiceData[i + 2] = (int) m_Mesh.m_Indices[i + 2];
            }
        }

        private void CalculateMeshNormals(Mesh m_Mesh, int count)
        {
            if (m_Mesh.m_Normals != null && m_Mesh.m_Normals.Length > 0)
            {
                if (m_Mesh.m_Normals.Length == m_Mesh.m_VertexCount * 3)
                {
                    count = 3;
                }
                else if (m_Mesh.m_Normals.Length == m_Mesh.m_VertexCount * 4)
                {
                    count = 4;
                }

                this.normalData = new Vector3[m_Mesh.m_VertexCount];

                for (var n = 0; n < m_Mesh.m_VertexCount; n++)
                {
                    this.normalData[n] = new Vector3(m_Mesh.m_Normals[n * count], m_Mesh.m_Normals[n * count + 1], m_Mesh.m_Normals[n * count + 2]);
                }
            }
            else
            {
                this.normalData = null;
            }

            // calculate normal by ourself
            this.normal2Data = new Vector3[m_Mesh.m_VertexCount];
            var normalCalculatedCount = new int[m_Mesh.m_VertexCount];

            for (var i = 0; i < m_Mesh.m_VertexCount; i++)
            {
                this.normal2Data[i] = Vector3.Zero;
                normalCalculatedCount[i] = 0;
            }

            for (var i = 0; i < m_Mesh.m_Indices.Count; i = i + 3)
            {
                Vector3 dir1 = this.vertexData[this.indiceData[i + 1]] - this.vertexData[this.indiceData[i]];
                Vector3 dir2 = this.vertexData[this.indiceData[i + 2]] - this.vertexData[this.indiceData[i]];
                Vector3 normal = Vector3.Cross(dir1, dir2);
                normal.Normalize();

                for (var j = 0; j < 3; j++)
                {
                    this.normal2Data[this.indiceData[i + j]] += normal;
                    normalCalculatedCount[this.indiceData[i + j]]++;
                }
            }

            for (var i = 0; i < m_Mesh.m_VertexCount; i++)
            {
                if (normalCalculatedCount[i] == 0)
                {
                    this.normal2Data[i] = new Vector3(0, 1, 0);
                }
                else
                {
                    this.normal2Data[i] /= normalCalculatedCount[i];
                }
            }
        }

        private void CalculateMeshColors(Mesh m_Mesh)
        {
            if (m_Mesh.m_Colors != null && m_Mesh.m_Colors.Length == m_Mesh.m_VertexCount * 3)
            {
                this.colorData = new Vector4[m_Mesh.m_VertexCount];

                for (var c = 0; c < m_Mesh.m_VertexCount; c++)
                {
                    this.colorData[c] = new Vector4(m_Mesh.m_Colors[c * 3], m_Mesh.m_Colors[c * 3 + 1], m_Mesh.m_Colors[c * 3 + 2], 1.0f);
                }
            }
            else if (m_Mesh.m_Colors != null && m_Mesh.m_Colors.Length == m_Mesh.m_VertexCount * 4)
            {
                this.colorData = new Vector4[m_Mesh.m_VertexCount];

                for (var c = 0; c < m_Mesh.m_VertexCount; c++)
                {
                    this.colorData[c] = new Vector4(m_Mesh.m_Colors[c * 4], m_Mesh.m_Colors[c * 4 + 1], m_Mesh.m_Colors[c * 4 + 2], m_Mesh.m_Colors[c * 4 + 3]);
                }
            }
            else
            {
                this.colorData = new Vector4[m_Mesh.m_VertexCount];

                for (var c = 0; c < m_Mesh.m_VertexCount; c++)
                {
                    this.colorData[c] = new Vector4(0.5f, 0.5f, 0.5f, 1.0f);
                }
            }
        }

        private void PreviewAsset_Shader(AssetItem asset)
        {
            ObjectReader reader = asset.reader;
            var m_Shader = new Shader(reader);
            string str = ShaderConverter.Convert(m_Shader);
            this.textPreviewBox.Text = str?.Replace("\n", "\r\n") ?? "Serialized Shader can't be read";
            this.textPreviewBox.Visible = true;
        }

        private void PreviewAsset_Sprite(AssetItem asset)
        {
            this.imageTexture?.Dispose();
            this.imageTexture = SpriteHelper.GetImageFromSprite(new Sprite(asset.reader));

            if (this.imageTexture != null)
            {
                asset.InfoText = $"Width: {this.imageTexture.Width}\nHeight: {this.imageTexture.Height}\n";

                this.previewPanel.BackgroundImage = this.imageTexture;

                if (this.imageTexture.Width > this.previewPanel.Width || this.imageTexture.Height > this.previewPanel.Height)
                {
                    this.previewPanel.BackgroundImageLayout = ImageLayout.Zoom;
                }
                else
                {
                    this.previewPanel.BackgroundImageLayout = ImageLayout.Center;
                }
            }
            else
            {
                this.StatusStripUpdate("Unsupported sprite for preview.");
            }
        }

        private void PreviewAsset_TextAsset(AssetItem asset)
        {
            var m_TextAsset = new TextAsset(asset.reader);

            string m_Script_Text = Encoding.UTF8.GetString(m_TextAsset.m_Script);
            m_Script_Text = Regex.Replace(m_Script_Text, "(?<!\r)\n", "\r\n");

            this.textPreviewBox.Text = m_Script_Text;
            this.textPreviewBox.Visible = true;
        }

        private void PreviewAsset_Texture2D(AssetItem asset)
        {
            this.imageTexture?.Dispose();
            var m_Texture2D = new Texture2D(asset.reader, true);

            //Info
            asset.InfoText = $"Width: {m_Texture2D.m_Width}\nHeight: {m_Texture2D.m_Height}\nFormat: {m_Texture2D.m_TextureFormat}";

            switch (m_Texture2D.m_FilterMode)
            {
                case 0:
                    asset.InfoText += "\nFilter Mode: Point ";
                    break;
                case 1:
                    asset.InfoText += "\nFilter Mode: Bilinear ";
                    break;
                case 2:
                    asset.InfoText += "\nFilter Mode: Trilinear ";
                    break;
            }

            asset.InfoText += $"\nAnisotropic level: {m_Texture2D.m_Aniso}\nMip map bias: {m_Texture2D.m_MipBias}";

            switch (m_Texture2D.m_WrapMode)
            {
                case 0:
                    asset.InfoText += "\nWrap mode: Repeat";
                    break;
                case 1:
                    asset.InfoText += "\nWrap mode: Clamp";
                    break;
            }

            var converter = new Texture2DConverter(m_Texture2D);
            this.imageTexture = converter.ConvertToBitmap(true);

            if (this.imageTexture != null)
            {
                this.previewPanel.BackgroundImage = this.imageTexture;
                if (this.imageTexture.Width > this.previewPanel.Width || this.imageTexture.Height > this.previewPanel.Height)
                {
                    this.previewPanel.BackgroundImageLayout = ImageLayout.Zoom;
                }
                else
                {
                    this.previewPanel.BackgroundImageLayout = ImageLayout.Center;
                }
            }
            else
            {
                this.StatusStripUpdate("Unsupported image for preview");
            }
        }

        private void FMODinit()
        {
            this.FMODreset();

            RESULT result = Factory.System_Create(out this.system);
            if (this.ERRCHECK(result))
            {
                return;
            }

            result = this.system.getVersion(out uint version);
            this.ERRCHECK(result);
            if (version < VERSION.number)
            {
                MessageBox.Show($"Error!  You are using an old version of FMOD {version:X}.  This program requires {VERSION.number:X}.");
                Application.Exit();
            }

            result = this.system.init(1, INITFLAGS.NORMAL, IntPtr.Zero);
            if (this.ERRCHECK(result))
            {
                return;
            }

            result = this.system.getMasterSoundGroup(out this.masterSoundGroup);
            if (this.ERRCHECK(result))
            {
                return;
            }

            result = this.masterSoundGroup.setVolume(this.FMODVolume);
            if (this.ERRCHECK(result))
            {
                return;
            }
        }

        private void FMODreset()
        {
            this.timer.Stop();
            this.FMODprogressBar.Value = 0;
            this.FMODtimerLabel.Text = "0:00.0 / 0:00.0";
            this.FMODstatusLabel.Text = "Stopped";
            this.FMODinfoLabel.Text = "";

            if (this.sound == null || !this.sound.isValid())
            {
                return;
            }

            RESULT result = this.sound.release();
            this.ERRCHECK(result);
            this.sound = null;
        }

        private void FMODplayButton_Click(object sender, EventArgs e)
        {
            if (this.sound == null || this.channel == null)
            {
                return;
            }

            this.timer.Start();
            RESULT result = this.channel.isPlaying(out bool playing);
            if (result != RESULT.OK && result != RESULT.ERR_INVALID_HANDLE)
            {
                if (this.ERRCHECK(result))
                {
                    return;
                }
            }

            if (playing)
            {
                result = this.channel.stop();
                if (this.ERRCHECK(result))
                {
                    return;
                }

                result = this.system.playSound(this.sound, null, false, out this.channel);
                if (this.ERRCHECK(result))
                {
                    return;
                }

                this.FMODpauseButton.Text = "Pause";
            }
            else
            {
                result = this.system.playSound(this.sound, null, false, out this.channel);
                if (this.ERRCHECK(result))
                {
                    return;
                }
                this.FMODstatusLabel.Text = "Playing";

                if (this.FMODprogressBar.Value <= 0)
                {
                    return;
                }

                uint newms = this.FMODlenms / 1000 * (uint) this.FMODprogressBar.Value;

                result = this.channel.setPosition(newms, TIMEUNIT.MS);
                if (result == RESULT.OK || result == RESULT.ERR_INVALID_HANDLE)
                {
                    return;
                }

                if (this.ERRCHECK(result))
                {
                    return;
                }
            }
        }

        private void FMODpauseButton_Click(object sender, EventArgs e)
        {
            if (this.sound == null || this.channel == null)
            {
                return;
            }

            RESULT result = this.channel.isPlaying(out bool playing);
            if (result != RESULT.OK && result != RESULT.ERR_INVALID_HANDLE)
            {
                if (this.ERRCHECK(result))
                {
                    return;
                }
            }

            if (!playing)
            {
                return;
            }

            result = this.channel.getPaused(out bool paused);
            if (this.ERRCHECK(result))
            {
                return;
            }

            result = this.channel.setPaused(!paused);
            if (this.ERRCHECK(result))
            {
                return;
            }

            if (paused)
            {
                this.FMODstatusLabel.Text = "Playing";
                this.FMODpauseButton.Text = "Pause";
                this.timer.Start();
            }
            else
            {
                this.FMODstatusLabel.Text = "Paused";
                this.FMODpauseButton.Text = "Resume";
                this.timer.Stop();
            }
        }

        private void FMODstopButton_Click(object sender, EventArgs e)
        {
            if (this.channel == null)
            {
                return;
            }

            RESULT result = this.channel.isPlaying(out bool playing);
            if (result != RESULT.OK && result != RESULT.ERR_INVALID_HANDLE)
            {
                if (this.ERRCHECK(result))
                {
                    return;
                }
            }

            if (!playing)
            {
                return;
            }

            result = this.channel.stop();
            if (this.ERRCHECK(result))
            {
                return;
            }

            //channel = null;
            //don't FMODreset, it will nullify the sound
            this.timer.Stop();
            this.FMODprogressBar.Value = 0;
            this.FMODtimerLabel.Text = "0:00.0 / 0:00.0";
            this.FMODstatusLabel.Text = "Stopped";
            this.FMODpauseButton.Text = "Pause";
        }

        private void FMODloopButton_CheckedChanged(object sender, EventArgs e)
        {
            RESULT result;

            this.loopMode = this.FMODloopButton.Checked ? MODE.LOOP_NORMAL : MODE.LOOP_OFF;

            if (this.sound != null)
            {
                result = this.sound.setMode(this.loopMode);
                if (this.ERRCHECK(result))
                {
                    return;
                }
            }

            if (this.channel == null)
            {
                return;
            }

            result = this.channel.isPlaying(out bool playing);
            if (result != RESULT.OK && result != RESULT.ERR_INVALID_HANDLE)
            {
                if (this.ERRCHECK(result))
                {
                    return;
                }
            }

            result = this.channel.getPaused(out bool paused);
            if (result != RESULT.OK && result != RESULT.ERR_INVALID_HANDLE)
            {
                if (this.ERRCHECK(result))
                {
                    return;
                }
            }

            if (!playing && !paused)
            {
                return;
            }

            result = this.channel.setMode(this.loopMode);
            if (this.ERRCHECK(result))
            {
                return;
            }
        }

        private void FMODvolumeBar_ValueChanged(object sender, EventArgs e)
        {
            this.FMODVolume = Convert.ToSingle(this.FMODvolumeBar.Value) / 10;

            RESULT result = this.masterSoundGroup.setVolume(this.FMODVolume);
            if (this.ERRCHECK(result))
            {
                return;
            }
        }

        private void FMODprogressBar_Scroll(object sender, EventArgs e)
        {
            if (this.channel == null)
            {
                return;
            }

            uint newms = this.FMODlenms / 1000 * (uint) this.FMODprogressBar.Value;
            this.FMODtimerLabel.Text = $"{newms / 1000 / 60}:{newms / 1000 % 60}.{newms / 10 % 100}/{this.FMODlenms / 1000 / 60}:{this.FMODlenms / 1000 % 60}.{this.FMODlenms / 10 % 100}";
        }

        private void FMODprogressBar_MouseDown(object sender, MouseEventArgs e)
        {
            this.timer.Stop();
        }

        private void FMODprogressBar_MouseUp(object sender, MouseEventArgs e)
        {
            if (this.channel == null)
            {
                return;
            }

            uint newms = this.FMODlenms / 1000 * (uint) this.FMODprogressBar.Value;

            RESULT result = this.channel.setPosition(newms, TIMEUNIT.MS);
            if (result != RESULT.OK && result != RESULT.ERR_INVALID_HANDLE)
            {
                if (this.ERRCHECK(result))
                {
                    return;
                }
            }

            result = this.channel.isPlaying(out bool playing);
            if (result != RESULT.OK && result != RESULT.ERR_INVALID_HANDLE)
            {
                if (this.ERRCHECK(result))
                {
                    return;
                }
            }

            if (playing)
            {
                this.timer.Start();
            }
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            uint ms = 0;
            var playing = false;
            var paused = false;

            if (this.channel != null)
            {
                RESULT result = this.channel.getPosition(out ms, TIMEUNIT.MS);
                if (result != RESULT.OK && result != RESULT.ERR_INVALID_HANDLE)
                {
                    this.ERRCHECK(result);
                }

                result = this.channel.isPlaying(out playing);
                if (result != RESULT.OK && result != RESULT.ERR_INVALID_HANDLE)
                {
                    this.ERRCHECK(result);
                }

                result = this.channel.getPaused(out paused);
                if (result != RESULT.OK && result != RESULT.ERR_INVALID_HANDLE)
                {
                    this.ERRCHECK(result);
                }
            }

            this.FMODtimerLabel.Text = $"{ms / 1000 / 60}:{ms / 1000 % 60}.{ms / 10 % 100} / {this.FMODlenms / 1000 / 60}:{this.FMODlenms / 1000 % 60}.{this.FMODlenms / 10 % 100}";
            this.FMODprogressBar.Value = (int) (ms * 1000 / this.FMODlenms);
            this.FMODstatusLabel.Text = paused ? "Paused " : playing ? "Playing" : "Stopped";

            if (this.system != null && this.channel != null)
            {
                this.system.update();
            }
        }

        private bool ERRCHECK(RESULT result)
        {
            if (result == RESULT.OK)
            {
                return false;
            }

            this.FMODreset();
            this.StatusStripUpdate($"FMOD error! {result} - {Error.String(result)}");
            return true;
        }

        private void ExportAssets_Click(object sender, EventArgs e)
        {
            if (Studio.exportableAssets.Count > 0)
            {
                var saveFolderDialog1 = new OpenFolderDialog();

                if (saveFolderDialog1.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                this.timer.Stop();

                List<AssetItem> toExportAssets = null;

                switch (((ToolStripItem) sender).Name)
                {
                    case "exportAllAssetsMenuItem":
                        toExportAssets = Studio.exportableAssets;
                        break;
                    case "exportFilteredAssetsMenuItem":
                        toExportAssets = Studio.visibleAssets;
                        break;
                    case "exportSelectedAssetsMenuItem":
                        toExportAssets = new List<AssetItem>(this.assetListView.SelectedIndices.Count);
                        foreach (int i in this.assetListView.SelectedIndices)
                        {
                            toExportAssets.Add((AssetItem) this.assetListView.Items[i]);
                        }
                        break;
                }

                Studio.ExportAssets(saveFolderDialog1.Folder, toExportAssets, this.assetGroupOptions.SelectedIndex, this.openAfterExport.Checked);
            }
            else
            {
                this.StatusStripUpdate("No exportable assets loaded");
            }
        }

        private void StatusStripUpdate(string statusText)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => this.toolStripStatusLabel1.Text = statusText));
            }
            else
            {
                this.toolStripStatusLabel1.Text = statusText;
            }
        }

        private void initOpenTK()
        {
            this.changeGLSize(this.glControl1.Size);
            GL.ClearColor(Color.CadetBlue);

            this.pgmID = GL.CreateProgram();
            this.loadShader("vs", ShaderType.VertexShader, this.pgmID, out int vsID);
            this.loadShader("fs", ShaderType.FragmentShader, this.pgmID, out int fsID);
            GL.LinkProgram(this.pgmID);

            this.pgmColorID = GL.CreateProgram();
            this.loadShader("vs", ShaderType.VertexShader, this.pgmColorID, out vsID);
            this.loadShader("fsColor", ShaderType.FragmentShader, this.pgmColorID, out fsID);
            GL.LinkProgram(this.pgmColorID);

            this.pgmBlackID = GL.CreateProgram();
            this.loadShader("vs", ShaderType.VertexShader, this.pgmBlackID, out vsID);
            this.loadShader("fsBlack", ShaderType.FragmentShader, this.pgmBlackID, out fsID);
            GL.LinkProgram(this.pgmBlackID);

            this.attributeVertexPosition = GL.GetAttribLocation(this.pgmID, "vertexPosition");
            this.attributeNormalDirection = GL.GetAttribLocation(this.pgmID, "normalDirection");
            this.attributeVertexColor = GL.GetAttribLocation(this.pgmColorID, "vertexColor");
            this.uniformModelMatrix = GL.GetUniformLocation(this.pgmID, "modelMatrix");
            this.uniformViewMatrix = GL.GetUniformLocation(this.pgmID, "viewMatrix");
            this.uniformProjMatrix = GL.GetUniformLocation(this.pgmID, "projMatrix");
        }

        private void loadShader(string filename, ShaderType type, int program, out int address)
        {
            address = GL.CreateShader(type);
            var str = (string) Resources.ResourceManager.GetObject(filename);
            GL.ShaderSource(address, str);
            GL.CompileShader(address);
            GL.AttachShader(program, address);
            GL.DeleteShader(address);
        }

        private void createVBO(out int vboAddress, Vector3[] data, int address)
        {
            GL.GenBuffers(1, out vboAddress);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboAddress);
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr) (data.Length * Vector3.SizeInBytes), data, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(address, 3, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(address);
        }

        private void createVBO(out int vboAddress, Vector4[] data, int address)
        {
            GL.GenBuffers(1, out vboAddress);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboAddress);
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr) (data.Length * Vector4.SizeInBytes), data, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(address, 4, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(address);
        }

        private void createVBO(out int vboAddress, Matrix4 data, int address)
        {
            GL.GenBuffers(1, out vboAddress);
            GL.UniformMatrix4(address, false, ref data);
        }

        private void createEBO(out int address, int[] data)
        {
            GL.GenBuffers(1, out address);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, address);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr) (data.Length * sizeof(int)), data, BufferUsageHint.StaticDraw);
        }

        private void createVAO()
        {
            GL.DeleteVertexArray(this.vao);
            GL.GenVertexArrays(1, out this.vao);
            GL.BindVertexArray(this.vao);

            this.createVBO(out int vboPositions, this.vertexData, this.attributeVertexPosition);

            if (this.normalMode == 0)
            {
                this.createVBO(out int vboNormals, this.normal2Data, this.attributeNormalDirection);
            }
            else
            {
                if (this.normalData != null)
                {
                    this.createVBO(out int vboNormals, this.normalData, this.attributeNormalDirection);
                }
            }

            this.createVBO(out int vboColors, this.colorData, this.attributeVertexColor);
            this.createVBO(out int vboModelMatrix, this.modelMatrixData, this.uniformModelMatrix);
            this.createVBO(out int vboViewMatrix, this.viewMatrixData, this.uniformViewMatrix);
            this.createVBO(out int vboProjMatrix, this.projMatrixData, this.uniformProjMatrix);
            this.createEBO(out int eboElements, this.indiceData);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);
        }

        private void changeGLSize(Size size)
        {
            GL.Viewport(0, 0, size.Width, size.Height);

            if (size.Width <= size.Height)
            {
                float k = 1.0f * size.Width / size.Height;
                this.projMatrixData = Matrix4.CreateScale(1, k, 1);
            }
            else
            {
                float k = 1.0f * size.Height / size.Width;
                this.projMatrixData = Matrix4.CreateScale(k, 1, 1);
            }
        }

        private void preview_Resize(object sender, EventArgs e)
        {
            if (!this.glControlLoaded || !this.glControl1.Visible)
            {
                return;
            }

            this.changeGLSize(this.glControl1.Size);
            this.glControl1.Invalidate();
        }

        private void glControl1_Load(object sender, EventArgs e)
        {
            this.initOpenTK();
            this.glControlLoaded = true;
        }

        private void glControl1_Paint(object sender, PaintEventArgs e)
        {
            this.glControl1.MakeCurrent();
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.BindVertexArray(this.vao);

            if (this.wireFrameMode == 0 || this.wireFrameMode == 2)
            {
                GL.UseProgram(this.shadeMode == 0 ? this.pgmID : this.pgmColorID);
                GL.UniformMatrix4(this.uniformModelMatrix, false, ref this.modelMatrixData);
                GL.UniformMatrix4(this.uniformViewMatrix, false, ref this.viewMatrixData);
                GL.UniformMatrix4(this.uniformProjMatrix, false, ref this.projMatrixData);
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                GL.DrawElements(BeginMode.Triangles, this.indiceData.Length, DrawElementsType.UnsignedInt, 0);
            }

            //Wireframe
            if (this.wireFrameMode == 1 || this.wireFrameMode == 2)
            {
                GL.Enable(EnableCap.PolygonOffsetLine);
                GL.PolygonOffset(-1, -1);
                GL.UseProgram(this.pgmBlackID);
                GL.UniformMatrix4(this.uniformModelMatrix, false, ref this.modelMatrixData);
                GL.UniformMatrix4(this.uniformViewMatrix, false, ref this.viewMatrixData);
                GL.UniformMatrix4(this.uniformProjMatrix, false, ref this.projMatrixData);
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                GL.DrawElements(BeginMode.Triangles, this.indiceData.Length, DrawElementsType.UnsignedInt, 0);
                GL.Disable(EnableCap.PolygonOffsetLine);
            }

            GL.BindVertexArray(0);
            GL.Flush();
            this.glControl1.SwapBuffers();
        }

        private void glControl1_MouseWheel(object sender, MouseEventArgs e)
        {
            if (!this.glControl1.Visible)
            {
                return;
            }

            this.viewMatrixData *= Matrix4.CreateScale(1 + e.Delta / 1000f);
            this.glControl1.Invalidate();
        }

        private void glControl1_MouseDown(object sender, MouseEventArgs e)
        {
            this.mdx = e.X;
            this.mdy = e.Y;

            if (e.Button == MouseButtons.Left)
            {
                this.lmdown = true;
            }

            if (e.Button == MouseButtons.Right)
            {
                this.rmdown = true;
            }
        }

        private void glControl1_MouseMove(object sender, MouseEventArgs e)
        {
            if (!this.lmdown && !this.rmdown)
            {
                return;
            }

            float dx = this.mdx - e.X;
            float dy = this.mdy - e.Y;
            this.mdx = e.X;
            this.mdy = e.Y;

            if (this.lmdown)
            {
                dx *= 0.01f;
                dy *= 0.01f;
                this.viewMatrixData *= Matrix4.CreateRotationX(dy);
                this.viewMatrixData *= Matrix4.CreateRotationY(dx);
            }

            if (this.rmdown)
            {
                dx *= 0.003f;
                dy *= 0.003f;
                this.viewMatrixData *= Matrix4.CreateTranslation(-dx, dy, 0);
            }

            this.glControl1.Invalidate();
        }

        private void glControl1_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this.lmdown = false;
            }

            if (e.Button == MouseButtons.Right)
            {
                this.rmdown = false;
            }
        }

        private void ResetForm()
        {
            this.Text = "AssetStudio";

            Importer.importFiles.Clear();

            // reset progress bar
            this.progressBarManager.Reset();

            // release assets files
            this.StatusStripUpdate("Releasing assets from memory...");

            foreach (AssetsFile assetsFile in Studio.assetsFileList)
            {
                assetsFile.reader.Dispose();
            }

            Studio.assetsFileList.Clear();
            Studio.exportableAssets.Clear();
            Studio.visibleAssets.Clear();

            // release binary readers
            this.StatusStripUpdate("Releasing binary readers from memory...");

            foreach (KeyValuePair<string, EndianBinaryReader> resourceFileReader in Studio.resourceFileReaders)
            {
                resourceFileReader.Value.Dispose();
            }

            this.StatusStripUpdate("Resetting collections and defaults...");

            Studio.resourceFileReaders.Clear();
            Studio.assetsFileIndexCache.Clear();
            Studio.productName = string.Empty;

            this.sceneTreeView?.Nodes.Clear();

            this.assetListView.VirtualListSize = 0;
            this.assetListView.Items.Clear();

            this.classesListView.Items.Clear();
            this.classesListView.Groups.Clear();

            this.previewPanel.BackgroundImage = Resources.preview;
            this.previewPanel.BackgroundImageLayout = ImageLayout.Center;
            this.assetInfoLabel.Visible = false;
            this.assetInfoLabel.Text = null;
            this.textPreviewBox.Visible = false;
            this.fontPreviewBox.Visible = false;
            this.glControl1.Visible = false;
            this.lastSelectedItem = null;
            this.lastLoadedAsset = null;
            this.firstSortColumn = -1;
            this.secondSortColumn = 0;
            this.reverseSort = false;
            this.enableFiltering = false;
            this.listSearch.Text = " Filter ";

            int count = this.filterTypeToolStripMenuItem.DropDownItems.Count;

            for (var i = 1; i < count; i++)
            {
                this.filterTypeToolStripMenuItem.DropDownItems.RemoveAt(1);
            }

            this.FMODreset();

            ScriptHelper.moduleLoaded = false;
            ScriptHelper.LoadedModuleDic.Clear();
            Studio.treeNodeCollection.Clear();
            Studio.treeNodeDictionary.Clear();
        }

        private void assetListView_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || this.assetListView.SelectedIndices.Count <= 0)
            {
                return;
            }

            this.jumpToSceneHierarchyToolStripMenuItem.Visible = false;
            this.showOriginalFileToolStripMenuItem.Visible = false;
            this.exportAnimatorWithSelectedAnimationClipMenuItem.Visible = false;
            this.exportObjectsWithSelectedAnimationClipMenuItem.Visible = false;

            if (this.assetListView.SelectedIndices.Count == 1)
            {
                this.jumpToSceneHierarchyToolStripMenuItem.Visible = true;
                this.showOriginalFileToolStripMenuItem.Visible = true;
            }

            if (this.assetListView.SelectedIndices.Count >= 1)
            {
                List<AssetItem> selectedAssets = this.GetSelectedAssets();
                if (selectedAssets.Any(x => x.Type == ClassIDType.Animator) && selectedAssets.Any(x => x.Type == ClassIDType.AnimationClip))
                {
                    this.exportAnimatorWithSelectedAnimationClipMenuItem.Visible = true;
                }
                else if (selectedAssets.All(x => x.Type == ClassIDType.AnimationClip))
                {
                    this.exportObjectsWithSelectedAnimationClipMenuItem.Visible = true;
                }
            }

            this.contextMenuStrip1.Show(this.assetListView, e.X, e.Y);
        }

        private void exportSelectedAssetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var saveFolderDialog1 = new OpenFolderDialog();

            if (saveFolderDialog1.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            this.timer.Stop();

            Studio.ExportAssets(saveFolderDialog1.Folder, this.GetSelectedAssets(), this.assetGroupOptions.SelectedIndex, this.openAfterExport.Checked);
        }

        private void exportSelectedAssetsToRawToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var saveFolderDialog1 = new OpenFolderDialog();

            if (saveFolderDialog1.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            this.timer.Stop();

            Studio.ExportAssets(saveFolderDialog1.Folder, this.GetSelectedAssets(), this.assetGroupOptions.SelectedIndex, this.openAfterExport.Checked, true);
        }

        private void showOriginalFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var selectasset = (AssetItem) this.assetListView.Items[this.assetListView.SelectedIndices[0]];
            string args = string.Format("/select, \"{0}\"", selectasset.sourceFile.parentPath ?? selectasset.sourceFile.filePath);
            var pfi = new ProcessStartInfo("explorer.exe", args);
            Process process = Process.Start(pfi);
            process?.Dispose();
        }

        private void exportAnimatorwithAnimationClipMenuItem_Click(object sender, EventArgs e)
        {
            AssetItem animator = null;

            var animationList = new List<AssetItem>();

            List<AssetItem> selectedAssets = this.GetSelectedAssets();

            foreach (AssetItem assetPreloadData in selectedAssets)
            {
                if (assetPreloadData.Type == ClassIDType.Animator)
                {
                    animator = assetPreloadData;
                }
                else if (assetPreloadData.Type == ClassIDType.AnimationClip)
                {
                    animationList.Add(assetPreloadData);
                }
            }

            if (animator == null)
            {
                return;
            }

            var saveFolderDialog1 = new OpenFolderDialog();

            if (saveFolderDialog1.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            string exportPath = saveFolderDialog1.Folder + "\\Animator\\";
            this.progressBarManager.Reset(1);

            Studio.ExportAnimatorWithAnimationClip(animator, animationList, exportPath);
        }

        private void exportSelectedObjectsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.sceneTreeView.Nodes.Count > 0)
            {
                var saveFolderDialog1 = new OpenFolderDialog();

                if (saveFolderDialog1.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                string exportPath = saveFolderDialog1.Folder + "\\GameObject\\";

                Studio.ExportObjectsWithAnimationClip(exportPath, this.sceneTreeView.Nodes);
            }
            else
            {
                this.StatusStripUpdate("No Objects available for export");
            }
        }

        private void exportObjectswithAnimationClipMenuItem_Click(object sender, EventArgs e)
        {
            if (this.sceneTreeView.Nodes.Count > 0)
            {
                var saveFolderDialog1 = new OpenFolderDialog();

                if (saveFolderDialog1.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                string exportPath = saveFolderDialog1.Folder + "\\GameObject\\";

                List<AssetItem> animationList = this.GetSelectedAssets().Where(x => x.Type == ClassIDType.AnimationClip).ToList();

                Studio.ExportObjectsWithAnimationClip(exportPath, this.sceneTreeView.Nodes, animationList.Count == 0 ? null : animationList);
            }
            else
            {
                this.StatusStripUpdate("No Objects available for export");
            }
        }

        private void listSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Enter)
            {
                this.FilterAssetList();
            }
        }

        private void liveSearch_CheckedChanged(object sender, EventArgs e)
        {
            if (this.enableLiveSearch.Checked)
            {
                this.FilterAssetList();
            }

            Settings.Default["enableLiveSearch"] = this.enableLiveSearch.Checked;
            Settings.Default.Save();
        }

        private void jumpToSceneHierarchyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var selectasset = (AssetItem) this.assetListView.Items[this.assetListView.SelectedIndices[0]];

            if (selectasset.gameObject == null)
            {
                return;
            }

            this.sceneTreeView.SelectedNode = Studio.treeNodeDictionary[selectasset.gameObject];
            this.tabControl1.SelectedTab = this.tabPage1;
        }

        private void selectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StudioClasses.NativeMethods.SelectAllItems(this.assetListView);
        }

        private void assetListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A)
            {
                StudioClasses.NativeMethods.SelectAllItems(this.assetListView);
            }
        }

        private void textPreviewBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (!e.Control || e.KeyCode != Keys.A)
            {
                return;
            }

            this.textPreviewBox.SelectAll();
            this.textPreviewBox.Focus();
        }

        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {
            if (!this.assetListView.Focused)
            {
                return;
            }

            int selectedCount = this.assetListView.SelectedIndices.Count;

            this.exportSelectedAssetsToolStripMenuItem.Enabled = selectedCount > 0;
            this.exportSelectedAssetsToRawToolStripMenuItem.Enabled = selectedCount > 0;

            this.exportSelectedAssetsToolStripMenuItem.Text = selectedCount == 1 ? Resources.ContextMenu_ExportSelectedAsset : Resources.ContextMenu_ExportSelectedAssets;
            this.exportSelectedAssetsToRawToolStripMenuItem.Text = selectedCount == 1 ? Resources.ContextMenu_ExportSelectedAssetRaw : Resources.ContextMenu_ExportSelectedAssetsRaw;

            string itemSelectedFormat = selectedCount == 1 ? Resources.ContextMenu_ItemSelectedFormat : Resources.ContextMenu_ItemsSelectedFormat;

            this.exportSelectedAssetsToolStripMenuItem.ShortcutKeyDisplayString = string.Format(itemSelectedFormat, selectedCount);
        }

        private void exportAllObjectsSplitToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (this.sceneTreeView.Nodes.Count > 0)
            {
                var saveFolderDialog1 = new OpenFolderDialog();

                if (saveFolderDialog1.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                string savePath = saveFolderDialog1.Folder + "\\";

                int sceneTreeViewNodesCount = this.sceneTreeView.Nodes.Cast<TreeNode>().Sum(x => x.Nodes.Count);

                this.progressBarManager.Reset(sceneTreeViewNodesCount);

                Studio.ExportSplitObjects(savePath, this.sceneTreeView.Nodes);
            }
            else
            {
                this.StatusStripUpdate("No Objects available for export");
            }
        }

        private List<AssetItem> GetSelectedAssets()
        {
            var selectedAssets = new List<AssetItem>();
            foreach (int index in this.assetListView.SelectedIndices)
            {
                selectedAssets.Add((AssetItem) this.assetListView.Items[index]);
            }

            return selectedAssets;
        }

        private static List<AssetItem> ExecuteFilterQuery(string query)
        {
            // ReSharper disable once InvertIf
            if (query.ToLowerInvariant().StartsWith("type:"))
            {
                string typeQuery = query.Remove(0, 5);

                if (typeQuery != string.Empty)
                {
                    return Studio.visibleAssets.FindAll(x => x.TypeString.IndexOf(typeQuery, StringComparison.CurrentCultureIgnoreCase) >= 0);
                }
            }

            return Studio.visibleAssets.FindAll(x => x.Text.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0);
        }

        private void FilterAssetList()
        {
            this.assetListView.BeginUpdate();
            this.assetListView.SelectedIndices.Clear();

            var show = new List<ClassIDType>();

            if (!this.allToolStripMenuItem.Checked)
            {
                for (var i = 1; i < this.filterTypeToolStripMenuItem.DropDownItems.Count; i++)
                {
                    var item = (ToolStripMenuItem) this.filterTypeToolStripMenuItem.DropDownItems[i];

                    if (item.Checked)
                    {
                        show.Add((ClassIDType) Enum.Parse(typeof(ClassIDType), item.Text));
                    }
                }

                Studio.visibleAssets = Studio.exportableAssets.FindAll(x => show.Contains(x.Type));
            }
            else
            {
                Studio.visibleAssets = Studio.exportableAssets;
            }

            if (this.listSearch.Text != " Filter ")
            {
                Studio.visibleAssets = ExecuteFilterQuery(this.listSearch.Text);
            }

            this.assetListView.VirtualListSize = Studio.visibleAssets.Count;
            this.assetListView.EndUpdate();
        }

        private void fontPreviewBox_VisibleChanged(object sender, EventArgs e)
        {
            if (!this.fontPreviewBox.Visible)
            {
                this.fontPreviewBox.SelectionFont.Dispose();
            }
        }
    }
}