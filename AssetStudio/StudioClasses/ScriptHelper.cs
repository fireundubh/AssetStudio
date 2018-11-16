using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using AssetStudio.Extensions;
using dnlib.DotNet;

namespace AssetStudio.StudioClasses
{
    public static class ScriptHelper
    {
        public static bool moduleLoaded;
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
            TryToLoadModules();

            var m_Script = new MonoScript(reader);

            TreeNodeCollection nodes = m_Script.RootNode.Nodes;

            foreach (TreeNode treeNode in nodes)
            {
                monoPreviewBox.Nodes.Add(treeNode);
            }
        }

        private static void AddNodes_MonoBehaviour(TreeView previewTree, ObjectReader reader, int indent = -1, bool isRoot = true)
        {
            TryToLoadModules();

            var m_MonoBehaviour = new MonoBehaviour(reader);

            foreach (TreeNode node in m_MonoBehaviour.RootNode.Nodes)
            {
                previewTree.Nodes.Add(node);
            }

            if (!m_MonoBehaviour.m_Script.TryGet(out ObjectReader script))
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

            IEnumerator<TreeNode> nodeEnumerator = NodeReader.DumpNode(null, reader, typeDef.ToTypeSig(), null);

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
            foreach (TreeNode node in nodeEnumerator.AsEnumerable().Where(node => node.Parent == null))
            {
                previewTree.Nodes.Add(node);
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

        public static string GetScriptString(ObjectReader reader, int indent = -1, bool isRoot = true)
        {
            TryToLoadModules();

            var strings = new List<string>();

            ObjectReader script = reader;

            if (reader.type == ClassIDType.MonoBehaviour)
            {
                var m_MonoBehaviour = new MonoBehaviour(reader);

                strings.AddRange(m_MonoBehaviour.RootNodeText);

                if (!m_MonoBehaviour.m_Script.TryGet(out script))
                {
                    return string.Join(Environment.NewLine, strings);
                }
            }

            var m_Script = new MonoScript(script);

            List<string> scriptHeader = m_Script.RootNodeText;

            if (reader.type == ClassIDType.MonoBehaviour)
            {
                // remove PPtr name
                scriptHeader.RemoveAt(0);

                // remove newline
                scriptHeader.RemoveAt(scriptHeader.Count - 1);
            }

            switch (reader.type)
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
                TypeReader.DumpType(typeDef.ToTypeSig(), stringBuilder, reader, null, indent, isRoot);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);

                if (reader.type != ClassIDType.MonoBehaviour)
                {
                    return stringBuilder.ToString();
                }

                stringBuilder.Clear();

                var m_MonoBehaviour = new MonoBehaviour(reader);

                stringBuilder.Append(string.Join(Environment.NewLine, m_MonoBehaviour.RootNodeText));
            }

            return stringBuilder.ToString();
        }
    }
}