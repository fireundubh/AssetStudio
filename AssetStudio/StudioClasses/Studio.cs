using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using AssetStudio.Extensions;
using dnlib.DotNet;
using AssetStudio.Properties;
using AssetStudio.StudioClasses;
using static AssetStudio.Exporter;

namespace AssetStudio
{
    internal static class Studio
    {
        public static List<AssetsFile> assetsfileList = new List<AssetsFile>(); //loaded files
        public static Dictionary<string, int> assetsFileIndexCache = new Dictionary<string, int>();
        public static Dictionary<string, EndianBinaryReader> resourceFileReaders = new Dictionary<string, EndianBinaryReader>(); //use for read res files
        public static List<AssetPreloadData> exportableAssets = new List<AssetPreloadData>(); //used to hold all assets while the ListView is filtered
        private static HashSet<string> assetsNameHash = new HashSet<string>(); //avoid the same name asset
        public static List<AssetPreloadData> visibleAssets = new List<AssetPreloadData>(); //used to build the ListView from all or filtered assets
        public static Dictionary<string, SortedDictionary<int, TypeTreeItem>> AllTypeMap = new Dictionary<string, SortedDictionary<int, TypeTreeItem>>();
        public static string mainPath;
        public static string productName = "";
        public static Dictionary<string, ModuleDef> LoadedModuleDic = new Dictionary<string, ModuleDef>();
        public static List<GameObjectTreeNode> treeNodeCollection = new List<GameObjectTreeNode>();
        public static Dictionary<GameObject, GameObjectTreeNode> treeNodeDictionary = new Dictionary<GameObject, GameObjectTreeNode>();

	    public static ModuleContext moduleContext;

        //UI
        public static Action<int> SetProgressBarValue;
        public static Action<int> SetProgressBarMaximum;
        public static Action ProgressBarPerformStep;
        public static Action<string> StatusStripUpdate;
        public static Action<int> ProgressBarMaximumAdd;

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
            var signature = reader.ReadStringToNull();
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
                        var magic = reader.ReadBytes(2);
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
                int extractedCount = 0;
                foreach (var fileName in fileNames)
                {
                    var type = CheckFileType(fileName, out var reader);
                    if (type == FileType.BundleFile)
                        extractedCount += ExtractBundleFile(fileName, reader);
                    else if (type == FileType.WebFile)
                        extractedCount += ExtractWebDataFile(fileName, reader);
                    else
                        reader.Dispose();
                    ProgressBarPerformStep();
                }
                StatusStripUpdate($"Finished extracting {extractedCount} files.");
            });
        }

        private static int ExtractBundleFile(string bundleFileName, EndianBinaryReader reader)
        {
            StatusStripUpdate($"Decompressing {Path.GetFileName(bundleFileName)} ...");

            var bundleFile = new BundleFile(reader, bundleFileName);
            reader.Dispose();

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
            StatusStripUpdate($"Decompressing {Path.GetFileName(webFileName)} ...");

            var webFile = new WebFile(reader);
            reader.Dispose();

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
            int extractedCount = 0;
            foreach (var file in fileList)
            {
                var filePath = extractPath + file.fileName;
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

        public static void BuildAssetStructures(bool loadAssets, bool displayAll, bool buildHierarchy, bool buildClassStructures, bool displayOriginalName)
        {
            // first loop - read asset data & create list
            if (loadAssets)
            {
	            BuildAssetList(displayAll, displayOriginalName);
            }

            // second loop - build tree structure
            if (buildHierarchy)
            {
	            BuildTreeStructure();
            }

            // build list of class structures
            if (buildClassStructures)
            {
	            BuildClassStructures();
            }
        }

	    private static void BuildAssetList(bool displayAll, bool displayOriginalName)
	    {
		    SetProgressBarValue(0);
		    SetProgressBarMaximum(assetsfileList.Sum(x => x.preloadTable.Values.Count));
		    StatusStripUpdate("Building asset list...");

		    string fileIDfmt = "D" + assetsfileList.Count.ToString().Length;

		    for (var i = 0; i < assetsfileList.Count; i++)
		    {
			    AssetsFile assetsFile = assetsfileList[i];

			    string fileID = i.ToString(fileIDfmt);

			    AssetBundle ab = null;

			    foreach (AssetPreloadData asset in assetsFile.preloadTable.Values)
			    {
				    asset.uniqueID = fileID + asset.uniqueID;

				    var exportable = false;

				    switch (asset.Type)
				    {
					    case ClassIDType.GameObject:
					    {
						    var m_GameObject = new GameObject(asset);
						    asset.Text = m_GameObject.m_Name;
						    assetsFile.GameObjectList.Add(asset.m_PathID, m_GameObject);
						    break;
					    }
					    case ClassIDType.Transform:
					    {
						    var m_Transform = new Transform(asset);
						    assetsFile.TransformList.Add(asset.m_PathID, m_Transform);
						    break;
					    }
					    case ClassIDType.RectTransform:
					    {
						    var m_Rect = new RectTransform(asset);
						    assetsFile.TransformList.Add(asset.m_PathID, m_Rect);
						    break;
					    }
					    case ClassIDType.Texture2D:
					    {
						    var m_Texture2D = new Texture2D(asset, false);
						    if (!string.IsNullOrEmpty(m_Texture2D.path))
						    {
							    asset.FullSize = asset.Size + m_Texture2D.size;
						    }
						    goto case ClassIDType.NamedObject;
					    }
					    case ClassIDType.AudioClip:
					    {
						    var m_AudioClip = new AudioClip(asset, false);
						    if (!string.IsNullOrEmpty(m_AudioClip.m_Source))
						    {
							    asset.FullSize = asset.Size + m_AudioClip.m_Size;
						    }
						    goto case ClassIDType.NamedObject;
					    }
					    case ClassIDType.VideoClip:
					    {
						    var m_VideoClip = new VideoClip(asset, false);
						    if (!string.IsNullOrEmpty(m_VideoClip.m_OriginalPath))
						    {
							    asset.FullSize = asset.Size + (long) m_VideoClip.m_Size;
						    }
						    goto case ClassIDType.NamedObject;
					    }
					    case ClassIDType.NamedObject:
					    case ClassIDType.Mesh:
					    case ClassIDType.Shader:
					    case ClassIDType.TextAsset:
					    case ClassIDType.AnimationClip:
					    case ClassIDType.Font:
					    case ClassIDType.MovieTexture:
					    case ClassIDType.Sprite:
					    {
						    var obj = new NamedObject(asset);
						    asset.Text = obj.m_Name;
						    exportable = true;
						    break;
					    }
					    case ClassIDType.Avatar:
					    case ClassIDType.AnimatorController:
					    case ClassIDType.AnimatorOverrideController:
					    case ClassIDType.Material:
					    case ClassIDType.SpriteAtlas:
					    {
						    var obj = new NamedObject(asset);
						    asset.Text = obj.m_Name;
						    break;
					    }
					    case ClassIDType.Animator:
					    {
						    exportable = true;
						    break;
					    }
					    case ClassIDType.MonoScript:
					    {
						    var m_Script = new MonoScript(asset);

						    asset.Text = m_Script.m_ClassName;
						    if (m_Script.m_Namespace == string.Empty)
						    {
							    asset.TypeString = string.Format("{0} ({1})", asset.TypeString, m_Script.m_AssemblyName);
						    }
						    else
						    {
							    asset.TypeString = string.Format("{0} : {1} ({2})", asset.TypeString, m_Script.m_Namespace, m_Script.m_AssemblyName);
						    }
						    break;
					    }
					    case ClassIDType.MonoBehaviour:
					    {
						    var m_MonoBehaviour = new MonoBehaviour(asset);

						    if (m_MonoBehaviour.m_Name != "")
						    {
							    asset.Text = m_MonoBehaviour.m_Name;
						    }

						    if (m_MonoBehaviour.m_Script.TryGetPD(out AssetPreloadData script))
						    {
							    var m_Script = new MonoScript(script);

							    if (m_MonoBehaviour.m_Name == "")
							    {
								    asset.Text = m_Script.m_ClassName;
							    }

							    asset.TypeString = string.Format("{0} : {1}.{2} ({3})", asset.TypeString, m_Script.m_Namespace, m_Script.m_ClassName, m_Script.m_AssemblyName);
						    }
						    exportable = true;
						    break;
					    }
					    case ClassIDType.PlayerSettings:
					    {
						    var plSet = new PlayerSettings(asset);
						    productName = plSet.productName;
						    break;
					    }
					    case ClassIDType.AssetBundle:
					    {
						    ab = new AssetBundle(asset);
						    asset.Text = ab.m_Name;
						    break;
					    }
				    }

				    if (asset.Text == "")
				    {
					    asset.Text = asset.TypeString + " #" + asset.uniqueID;
				    }

				    asset.SubItems.AddRange(new[]
				    {
					    asset.TypeString,
					    asset.FullSize.ToString()
				    });

				    //处理同名文件
				    if (!assetsNameHash.Add((asset.TypeString + asset.Text).ToUpper()))
				    {
					    asset.Text += " #" + asset.uniqueID;
				    }

				    //处理非法文件名
				    asset.Text = FixFileName(asset.Text);

				    if (displayAll)
				    {
					    exportable = true;
				    }

				    if (exportable)
				    {
					    assetsFile.exportableAssets.Add(asset);
				    }

				    ProgressBarPerformStep();
			    }

			    if (displayOriginalName)
			    {
				    assetsFile.exportableAssets.ForEach(x =>
				                                        {
					                                        string replacename = ab?.m_Container.Find(y => y.second.asset.m_PathID == x.m_PathID)?.first;
					                                        if (!string.IsNullOrEmpty(replacename))
					                                        {
						                                        string ex = Path.GetExtension(replacename);
						                                        x.Text = !string.IsNullOrEmpty(ex) ? replacename.Replace(ex, "") : replacename;
					                                        }
				                                        });
			    }

			    exportableAssets.AddRange(assetsFile.exportableAssets);
		    }

		    visibleAssets = exportableAssets;
		    assetsNameHash.Clear();
	    }

	    private static void BuildTreeStructure()
	    {
		    int gameObjectCount = assetsfileList.Sum(x => x.GameObjectList.Values.Count);

		    if (gameObjectCount <= 0)
		    {
			    return;
		    }

		    SetProgressBarValue(0);
		    SetProgressBarMaximum(gameObjectCount);
		    StatusStripUpdate("Building tree structure...");

		    foreach (AssetsFile assetsFile in assetsfileList)
		    {
			    var fileNode = new GameObjectTreeNode(null); //RootNode
			    fileNode.Text = assetsFile.fileName;

			    foreach (GameObject m_GameObject in assetsFile.GameObjectList.Values)
			    {
				    foreach (PPtr m_Component in m_GameObject.m_Components)
				    {
					    if (!m_Component.TryGetPD(out AssetPreloadData asset))
					    {
						    continue;
					    }

					    switch (asset.Type)
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
							    if (m_Component.TryGetPD(out AssetPreloadData assetPreloadData))
							    {
								    var m_MeshFilter = new MeshFilter(assetPreloadData);
								    if (m_MeshFilter.m_Mesh.TryGetPD(out assetPreloadData))
								    {
									    assetPreloadData.gameObject = m_GameObject;
								    }
							    }
							    break;
						    }
						    case ClassIDType.SkinnedMeshRenderer:
						    {
							    m_GameObject.m_SkinnedMeshRenderer = m_Component;
							    if (m_Component.TryGetPD(out AssetPreloadData assetPreloadData))
							    {
								    var m_SkinnedMeshRenderer = new SkinnedMeshRenderer(assetPreloadData);
								    if (m_SkinnedMeshRenderer.m_Mesh.TryGetPD(out assetPreloadData))
								    {
									    assetPreloadData.gameObject = m_GameObject;
								    }
							    }
							    break;
						    }
						    case ClassIDType.Animator:
						    {
							    m_GameObject.m_Animator = m_Component;
							    asset.Text = m_GameObject.preloadData.Text;
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

	    private static void BuildClassStructures()
	    {
		    foreach (AssetsFile assetsFile in assetsfileList)
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
            if (str.Length >= 260) return Path.GetRandomFileName();
            return Path.GetInvalidFileNameChars().Aggregate(str, (current, c) => current.Replace(c, '_'));
        }

        public static string[] ProcessingSplitFiles(List<string> selectFile)
        {
            var splitFiles = selectFile.Where(x => x.Contains(".split"))
                .Select(x => Path.GetDirectoryName(x) + "\\" + Path.GetFileNameWithoutExtension(x))
                .Distinct()
                .ToList();
            selectFile.RemoveAll(x => x.Contains(".split"));
            foreach (var file in splitFiles)
            {
                if (File.Exists(file))
                {
                    selectFile.Add(file);
                }
            }
            return selectFile.Distinct().ToArray();
        }

	    private delegate bool ExportAssetCallback(AssetPreloadData assetPreloadData, string exportPath);

	    private static void ExportAsset(AssetPreloadData assetPreloadData, string exportPath, ExportAssetCallback exportAssetCallback, ref int exportedCount)
	    {
		    if (exportAssetCallback(assetPreloadData, exportPath))
		    {
			    exportedCount++;
		    }
	    }

        public static void ExportAssets(string savePath, List<AssetPreloadData> toExportAssets, int assetGroupSelectedIndex, bool openAfterExport, bool forceRaw = false)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

                int toExport = toExportAssets.Count;
                var exportedCount = 0;

                SetProgressBarValue(0);
                SetProgressBarMaximum(toExport);

                foreach (AssetPreloadData asset in toExportAssets)
                {
                    string exportPath = Path.Combine(savePath, "_export");

                    if (assetGroupSelectedIndex == 1)
                    {
	                    string sourceFileName = Path.GetFileNameWithoutExtension(asset.sourceFile.filePath) ?? throw new InvalidOperationException();
                        exportPath = Path.Combine(exportPath, sourceFileName);
                    }
                    else if (assetGroupSelectedIndex == 0)
                    {
                        //exportPath = Path.Combine(savePath, "_export", asset.TypeString);
                        exportPath = Path.Combine(savePath, "_export", asset.Type.ToString());
                    }

                    StatusStripUpdate(string.Format(Resources.ExportAssets_ExportingFormat, asset.TypeString, asset.Text));

                    try
                    {
	                    if (forceRaw)
	                    {
		                    if (ExportRawFile(asset, exportPath))
		                    {
			                    exportedCount++;
		                    }
	                    }
	                    else
	                    {
		                    switch (asset.Type)
		                    {
			                    case ClassIDType.Texture2D:
				                    if (ExportTexture2D(asset, exportPath, true))
				                    {
					                    exportedCount++;
				                    }
				                    break;
			                    case ClassIDType.AudioClip:
				                    ExportAsset(asset, exportPath, ExportAudioClip, ref exportedCount);
				                    break;
			                    case ClassIDType.Shader:
				                    ExportAsset(asset, exportPath, ExportShader, ref exportedCount);
				                    break;
			                    case ClassIDType.TextAsset:
				                    ExportAsset(asset, exportPath, ExportTextAsset, ref exportedCount);
				                    break;
			                    case ClassIDType.MonoScript:
				                    ExportAsset(asset, exportPath, ExportMonoScript, ref exportedCount);
				                    break;
			                    case ClassIDType.MonoBehaviour:
				                    ExportAsset(asset, exportPath, ExportMonoBehaviour, ref exportedCount);
				                    break;
			                    case ClassIDType.Font:
				                    ExportAsset(asset, exportPath, ExportFont, ref exportedCount);
				                    break;
			                    case ClassIDType.Mesh:
				                    ExportAsset(asset, exportPath, ExportMesh, ref exportedCount);
				                    break;
			                    case ClassIDType.VideoClip:
				                    ExportAsset(asset, exportPath, ExportVideoClip, ref exportedCount);
				                    break;
			                    case ClassIDType.MovieTexture:
				                    ExportAsset(asset, exportPath, ExportMovieTexture, ref exportedCount);
				                    break;
			                    case ClassIDType.Sprite:
				                    ExportAsset(asset, exportPath, ExportSprite, ref exportedCount);
				                    break;
			                    case ClassIDType.Animator:
				                    if (ExportAnimator(asset, exportPath))
				                    {
					                    exportedCount++;
				                    }
				                    break;
			                    case ClassIDType.AnimationClip:
				                    break;
			                    default:
				                    if (ExportRawFile(asset, exportPath))
				                    {
					                    exportedCount++;
				                    }
				                    break;
		                    }
	                    }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(string.Format(Resources.ExportAssets_ErrorFormat, asset.Type, asset.Text, Environment.NewLine, ex.Message, Environment.NewLine, ex.StackTrace));
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
                    //遍历一级子节点
                    foreach (GameObjectTreeNode j in node.Nodes)
                    {
                        ProgressBarPerformStep();
                        //收集所有子节点
                        var gameObjects = new List<GameObject>();
                        CollectNode(j, gameObjects);
                        //跳过一些不需要导出的object
                        if (gameObjects.All(x => x.m_SkinnedMeshRenderer == null && x.m_MeshFilter == null))
                            continue;
                        //处理非法文件名
                        var filename = FixFileName(j.Text);
                        //每个文件存放在单独的文件夹
                        var targetPath = $"{savePath}{filename}\\";
                        //重名文件处理
                        for (int i = 1; ; i++)
                        {
                            if (Directory.Exists(targetPath))
                            {
                                targetPath = $"{savePath}{filename} ({i})\\";
                            }
                            else
                            {
                                break;
                            }
                        }
                        Directory.CreateDirectory(targetPath);
                        //导出FBX
                        StatusStripUpdate($"Exporting {filename}.fbx");
                        try
                        {
                            ExportGameObject(j.gameObject, targetPath);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"{ex.Message}\r\n{ex.StackTrace}");
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

        public static void ExportAnimatorWithAnimationClip(AssetPreloadData animator, List<AssetPreloadData> animationList, string exportPath)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                StatusStripUpdate($"Exporting {animator.Text}");
                try
                {
                    ExportAnimator(animator, exportPath, animationList);
                    StatusStripUpdate($"Finished exporting {animator.Text}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{ex.Message}\r\n{ex.StackTrace}");
                    StatusStripUpdate("Error in export");
                }
                ProgressBarPerformStep();
            });
        }

        public static void ExportObjectsWithAnimationClip(string exportPath, TreeNodeCollection nodes, List<AssetPreloadData> animationList = null)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                var gameObjects = new List<GameObject>();
                GetSelectedParentNode(nodes, gameObjects);
                if (gameObjects.Count > 0)
                {
                    SetProgressBarValue(0);
                    SetProgressBarMaximum(gameObjects.Count);
                    foreach (var gameObject in gameObjects)
                    {
                        StatusStripUpdate($"Exporting {gameObject.m_Name}");
                        try
                        {
                            ExportGameObject(gameObject, exportPath, animationList);
                            StatusStripUpdate($"Finished exporting {gameObject.m_Name}");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"{ex.Message}\r\n{ex.StackTrace}");
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

            double[,] M = new double[4, 4];

            double Nq = qx * qx + qy * qy + qz * qz + qw * qw;
            double s = (Nq > 0.0) ? (2.0 / Nq) : 0.0;
            double xs = qx * s, ys = qy * s, zs = qz * s;
            double wx = qw * xs, wy = qw * ys, wz = qw * zs;
            double xx = qx * xs, xy = qx * ys, xz = qx * zs;
            double yy = qy * ys, yz = qy * zs, zz = qz * zs;

            M[0, 0] = 1.0 - (yy + zz); M[0, 1] = xy - wz; M[0, 2] = xz + wy;
            M[1, 0] = xy + wz; M[1, 1] = 1.0 - (xx + zz); M[1, 2] = yz - wx;
            M[2, 0] = xz - wy; M[2, 1] = yz + wx; M[2, 2] = 1.0 - (xx + yy);
            M[3, 0] = M[3, 1] = M[3, 2] = M[0, 3] = M[1, 3] = M[2, 3] = 0.0; M[3, 3] = 1.0;

            double test = Math.Sqrt(M[0, 0] * M[0, 0] + M[1, 0] * M[1, 0]);
            if (test > 16 * 1.19209290E-07F)//FLT_EPSILON
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

            return new[] { (float)(eax * 180 / Math.PI), (float)(eay * 180 / Math.PI), (float)(eaz * 180 / Math.PI) };
        }

	    private static void TryToLoadModules()
	    {
		    string path = Path.Combine(mainPath, "Managed");

		    if (Directory.Exists(path))
		    {
			    LoadModules(path);
		    }
		    else
		    {
			    var openFolderDialog = new OpenFolderDialog { Title = "Select Assembly Folder" };

			    if (openFolderDialog.ShowDialog() == DialogResult.OK)
			    {
				    LoadModules(openFolderDialog.Folder);
			    }
		    }
	    }

	    private static void LoadModules(string path)
	    {
		    if (moduleContext == null)
		    {
			    moduleContext = new ModuleContext();
			    var asmResolver = new AssemblyResolver(moduleContext, true);
			    var resolver = new Resolver(asmResolver);

			    moduleContext.AssemblyResolver = asmResolver;
			    moduleContext.Resolver = resolver;
		    }

		    string[] files = Directory.GetFiles(path, "*.dll");

		    foreach (string file in files)
		    {
			    string moduleFileName = Path.GetFileName(file);

			    if (LoadedModuleDic.ContainsKey(moduleFileName))
			    {
				    continue;
			    }

			    ModuleDefMD module = ModuleDefMD.Load(file, moduleContext);

			    LoadedModuleDic.Add(moduleFileName, module);
		    }
	    }

	    public static TypeDef GetTypeDefOfMonoScript(ModuleDef module, string sourcePath)
	    {
		    TypeDef typeDef = module.Assembly.Find(sourcePath, false);
		    return typeDef ?? module.ExportedTypes.Where(moduleExportedType => moduleExportedType.FullName == sourcePath).Select(moduleExportedType => moduleExportedType.Resolve()).FirstOrDefault();
	    }

	    public static void AddNodes(TreeView treeView, AssetPreloadData assetPreloadData, bool isMonoBehaviour)
	    {
			treeView.Nodes.Clear();

		    if (!isMonoBehaviour)
		    {
			    AddNodes_MonoScript(treeView, assetPreloadData);
		    }
		    else
		    {
			    AddNodes_MonoBehaviour(treeView, assetPreloadData);
		    }
	    }

	    private static void AddNodes_MonoScript(TreeView monoPreviewBox, AssetPreloadData assetPreloadData)
	    {
			TryToLoadModules();

		    var m_Script = new MonoScript(assetPreloadData);

		    TreeNodeCollection nodes = m_Script.RootNode.Nodes;

		    foreach (TreeNode treeNode in nodes)
		    {
			    monoPreviewBox.Nodes.Add(treeNode);
		    }
	    }

	    private static void AddNodes_MonoBehaviour(TreeView previewTree, AssetPreloadData assetPreloadData, int indent = -1, bool isRoot = true)
	    {
		    TryToLoadModules();

			var m_MonoBehaviour = new MonoBehaviour(assetPreloadData);

		    foreach (TreeNode node in m_MonoBehaviour.RootNode.Nodes)
		    {
			    previewTree.Nodes.Add(node);
		    }

			if (!m_MonoBehaviour.m_Script.TryGetPD(out AssetPreloadData script))
			{
				return;
			}

		    var m_Script = new MonoScript(script);

			TreeNode scriptNode = previewTree.Nodes.Find("m_Script", true).FirstOrDefault();

			if (scriptNode != null)
			{
				foreach (TreeNode node in m_Script.RootNode.Nodes["m_Script"].Nodes)
				{
					scriptNode.Nodes.Add(node);
				}
			}

		    if (!LoadedModuleDic.TryGetValue(m_Script.m_AssemblyName, out ModuleDef module))
		    {
			    return;
		    }

		    TypeDef typeDef = GetTypeDefOfMonoScript(module, m_Script.BasePath);

		    if (typeDef == null)
		    {
			    return;
		    }

			
		    IEnumerator<TreeNode> nodeEnumerator = NodeReader.DumpNode(typeDef.ToTypeSig(), assetPreloadData.sourceFile, null, null);
		    if (!nodeEnumerator.MoveNext())
		    {
			    throw new InvalidOperationException("no nodes");
		    }
		    TreeNode rootNode = nodeEnumerator.Current;
		    if (rootNode == null)
		    {
			    throw new InvalidOperationException("no root node");
		    }
		    previewTree.Nodes.Add(rootNode);
		    foreach (TreeNode node in nodeEnumerator.AsEnumerable())
		    {
			    // visit each node as needed, or just iterate to fill tree

			    // this check is possibly not needed
			    if (node.Parent == null)
			    {
				    previewTree.Nodes.Add(node);
			    }
		    }
	    }

        public static string GetScriptString(AssetPreloadData assetPreloadData, int indent = -1, bool isRoot = true)
        {
	        TryToLoadModules();

	        var strings = new List<string>();

	        AssetPreloadData script = assetPreloadData;

	        if (assetPreloadData.Type == ClassIDType.MonoBehaviour)
	        {
		        var m_MonoBehaviour = new MonoBehaviour(assetPreloadData);

		        strings.AddRange(m_MonoBehaviour.RootNodeText);

		        if (!m_MonoBehaviour.m_Script.TryGetPD(out script))
		        {
			        return string.Join(Environment.NewLine, strings);
		        }
	        }

	        var m_Script = new MonoScript(script);

	        List<string> scriptHeader = m_Script.RootNodeText;

	        if (assetPreloadData.Type == ClassIDType.MonoBehaviour)
	        {
				// remove PPtr name
				scriptHeader.RemoveAt(0);

		        // remove newline
				scriptHeader.RemoveAt(scriptHeader.Count - 1);
	        }

	        switch (assetPreloadData.Type)
	        {
		        case ClassIDType.MonoBehaviour:
			        strings.InsertRange(strings.Count > 1 ? strings.Count - 2 : 0, scriptHeader);
			        break;
		        case ClassIDType.MonoScript:
			        strings.AddRange(scriptHeader);
			        return string.Join(Environment.NewLine, strings);
	        }

	        if (!LoadedModuleDic.TryGetValue(m_Script.m_AssemblyName, out ModuleDef module))
	        {
				return string.Join(Environment.NewLine, strings);
	        }

	        TypeDef typeDef = GetTypeDefOfMonoScript(module, m_Script.BasePath);

	        if (typeDef == null)
	        {
		        return string.Join(Environment.NewLine, strings);
	        }

	        var stringBuilder = new StringBuilder();
	        stringBuilder.Append(string.Join(Environment.NewLine, strings).Replace(Environment.NewLine + Environment.NewLine, Environment.NewLine));
	        strings.Clear();

	        try
	        {
		        TypeReader.DumpType(typeDef.ToTypeSig(), stringBuilder, assetPreloadData.sourceFile, null, indent, isRoot);
	        }
	        catch (Exception e)
	        {
				Debug.WriteLine(e.Message);

				if (assetPreloadData.Type != ClassIDType.MonoBehaviour)
				{
					return stringBuilder.ToString();
				}

				stringBuilder.Clear();

			    var m_MonoBehaviour = new MonoBehaviour(assetPreloadData);

			    stringBuilder.Append(string.Join(Environment.NewLine, m_MonoBehaviour.RootNodeText));
	        }

	        return stringBuilder.ToString();
        }

	    public static bool IsExcludedType(TypeDef typeDef, TypeSig typeSig)
	    {
		    if (typeDef.FullName == "UnityEngine.Font") //TODO
		    {
			    return true;
		    }

		    if (typeDef.FullName == "UnityEngine.GUIStyle") //TODO
		    {
			    return true;
		    }

		    if (typeSig.FullName == "System.Object")
		    {
			    return true;
		    }

		    if (typeDef.IsDelegate)
		    {
			    return true;
		    }

		    return false;
	    }

	    public static bool IsAssignFromUnityObject(TypeDef typeDef)
        {
            if (typeDef.FullName == "UnityEngine.Object")
            {
                return true;
            }

	        if (typeDef.BaseType == null)
	        {
		        return false;
	        }

	        if (typeDef.BaseType.FullName == "UnityEngine.Object")
	        {
		        return true;
	        }

	        while (true)
	        {
		        typeDef = typeDef.BaseType.ResolveTypeDefThrow();

		        if (typeDef.BaseType == null)
		        {
			        break;
		        }

		        if (typeDef.BaseType.FullName == "UnityEngine.Object")
		        {
			        return true;
		        }
	        }

	        return false;
        }

	    public static bool IsBaseType(IFullName typeDef)
        {
            switch (typeDef.FullName)
            {
                case "System.Boolean":
                case "System.Byte":
                case "System.SByte":
                case "System.Int16":
                case "System.UInt16":
                case "System.Int32":
                case "System.UInt32":
                case "System.Int64":
                case "System.UInt64":
                case "System.Single":
                case "System.Double":
                case "System.String":
                    return true;
                default:
                    return false;
            }
        }

	    public static bool IsEngineType(IFullName typeDef)
        {
            switch (typeDef.FullName)
            {
                case "UnityEngine.Vector2":
                case "UnityEngine.Vector3":
                case "UnityEngine.Vector4":
                case "UnityEngine.Rect":
                case "UnityEngine.Quaternion":
                case "UnityEngine.Matrix4x4":
                case "UnityEngine.Color":
                case "UnityEngine.Color32":
                case "UnityEngine.LayerMask":
                case "UnityEngine.AnimationCurve":
                case "UnityEngine.Gradient":
                case "UnityEngine.RectOffset":
                case "UnityEngine.GUIStyle":
                    return true;
                default:
                    return false;
            }
        }
    }
}
