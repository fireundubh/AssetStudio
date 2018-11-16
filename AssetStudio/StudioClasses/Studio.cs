using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using AssetStudio.Extensions;
using AssetStudio.Properties;
using AssetStudio.StudioClasses;

namespace AssetStudio
{
    internal static class Studio
    {
        public static List<AssetsFile> assetsFileList = new List<AssetsFile>(); //loaded files
        public static Dictionary<string, int> assetsFileIndexCache = new Dictionary<string, int>();
        public static Dictionary<string, EndianBinaryReader> resourceFileReaders = new Dictionary<string, EndianBinaryReader>(); //use for read res files
        public static List<AssetItem> exportableAssets = new List<AssetItem>(); //used to hold all assets while the ListView is filtered
        private static HashSet<string> assetsNameHash = new HashSet<string>(); //avoid the same name asset
        public static List<AssetItem> visibleAssets = new List<AssetItem>(); //used to build the ListView from all or filtered assets
        public static Dictionary<string, SortedDictionary<int, TypeTreeItem>> AllTypeMap = new Dictionary<string, SortedDictionary<int, TypeTreeItem>>();
        public static List<GameObjectTreeNode> treeNodeCollection = new List<GameObjectTreeNode>();
        public static Dictionary<GameObject, GameObjectTreeNode> treeNodeDictionary = new Dictionary<GameObject, GameObjectTreeNode>();
        public static string mainPath;
        public static string productName = string.Empty;

        //UI
        public static Action<string> StatusStripUpdate;

        public static Action<int> ProgressBarIncrementMaximum;
        public static Action ProgressBarPerformStep;
        public static Action<int> ProgressBarReset;
        public static Action<int> ProgressBarSetValue;

        public enum FileType
        {
            AssetsFile,
            BundleFile,
            WebFile
        }

        public static FileType CheckFileType(Stream stream, out EndianBinaryReader reader)
        {
            reader = new EndianBinaryReader(stream);
            return CheckFileType(reader);
        }

        public static FileType CheckFileType(string fileName, out EndianBinaryReader reader)
        {
            reader = new EndianBinaryReader(File.OpenRead(fileName));
            return CheckFileType(reader);
        }

        private static FileType CheckFileType(EndianBinaryReader reader)
        {
            string signature = reader.ReadStringToNull();

            reader.Position = 0;

            switch (signature)
            {
                case "UnityWeb":
                case "UnityRaw":
                case "\xFA\xFA\xFA\xFA\xFA\xFA\xFA\xFA":
                case "UnityFS":
                    return FileType.BundleFile;
                case "UnityWebData1.0":
                    return FileType.WebFile;
                default:
                {
                    byte[] magic = reader.ReadBytes(2);

                    reader.Position = 0;

                    if (WebFile.gzipMagic.SequenceEqual(magic))
                    {
                        return FileType.WebFile;
                    }

                    reader.Position = 0x20;
                    magic = reader.ReadBytes(6);
                    reader.Position = 0;

                    if (WebFile.brotliMagic.SequenceEqual(magic))
                    {
                        return FileType.WebFile;
                    }

                    return FileType.AssetsFile;
                }
            }
        }

        public static void ExtractFile(string[] fileNames)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                var extractedCount = 0;

                foreach (string fileName in fileNames)
                {
                    FileType type = CheckFileType(fileName, out EndianBinaryReader reader);

                    if (type == FileType.BundleFile)
                    {
                        extractedCount += ExtractBundleFile(fileName, reader);
                    }
                    else if (type == FileType.WebFile)
                    {
                        extractedCount += ExtractWebDataFile(fileName, reader);
                    }
                    else
                    {
                        reader.Dispose();
                    }

                    ProgressBarPerformStep();
                }

                StatusStripUpdate(string.Format("Finished extracting {0} files.", extractedCount));
            });
        }

        private static int ExtractBundleFile(string bundleFileName, EndianBinaryReader reader)
        {
            StatusStripUpdate(string.Format("Decompressing {0} ...", Path.GetFileName(bundleFileName)));

            var bundleFile = new BundleFile(reader, bundleFileName);

            if (bundleFile.fileList.Count <= 0)
            {
                return 0;
            }

            string extractPath = bundleFileName + "_unpacked\\";
            Directory.CreateDirectory(extractPath);

            return ExtractStreamFile(extractPath, bundleFile.fileList);
        }

        private static int ExtractWebDataFile(string webFileName, EndianBinaryReader reader)
        {
            StatusStripUpdate(string.Format("Decompressing {0} ...", Path.GetFileName(webFileName)));

            var webFile = new WebFile(reader);

            if (webFile.fileList.Count <= 0)
            {
                return 0;
            }

            string extractPath = webFileName + "_unpacked\\";
            Directory.CreateDirectory(extractPath);

            return ExtractStreamFile(extractPath, webFile.fileList);
        }

        private static int ExtractStreamFile(string extractPath, List<StreamFile> fileList)
        {
            var extractedCount = 0;

            foreach (StreamFile file in fileList)
            {
                string filePath = extractPath + file.fileName;

                if (!Directory.Exists(extractPath))
                {
                    Directory.CreateDirectory(extractPath);
                }

                if (!File.Exists(filePath) && file.stream is MemoryStream stream)
                {
                    File.WriteAllBytes(filePath, stream.ToArray());
                    extractedCount += 1;
                }

                file.stream.Dispose();
            }

            return extractedCount;
        }

        // TODO: decompose, SRP
        public static void BuildAssetList(Dictionary<ObjectReader, AssetItem> tempDic, bool displayAll, bool displayOriginalName)
        {
            string fileIDfmt = "D" + assetsFileList.Count.ToString().Length;

            for (var i = 0; i < assetsFileList.Count; i++)
            {
                AssetsFile assetsFile = assetsFileList[i];

                var tempExportableAssets = new System.Collections.Generic.List<AssetItem>();

                string fileID = i.ToString(fileIDfmt);

                AssetBundle ab = null;

                var j = 0;
                string assetIDfmt = "D" + assetsFile.m_Objects.Count.ToString().Length;

                foreach (ObjectReader objectReader in assetsFile.ObjectReaders.Values)
                {
                    var assetItem = new AssetItem(objectReader);
                    tempDic.Add(objectReader, assetItem);

                    assetItem.UniqueID = fileID + j.ToString(assetIDfmt);

                    var exportable = false;

                    switch (assetItem.Type)
                    {
                        case ClassIDType.GameObject:
                        {
                            var m_GameObject = new GameObject(objectReader);
                            assetItem.Text = m_GameObject.m_Name;
                            assetsFile.GameObjects.Add(objectReader.m_PathID, m_GameObject);
                            break;
                        }
                        case ClassIDType.Transform:
                        {
                            var m_Transform = new Transform(objectReader);
                            assetsFile.Transforms.Add(objectReader.m_PathID, m_Transform);
                            break;
                        }
                        case ClassIDType.RectTransform:
                        {
                            var m_Rect = new RectTransform(objectReader);
                            assetsFile.Transforms.Add(objectReader.m_PathID, m_Rect);
                            break;
                        }
                        case ClassIDType.Texture2D:
                        {
                            var m_Texture2D = new Texture2D(objectReader, false);
                            if (!string.IsNullOrEmpty(m_Texture2D.path))
                            {
                                assetItem.FullSize = objectReader.byteSize + m_Texture2D.size;
                            }
                            assetItem.Text = m_Texture2D.m_Name;
                            exportable = true;
                            break;
                        }
                        case ClassIDType.AudioClip:
                        {
                            var m_AudioClip = new AudioClip(objectReader, false);
                            if (!string.IsNullOrEmpty(m_AudioClip.m_Source))
                            {
                                assetItem.FullSize = objectReader.byteSize + m_AudioClip.m_Size;
                            }
                            assetItem.Text = m_AudioClip.m_Name;
                            exportable = true;
                            break;
                        }
                        case ClassIDType.VideoClip:
                        {
                            var m_VideoClip = new VideoClip(objectReader, false);
                            if (!string.IsNullOrEmpty(m_VideoClip.m_OriginalPath))
                            {
                                assetItem.FullSize = objectReader.byteSize + (long) m_VideoClip.m_Size;
                            }
                            assetItem.Text = m_VideoClip.m_Name;
                            exportable = true;
                            break;
                        }
                        case ClassIDType.Shader:
                        {
                            var m_Shader = new Shader(objectReader);
                            assetItem.Text = m_Shader.m_ParsedForm?.m_Name ?? m_Shader.m_Name;
                            exportable = true;
                            break;
                        }
                        case ClassIDType.Mesh:
                        case ClassIDType.TextAsset:
                        case ClassIDType.AnimationClip:
                        case ClassIDType.Font:
                        case ClassIDType.MovieTexture:
                        case ClassIDType.Sprite:
                        {
                            var obj = new NamedObject(objectReader);
                            assetItem.Text = obj.m_Name;
                            exportable = true;
                            break;
                        }
                        case ClassIDType.Avatar:
                        case ClassIDType.AnimatorController:
                        case ClassIDType.AnimatorOverrideController:
                        case ClassIDType.Material:
                        case ClassIDType.SpriteAtlas:
                        {
                            var obj = new NamedObject(objectReader);
                            assetItem.Text = obj.m_Name;
                            break;
                        }
                        case ClassIDType.Animator:
                        {
                            exportable = true;
                            break;
                        }
                        case ClassIDType.MonoScript:
                        {
                            var m_Script = new MonoScript(objectReader);

                            assetItem.Text = m_Script.m_ClassName;

                            if (m_Script.m_Namespace == string.Empty)
                            {
                                assetItem.TypeString = string.Format("{0} ({1})", assetItem.TypeString, m_Script.m_AssemblyName);
                            }
                            else
                            {
                                assetItem.TypeString = string.Format("{0} : {1} ({2})", assetItem.TypeString, m_Script.m_Namespace, m_Script.m_AssemblyName);
                            }
                            break;
                        }
                        case ClassIDType.MonoBehaviour:
                        {
                            var m_MonoBehaviour = new MonoBehaviour(objectReader);

                            if (m_MonoBehaviour.m_Name != "")
                            {
                                assetItem.Text = m_MonoBehaviour.m_Name;
                            }

                            if (m_MonoBehaviour.m_Script.TryGet(out ObjectReader script))
                            {
                                var m_Script = new MonoScript(script);

                                if (m_MonoBehaviour.m_Name == "")
                                {
                                    assetItem.Text = m_Script.m_ClassName;
                                }

                                assetItem.TypeString = string.Format("{0} : {1}.{2} ({3})", assetItem.TypeString, m_Script.m_Namespace, m_Script.m_ClassName, m_Script.m_AssemblyName);
                            }
                            exportable = true;
                            break;
                        }
                        case ClassIDType.PlayerSettings:
                        {
                            var plSet = new PlayerSettings(objectReader);
                            productName = plSet.productName;
                            break;
                        }
                        case ClassIDType.AssetBundle:
                        {
                            ab = new AssetBundle(objectReader);
                            assetItem.Text = ab.m_Name;
                            break;
                        }
                    }

                    if (assetItem.Text == "")
                    {
                        assetItem.Text = string.Concat(assetItem.TypeString, " #", assetItem.UniqueID);
                    }

                    assetItem.SubItems.AddRange(new[]
                    {
                        assetItem.TypeString,
                        assetItem.FullSize.ToString()
                    });

                    // Process same name case
                    if (!assetsNameHash.Add((assetItem.TypeString + assetItem.Text).ToUpper()))
                    {
                        assetItem.Text += string.Concat(" #", assetItem.UniqueID);
                    }

                    // Process illegal name case
                    assetItem.Text = FixFileName(assetItem.Text);

                    if (displayAll)
                    {
                        exportable = true;
                    }

                    if (exportable)
                    {
                        tempExportableAssets.Add(assetItem);
                    }

                    objectReader.exportName = assetItem.Text;

                    ProgressBarPerformStep();
                    j++;
                }

                if (displayOriginalName)
                {
                    void GetOriginalName(AssetItem x)
                    {
                        string replacename = ab?.m_Container.Find(y => y.second.asset.m_PathID == x.reader.m_PathID)?.first;

                        if (string.IsNullOrEmpty(replacename))
                        {
                            return;
                        }

                        string ex = Path.GetExtension(replacename);

                        x.Text = !string.IsNullOrEmpty(ex) ? replacename.Replace(ex, "") : replacename;
                    }

                    tempExportableAssets.ForEach(GetOriginalName);
                }

                exportableAssets.AddRange(tempExportableAssets);
                tempExportableAssets.Clear();
            }

            visibleAssets = exportableAssets;
            assetsNameHash.Clear();
        }

        public static void BuildTreeStructure(Dictionary<ObjectReader, AssetItem> tempDic)
        {
            foreach (AssetsFile assetsFile in assetsFileList)
            {
                var fileNode = new GameObjectTreeNode(null); //RootNode
                fileNode.Text = assetsFile.fileName;

                foreach (GameObject m_GameObject in assetsFile.GameObjects.Values)
                {
                    foreach (PPtr m_Component in m_GameObject.m_Components)
                    {
                        if (!m_Component.TryGet(out ObjectReader objectReader))
                        {
                            continue;
                        }

                        switch (objectReader.type)
                        {
                            case ClassIDType.Transform:
                            {
                                m_GameObject.m_Transform = m_Component;
                                break;
                            }
                            case ClassIDType.MeshRenderer:
                            {
                                m_GameObject.m_MeshRenderer = m_Component;
                                break;
                            }
                            case ClassIDType.MeshFilter:
                            {
                                m_GameObject.m_MeshFilter = m_Component;

                                if (m_Component.TryGet(out ObjectReader componentObjectReader))
                                {
                                    var m_MeshFilter = new MeshFilter(componentObjectReader);

                                    if (m_MeshFilter.m_Mesh.TryGet(out componentObjectReader))
                                    {
                                        AssetItem item = tempDic[componentObjectReader];
                                        item.gameObject = m_GameObject;
                                    }
                                }
                                break;
                            }
                            case ClassIDType.SkinnedMeshRenderer:
                            {
                                m_GameObject.m_SkinnedMeshRenderer = m_Component;

                                if (m_Component.TryGet(out ObjectReader componentObjectReader))
                                {
                                    var m_SkinnedMeshRenderer = new SkinnedMeshRenderer(componentObjectReader);
                                    if (m_SkinnedMeshRenderer.m_Mesh.TryGet(out componentObjectReader))
                                    {
                                        AssetItem item = tempDic[componentObjectReader];
                                        item.gameObject = m_GameObject;
                                    }
                                }
                                break;
                            }
                            case ClassIDType.Animator:
                            {
                                m_GameObject.m_Animator = m_Component;

                                AssetItem item = tempDic[objectReader];
                                item.Text = m_GameObject.reader.exportName;

                                objectReader.exportName = m_GameObject.reader.exportName;
                                break;
                            }
                        }
                    }

                    GameObjectTreeNode parentNode = fileNode;

                    if (m_GameObject.m_Transform != null && m_GameObject.m_Transform.TryGetTransform(out Transform m_Transform))
                    {
                        if (m_Transform.m_Father.TryGetTransform(out Transform m_Father))
                        {
                            if (m_Father.m_GameObject.TryGetGameObject(out GameObject parentGameObject))
                            {
                                if (!treeNodeDictionary.TryGetValue(parentGameObject, out parentNode))
                                {
                                    parentNode = new GameObjectTreeNode(parentGameObject);
                                    treeNodeDictionary.Add(parentGameObject, parentNode);
                                }
                            }
                        }
                    }

                    if (!treeNodeDictionary.TryGetValue(m_GameObject, out GameObjectTreeNode currentNode))
                    {
                        currentNode = new GameObjectTreeNode(m_GameObject);
                        treeNodeDictionary.Add(m_GameObject, currentNode);
                    }

                    parentNode.Nodes.Add(currentNode);

                    ProgressBarPerformStep();
                }

                if (fileNode.Nodes.Count > 0)
                {
                    treeNodeCollection.Add(fileNode);
                }
            }
        }

        public static void BuildClassStructures(Dictionary<ObjectReader, AssetItem> tempDic)
        {
            foreach (AssetsFile assetsFile in assetsFileList)
            {
                if (AllTypeMap.TryGetValue(assetsFile.unityVersion, out SortedDictionary<int, TypeTreeItem> curVer))
                {
                    foreach (SerializedType type in assetsFile.m_Types.Where(x => x.m_Nodes != null))
                    {
                        int key = type.classID;

                        if (type.m_ScriptTypeIndex >= 0)
                        {
                            key = -1 - type.m_ScriptTypeIndex;
                        }

                        curVer[key] = new TypeTreeItem(key, type.m_Nodes);
                    }
                }
                else
                {
                    var items = new SortedDictionary<int, TypeTreeItem>();

                    foreach (SerializedType type in assetsFile.m_Types.Where(x => x.m_Nodes != null))
                    {
                        int key = type.classID;

                        if (type.m_ScriptTypeIndex >= 0)
                        {
                            key = -1 - type.m_ScriptTypeIndex;
                        }

                        items.Add(key, new TypeTreeItem(key, type.m_Nodes));
                    }

                    AllTypeMap.Add(assetsFile.unityVersion, items);
                }
            }
        }

        public static string FixFileName(string str)
        {
            if (str.Length >= 260)
            {
                return Path.GetRandomFileName();
            }

            return Path.GetInvalidFileNameChars().Aggregate(str, (current, c) => current.Replace(c, '_'));
        }

        public static string[] ProcessingSplitFiles(List<string> selectFile)
        {
            var splitFiles = new List<string>();
            var set = new HashSet<string>();

            foreach (string x in selectFile)
            {
                if (!x.Contains(".split"))
                {
                    continue;
                }

                string directoryName = Path.GetDirectoryName(x);

                if (directoryName == null)
                {
                    continue;
                }

                string s = Path.Combine(directoryName, Path.GetFileNameWithoutExtension(x));

                if (set.Add(s))
                {
                    splitFiles.Add(s);
                }
            }

            selectFile.RemoveAll(x => x.Contains(".split"));

            foreach (string file in splitFiles)
            {
                if (File.Exists(file))
                {
                    selectFile.Add(file);
                }
            }

            return selectFile.Distinct().ToArray();
        }

        private delegate bool ExportAssetCallback(ObjectReader reader, string exportPath);

        private static void ExportAsset(ObjectReader reader, string exportPath, ExportAssetCallback exportAssetCallback, ref int exportedCount)
        {
            if (exportAssetCallback(reader, exportPath))
            {
                exportedCount++;
            }
        }

        public static void ExportAssets(string savePath, List<AssetItem> toExportAssets, int assetGroupSelectedIndex, bool openAfterExport, bool forceRaw = false)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

                int toExport = toExportAssets.Count;
                var exportedCount = 0;

                ProgressBarReset(toExport);

                foreach (AssetItem assetItem in toExportAssets)
                {
                    string exportPath = Path.Combine(savePath, "_export");

                    if (assetGroupSelectedIndex == 1)
                    {
                        string sourceFileName = Path.GetFileNameWithoutExtension(assetItem.sourceFile.filePath) ?? throw new InvalidOperationException();
                        exportPath = Path.Combine(exportPath, sourceFileName);
                    }
                    else if (assetGroupSelectedIndex == 0)
                    {
                        //exportPath = Path.Combine(savePath, "_export", asset.TypeString);
                        exportPath = Path.Combine(savePath, "_export", assetItem.Type.ToString());
                    }

                    StatusStripUpdate(string.Format(Resources.ExportAssets_ExportingFormat, assetItem.TypeString, assetItem.Text));

                    ObjectReader reader = assetItem.reader;

                    try
                    {
                        if (forceRaw)
                        {
                            if (Exporter.ExportRawFile(reader, exportPath))
                            {
                                exportedCount++;
                            }
                        }
                        else
                        {
                            switch (assetItem.Type)
                            {
                                case ClassIDType.Texture2D:
                                    if (Exporter.ExportTexture2D(reader, exportPath, true))
                                    {
                                        exportedCount++;
                                    }
                                    break;
                                case ClassIDType.AudioClip:
                                    ExportAsset(reader, exportPath, Exporter.ExportAudioClip, ref exportedCount);
                                    break;
                                case ClassIDType.Shader:
                                    ExportAsset(reader, exportPath, Exporter.ExportShader, ref exportedCount);
                                    break;
                                case ClassIDType.TextAsset:
                                    ExportAsset(reader, exportPath, Exporter.ExportTextAsset, ref exportedCount);
                                    break;
                                case ClassIDType.MonoScript:
                                    ExportAsset(reader, exportPath, Exporter.ExportMonoScript, ref exportedCount);
                                    break;
                                case ClassIDType.MonoBehaviour:
                                    ExportAsset(reader, exportPath, Exporter.ExportMonoBehaviour, ref exportedCount);
                                    break;
                                case ClassIDType.Font:
                                    ExportAsset(reader, exportPath, Exporter.ExportFont, ref exportedCount);
                                    break;
                                case ClassIDType.Mesh:
                                    ExportAsset(reader, exportPath, Exporter.ExportMesh, ref exportedCount);
                                    break;
                                case ClassIDType.VideoClip:
                                    ExportAsset(reader, exportPath, Exporter.ExportVideoClip, ref exportedCount);
                                    break;
                                case ClassIDType.MovieTexture:
                                    ExportAsset(reader, exportPath, Exporter.ExportMovieTexture, ref exportedCount);
                                    break;
                                case ClassIDType.Sprite:
                                    ExportAsset(reader, exportPath, Exporter.ExportSprite, ref exportedCount);
                                    break;
                                case ClassIDType.Animator:
                                    if (Exporter.ExportAnimator(reader, exportPath))
                                    {
                                        exportedCount++;
                                    }
                                    break;
                                case ClassIDType.AnimationClip:
                                    break;
                                default:
                                    if (Exporter.ExportRawFile(reader, exportPath))
                                    {
                                        exportedCount++;
                                    }
                                    break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(string.Format(Resources.ExportAssets_ErrorFormat, assetItem.Type, assetItem.Text, Environment.NewLine, ex.Message, Environment.NewLine, ex.StackTrace));
                    }

                    ProgressBarPerformStep();
                }

                string statusText = exportedCount == 0 ? Resources.ExportAssets_NothingExported : string.Format(Resources.ExportAssets_FinishedFormat, exportedCount);

                if (toExport > exportedCount)
                {
                    statusText += string.Format(Resources.ExportAssets_SkippedFormat, toExport - exportedCount);
                }

                StatusStripUpdate(statusText);

                if (openAfterExport && exportedCount > 0)
                {
                    Process process = Process.Start(savePath);
                    process?.Dispose();
                }
            });
        }

        public static void ExportSplitObjects(string savePath, TreeNodeCollection nodes)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                foreach (GameObjectTreeNode node in nodes)
                {
                    // traverse first-level child nodes
                    foreach (GameObjectTreeNode j in node.Nodes)
                    {
                        ProgressBarPerformStep();

                        // collect all child nodes
                        var gameObjects = new List<GameObject>();
                        CollectNode(j, gameObjects);

                        // skip objects that do not need to be exported
                        if (gameObjects.All(x => x.m_SkinnedMeshRenderer == null && x.m_MeshFilter == null))
                        {
                            continue;
                        }

                        // Handle illegal file names
                        string filename = FixFileName(j.Text);

                        // store each file in separate folder
                        string targetPath = string.Format("{0}{1}\\", savePath, filename);

                        // handle existing files
                        for (var i = 1;; i++)
                        {
                            if (Directory.Exists(targetPath))
                            {
                                targetPath = string.Format("{0}{1} ({2})\\", savePath, filename, i);
                            }
                            else
                            {
                                break;
                            }
                        }

                        Directory.CreateDirectory(targetPath);

                        // Export FBX
                        StatusStripUpdate($"Exporting {filename}.fbx");

                        try
                        {
                            Exporter.ExportGameObject(j.gameObject, targetPath);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(string.Format("{0}{1}{2}", ex.Message, Environment.NewLine, ex.StackTrace));
                        }

                        StatusStripUpdate($"Finished exporting {filename}.fbx");
                    }
                }

                StatusStripUpdate("Finished");
            });
        }

        private static void CollectNode(GameObjectTreeNode node, List<GameObject> gameObjects)
        {
            gameObjects.Add(node.gameObject);
            foreach (GameObjectTreeNode i in node.Nodes)
            {
                CollectNode(i, gameObjects);
            }
        }

        public static void ExportAnimatorWithAnimationClip(AssetItem assetItem, List<AssetItem> assetItemList, string exportPath)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                StatusStripUpdate(string.Format("Exporting {0}", assetItem.Text));

                try
                {
                    Exporter.ExportAnimator(assetItem.reader, exportPath, assetItemList);
                    StatusStripUpdate(string.Format("Finished exporting {0}", assetItem.Text));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format("{0}{1}{2}", ex.Message, Environment.NewLine, ex.StackTrace));
                    StatusStripUpdate("Error in export");
                }

                ProgressBarPerformStep();
            });
        }

        public static void ExportObjectsWithAnimationClip(string exportPath, TreeNodeCollection nodes, List<AssetItem> assetItemList = null)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                var gameObjects = new List<GameObject>();

                GetSelectedParentNode(nodes, gameObjects);

                if (gameObjects.Count > 0)
                {
                    ProgressBarReset(gameObjects.Count);

                    foreach (GameObject gameObject in gameObjects)
                    {
                        StatusStripUpdate(string.Format("Exporting {0}", gameObject.m_Name));

                        try
                        {
                            Exporter.ExportGameObject(gameObject, exportPath, assetItemList);

                            StatusStripUpdate(string.Format("Finished exporting {0}", gameObject.m_Name));
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(string.Format("{0}\r\n{1}", ex.Message, ex.StackTrace));

                            StatusStripUpdate("Error in export");
                        }

                        ProgressBarPerformStep();
                    }
                }
                else
                {
                    StatusStripUpdate("No Object can be exported.");
                }
            });
        }

        private static void GetSelectedParentNode(TreeNodeCollection nodes, List<GameObject> gameObjects)
        {
            foreach (GameObjectTreeNode i in nodes)
            {
                if (i.Checked)
                {
                    gameObjects.Add(i.gameObject);
                }
                else
                {
                    GetSelectedParentNode(i.Nodes, gameObjects);
                }
            }
        }

        public static float[] QuatToEuler(float[] q)
        {
            double eax = 0;
            double eay = 0;
            double eaz = 0;

            float qx = q[0];
            float qy = q[1];
            float qz = q[2];
            float qw = q[3];

            var M = new double[4, 4];

            double Nq = qx * qx + qy * qy + qz * qz + qw * qw;
            double s = (Nq > 0.0) ? (2.0 / Nq) : 0.0;
            double xs = qx * s, ys = qy * s, zs = qz * s;
            double wx = qw * xs, wy = qw * ys, wz = qw * zs;
            double xx = qx * xs, xy = qx * ys, xz = qx * zs;
            double yy = qy * ys, yz = qy * zs, zz = qz * zs;

            M[0, 0] = 1.0 - (yy + zz);
            M[0, 1] = xy - wz;
            M[0, 2] = xz + wy;
            M[1, 0] = xy + wz;
            M[1, 1] = 1.0 - (xx + zz);
            M[1, 2] = yz - wx;
            M[2, 0] = xz - wy;
            M[2, 1] = yz + wx;
            M[2, 2] = 1.0 - (xx + yy);
            M[3, 0] = M[3, 1] = M[3, 2] = M[0, 3] = M[1, 3] = M[2, 3] = 0.0;
            M[3, 3] = 1.0;

            double test = Math.Sqrt(M[0, 0] * M[0, 0] + M[1, 0] * M[1, 0]);

            if (test > 16 * 1.19209290E-07F) //FLT_EPSILON
            {
                eax = Math.Atan2(M[2, 1], M[2, 2]);
                eay = Math.Atan2(-M[2, 0], test);
                eaz = Math.Atan2(M[1, 0], M[0, 0]);
            }
            else
            {
                eax = Math.Atan2(-M[1, 2], M[1, 1]);
                eay = Math.Atan2(-M[2, 0], test);
                eaz = 0;
            }

            return new[]
            {
                (float) (eax * 180 / Math.PI),
                (float) (eay * 180 / Math.PI),
                (float) (eaz * 180 / Math.PI)
            };
        }
    }
}