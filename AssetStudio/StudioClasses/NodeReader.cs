using System.IO;
using System.Linq;
using System.Windows.Forms;
using dnlib.DotNet;

namespace AssetStudio.StudioClasses
{
	public class NodeReader
	{
		public static void DumpNode(TreeView previewTree, TypeSig typeSig, AssetsFile assetsFile, string name, int currentDepth, bool isRoot = false, bool isArray = false, int arrayIndex = 0)
		{
			TypeDef typeDef = typeSig.ToTypeDefOrRef().ResolveTypeDefThrow();

			EndianBinaryReader reader = assetsFile.reader;

			if (Studio.IsExcludedType(typeDef, typeSig))
			{
				return;
			}

			if (ReadPrimitiveNode(previewTree, reader, typeDef, typeSig, name, currentDepth, isArray, arrayIndex))
			{
				return;
			}

			if (ReadStringNode(previewTree, reader, typeDef, typeSig, name, currentDepth, isArray, arrayIndex))
			{
				return;
			}

			if (ReadArraySigBaseNode(previewTree, reader, typeDef, typeSig, assetsFile, name, currentDepth))
			{
				return;
			}

			if (ReadGenericInstSigNode(previewTree, reader, typeSig, assetsFile, name, currentDepth, isRoot))
			{
				return;
			}

			DumpClassOrValueTypeNode(previewTree, typeDef, assetsFile, name, currentDepth);
		}

		private static void DumpClassOrValueTypeNode(TreeView previewTree, TypeDef typeDef, AssetsFile assetsFile, string name, int currentDepth)
		{
			if (!typeDef.IsClass && !typeDef.IsValueType)
			{
				return;
			}

			if (name != null && currentDepth != -1)
			{
				TreeNode node = previewTree.Nodes.Add(name, string.Format("{0} {1}", typeDef.Name, name));
				node.Tag = typeDef.ToTypeSig().ElementType;
			}

			if (currentDepth == -1 && typeDef.BaseType.FullName != "UnityEngine.Object")
			{
				DumpNode(previewTree, typeDef.BaseType.ToTypeSig(), assetsFile, null, currentDepth, true);
			}

			if (currentDepth != -1 && typeDef.BaseType.FullName != "System.Object")
			{
				DumpNode(previewTree, typeDef.BaseType.ToTypeSig(), assetsFile, null, currentDepth, true);
			}

			foreach (FieldDef fieldDef in typeDef.Fields)
			{
				FieldAttributes access = fieldDef.Access & FieldAttributes.FieldAccessMask;

				if (access != FieldAttributes.Public)
				{
					if (fieldDef.CustomAttributes.Any(x => x.TypeFullName.Contains("SerializeField")))
					{
						DumpNode(previewTree, fieldDef.FieldType, assetsFile, fieldDef.Name, currentDepth + 1);
					}
				}
				else if ((fieldDef.Attributes & FieldAttributes.Static) == 0 && (fieldDef.Attributes & FieldAttributes.InitOnly) == 0 && (fieldDef.Attributes & FieldAttributes.NotSerialized) == 0)
				{
					DumpNode(previewTree, fieldDef.FieldType, assetsFile, fieldDef.Name, currentDepth + 1);
				}
			}
		}

		private static bool ReadPrimitiveNode(TreeView previewTree, BinaryReader reader, TypeDef typeDef, TypeSig typeSig, string name, int currentDepth, bool isArray = false, int arrayIndex = 0)
		{
			if (!typeSig.IsPrimitive)
			{
				return false;
			}

			object value = TypeReader.ReadAlignedPrimitiveValue(reader, typeSig);

			string nodeText = !isArray ? string.Format("{0} {1} = {2}", typeDef.Name, name, value) : string.Format("[{0}] {1} {2} = {3}", arrayIndex, typeDef.Name, name, value);

			TreeNode node = previewTree.Nodes.Add(name, nodeText);
			node.Tag = typeSig.ElementType;

			return true;
		}

		private static bool ReadStringNode(TreeView previewTree, EndianBinaryReader reader, TypeDef typeDef, TypeSig typeSig, string name, int currentDepth, bool isArray = false, int arrayIndex = 0)
		{
			if (typeSig.FullName != "System.String")
			{
				return false;
			}

			string str = reader.ReadAlignedString();

			string nodeText = !isArray ? string.Format("{0} {1} = \"{2}\"", typeDef.Name, name, str) : string.Format("[{0}] {1} {2} = \"{3}\"", arrayIndex, typeDef.Name, name, str);

			TreeNode node = previewTree.Nodes.Add(name, nodeText);
			node.Tag = typeSig.ElementType;

			return true;
		}

		private static bool ReadArrayNode(TreeView previewTree, EndianBinaryReader reader, TypeSig typeSig, string name, int currentDepth, out int size)
		{
			size = reader.ReadInt32();

			TreeNode arrayNode = previewTree.Nodes.Add(name, string.Format("{0} {1}", typeSig.TypeName, name));
			arrayNode.Tag = typeSig.ElementType;

			NodeHelper.AddKeyedChildNode(arrayNode, name, ref size, "int size = {0}");

			if (typeSig.TypeName == "GUIStyle[]")
			{
				return true;
			}

			return false;
		}

		private static bool ReadArraySigBaseNode(TreeView previewTree, EndianBinaryReader reader, TypeDef typeDef, TypeSig typeSig, AssetsFile assetsFile, string name, int currentDepth)
		{
			if (!(typeSig is ArraySigBase))
			{
				return false;
			}

			if (!typeDef.IsEnum && !Studio.IsBaseType(typeDef) && !Studio.IsAssignFromUnityObject(typeDef) && !Studio.IsEngineType(typeDef) && !typeDef.IsSerializable)
			{
				return true;
			}

			if (ReadArrayNode(previewTree, reader, typeSig, name, currentDepth, out int size))
			{
				return true;
			}

			for (var i = 0; i < size; i++)
			{
				DumpNode(previewTree, typeDef.ToTypeSig(), assetsFile, "data", currentDepth + 3, isArray: true, arrayIndex: i);
			}

			return true;
		}

		private static bool ReadGenericInstSigNode(TreeView previewTree, EndianBinaryReader reader, TypeSig typeSig, AssetsFile assetsFile, string name, int indentDepth, bool isRoot)
		{
			if (isRoot || !(typeSig is GenericInstSig genericInstSig))
			{
				return false;
			}

			if (genericInstSig.GenericArguments.Count != 1)
			{
				return true;
			}

			TypeDef type = genericInstSig.GenericArguments[0].ToTypeDefOrRef().ResolveTypeDefThrow();

			if (!type.IsEnum && !Studio.IsBaseType(type) && !Studio.IsAssignFromUnityObject(type) && !Studio.IsEngineType(type) && !type.IsSerializable)
			{
				return true;
			}

			if (ReadArrayNode(previewTree, reader, typeSig, name, indentDepth, out int size))
			{
				return true;
			}

			for (var i = 0; i < size; i++)
			{
				DumpNode(previewTree, genericInstSig.GenericArguments[0], assetsFile, "data", indentDepth + 3, isArray: true, arrayIndex: i);
			}

			return true;
		}
	}
}
