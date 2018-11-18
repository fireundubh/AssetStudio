using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AssetStudio.Extensions;
using AssetStudio.Logging;
using dnlib.DotNet;

namespace AssetStudio.StudioClasses
{
    public static class ScriptHelper
    {
        public static Dictionary<string, ModuleDef> LoadedModuleDic = new Dictionary<string, ModuleDef>();

        public static ModuleContext moduleContext;

        private static void TryToLoadModules()
        {
            string path = Path.Combine(Studio.mainPath, "Managed");

            if (Directory.Exists(path))
            {
                LoadModules(path);
            }
            else
            {
                var openFolderDialog = new OpenFolderDialog
                {
                    Title = "Select Assembly Folder"
                };

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

        public static void AddNodes(TreeView treeView, ObjectReader reader, bool isMonoBehaviour)
        {
            treeView.Nodes.Clear();

            TryToLoadModules();

            if (!isMonoBehaviour)
            {
                AddNodes_MonoScript(treeView, reader);
            }
            else
            {
                AddNodes_MonoBehaviour(treeView, reader);
            }
        }

        private static void AddNodes_MonoScript(TreeView monoPreviewBox, ObjectReader reader)
        {
            var m_Script = new MonoScript(reader);

            monoPreviewBox.Nodes.AddRange(m_Script.RootNode.Nodes, monoPreviewBox);
        }

        private static void AddNodes_MonoBehaviour(TreeView previewTree, ObjectReader reader)
        {
            var m_MonoBehaviour = new MonoBehaviour(reader);

            previewTree.Nodes.AddRange(m_MonoBehaviour.RootNode.Nodes, previewTree);

            if (!m_MonoBehaviour.m_Script.TryGet(out ObjectReader script))
            {
                return;
            }

            var m_Script = new MonoScript(script);

            TreeNode scriptNode = previewTree.Nodes.Find("m_Script", true).FirstOrDefault();

            scriptNode?.Nodes.AddRange(m_Script.RootNode.Nodes["m_Script"].Nodes, previewTree);

            if (!LoadedModuleDic.TryGetValue(m_Script.m_AssemblyName, out ModuleDef module))
            {
                return;
            }

            TypeDef typeDef = GetTypeDefOfMonoScript(module, m_Script.BasePath);

            if (typeDef == null)
            {
                return;
            }

            TypeSig typeSig = typeDef.ToTypeSig();

            LoggingHelper.LogInfo(string.Format("typeDef.FullName = {0}, readerPosition = {1}", typeDef.FullName, reader.Position));

            IEnumerator<TreeNode> nodeEnumerator = NodeReader.DumpNode(null, reader, typeSig, null);

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

            // visit each node as needed, or just iterate to fill tree
            // node.Parent == null check is possibly not needed
            foreach (TreeNode node in nodeEnumerator.AsEnumerable())
            {
                if (node.Parent == null)
                {
                    previewTree.Nodes.Add(node);
                }
            }
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
                case "UnityEngine.AnimationCurve":
                case "UnityEngine.Bounds":
                case "UnityEngine.BoundsInt":
                case "UnityEngine.Color":
                case "UnityEngine.Color32":
                case "UnityEngine.Gradient":
                case "UnityEngine.GUIStyle":
                case "UnityEngine.LayerMask":
                case "UnityEngine.Matrix4x4":
                case "UnityEngine.Quaternion":
                case "UnityEngine.Rect":
                case "UnityEngine.RectInt":
                case "UnityEngine.RectOffset":
                case "UnityEngine.Vector2":
                case "UnityEngine.Vector2Int":
                case "UnityEngine.Vector3":
                case "UnityEngine.Vector3Int":
                case "UnityEngine.Vector4":
                    return true;
                default:
                    return false;
            }
        }
    }
}