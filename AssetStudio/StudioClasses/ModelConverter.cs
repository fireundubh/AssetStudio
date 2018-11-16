using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using AssetStudio.StudioClasses;
using SharpDX;

namespace AssetStudio
{
    class ModelConverter : IImported
    {
        public List<ImportedFrame> FrameList { get; protected set; } = new List<ImportedFrame>();
        public List<ImportedMesh> MeshList { get; protected set; } = new List<ImportedMesh>();
        public List<ImportedMaterial> MaterialList { get; protected set; } = new List<ImportedMaterial>();
        public List<ImportedTexture> TextureList { get; protected set; } = new List<ImportedTexture>();
        public List<ImportedKeyframedAnimation> AnimationList { get; protected set; } = new List<ImportedKeyframedAnimation>();
        public List<ImportedMorph> MorphList { get; protected set; } = new List<ImportedMorph>();

        private Avatar avatar;
        private Dictionary<uint, string> morphChannelInfo = new Dictionary<uint, string>();
        private HashSet<ObjectReader> animationClipHashSet = new HashSet<ObjectReader>();
        private Dictionary<uint, string> bonePathHash = new Dictionary<uint, string>();

        public ModelConverter(GameObject m_GameObject)
        {
            if (m_GameObject.m_Animator != null && m_GameObject.m_Animator.TryGet(out ObjectReader m_Animator))
            {
                var animator = new Animator(m_Animator);
                this.InitWithAnimator(animator);
                this.CollectAnimationClip(animator);
            }
            else
            {
                this.InitWithGameObject(m_GameObject);
            }
            this.ConvertAnimations();
        }

        public ModelConverter(GameObject m_GameObject, List<AssetItem> animationList)
        {
            if (m_GameObject.m_Animator != null && m_GameObject.m_Animator.TryGet(out ObjectReader m_Animator))
            {
                var animator = new Animator(m_Animator);
                this.InitWithAnimator(animator);
            }
            else
            {
                this.InitWithGameObject(m_GameObject);
            }
            foreach (AssetItem assetPreloadData in animationList)
            {
                this.animationClipHashSet.Add(assetPreloadData.reader);
            }
            this.ConvertAnimations();
        }

        public ModelConverter(Animator m_Animator)
        {
            this.InitWithAnimator(m_Animator);
            this.CollectAnimationClip(m_Animator);
            this.ConvertAnimations();
        }

        public ModelConverter(Animator m_Animator, List<AssetItem> animationList)
        {
            this.InitWithAnimator(m_Animator);
            foreach (AssetItem assetPreloadData in animationList)
            {
                this.animationClipHashSet.Add(assetPreloadData.reader);
            }
            this.ConvertAnimations();
        }

        private void InitWithAnimator(Animator m_Animator)
        {
            if (m_Animator.m_Avatar.TryGet(out var m_Avatar))
            {
                this.avatar = new Avatar(m_Avatar);
            }

            m_Animator.m_GameObject.TryGetGameObject(out GameObject m_GameObject);
            this.InitWithGameObject(m_GameObject, m_Animator.m_HasTransformHierarchy);
        }

        private void InitWithGameObject(GameObject m_GameObject, bool hasTransformHierarchy = true)
        {
            m_GameObject.m_Transform.TryGetTransform(out Transform m_Transform);
            Transform rootTransform = m_Transform;
            if (!hasTransformHierarchy)
            {
                ImportedFrame rootFrame = this.ConvertFrame(rootTransform);
                this.FrameList.Add(rootFrame);
                this.DeoptimizeTransformHierarchy();
            }
            else
            {
                var frameList = new List<ImportedFrame>();
                Transform tempTransform = rootTransform;
                while (tempTransform.m_Father.TryGetTransform(out Transform m_Father))
                {
                    frameList.Add(this.ConvertFrame(m_Father));
                    tempTransform = m_Father;
                }
                if (frameList.Count > 0)
                {
                    this.FrameList.Add(frameList[frameList.Count - 1]);
                    for (int i = frameList.Count - 2; i >= 0; i--)
                    {
                        ImportedFrame frame = frameList[i];
                        ImportedFrame parent = frameList[i + 1];
                        parent.AddChild(frame);
                    }
                    this.ConvertFrames(m_Transform, frameList[0]);
                }
                else
                {
                    this.ConvertFrames(m_Transform, null);
                }

                this.CreateBonePathHash(rootTransform);
            }

            this.ConvertMeshRenderer(m_Transform);
        }

        private void ConvertMeshRenderer(Transform m_Transform)
        {
            m_Transform.m_GameObject.TryGetGameObject(out GameObject m_GameObject);

            foreach (PPtr m_Component in m_GameObject.m_Components)
            {
                if (!m_Component.TryGet(out ObjectReader componentObject))
                {
                    continue;
                }

                switch (componentObject.type)
                {
                    case ClassIDType.MeshRenderer:
                    {
                        var m_Renderer = new MeshRenderer(componentObject);
                        this.ConvertMeshRenderer(m_Renderer);
                        break;
                    }
                    case ClassIDType.SkinnedMeshRenderer:
                    {
                        var m_SkinnedMeshRenderer = new SkinnedMeshRenderer(componentObject);
                        this.ConvertMeshRenderer(m_SkinnedMeshRenderer);
                        break;
                    }
                    case ClassIDType.Animation:
                    {
                        var m_Animation = new Animation(componentObject);

                        foreach (PPtr animation in m_Animation.m_Animations)
                        {
                            if (animation.TryGet(out ObjectReader animationClip))
                            {
                                this.animationClipHashSet.Add(animationClip);
                            }
                        }
                        break;
                    }
                }
            }

            foreach (PPtr pptr in m_Transform.m_Children)
            {
                if (pptr.TryGetTransform(out Transform child))
                {
                    this.ConvertMeshRenderer(child);
                }
            }
        }

        private void CollectAnimationClip(Animator m_Animator)
        {
            if (!m_Animator.m_Controller.TryGet(out ObjectReader controllerObject))
            {
                return;
            }

            if (controllerObject.type == ClassIDType.AnimatorOverrideController)
            {
                var m_AnimatorOverrideController = new AnimatorOverrideController(controllerObject);

                if (!m_AnimatorOverrideController.m_Controller.TryGet(out controllerObject))
                {
                    return;
                }

                var m_AnimatorController = new AnimatorController(controllerObject);

                foreach (PPtr m_AnimationClip in m_AnimatorController.m_AnimationClips)
                {
                    if (m_AnimationClip.TryGet(out controllerObject))
                    {
                        this.animationClipHashSet.Add(controllerObject);
                    }
                }
            }
            else if (controllerObject.type == ClassIDType.AnimatorController)
            {
                var m_AnimatorController = new AnimatorController(controllerObject);

                foreach (PPtr m_AnimationClip in m_AnimatorController.m_AnimationClips)
                {
                    if (m_AnimationClip.TryGet(out controllerObject))
                    {
                        this.animationClipHashSet.Add(controllerObject);
                    }
                }
            }
        }

        private ImportedFrame ConvertFrame(Transform trans)
        {
            var frame = new ImportedFrame();

            trans.m_GameObject.TryGetGameObject(out GameObject m_GameObject);

            frame.Name = m_GameObject.m_Name;
            frame.InitChildren(trans.m_Children.Count);

            float[] m_EulerRotation = Studio.QuatToEuler(new[]
            {
                trans.m_LocalRotation[0],
                -trans.m_LocalRotation[1],
                -trans.m_LocalRotation[2],
                trans.m_LocalRotation[3]
            });

            frame.LocalRotation = new[]
            {
                m_EulerRotation[0],
                m_EulerRotation[1],
                m_EulerRotation[2]
            };

            frame.LocalScale = new[]
            {
                trans.m_LocalScale[0],
                trans.m_LocalScale[1],
                trans.m_LocalScale[2]
            };

            frame.LocalPosition = new[]
            {
                -trans.m_LocalPosition[0],
                trans.m_LocalPosition[1],
                trans.m_LocalPosition[2]
            };

            return frame;
        }

        private ImportedFrame ConvertFrame(Vector3 t, Quaternion q, Vector3 s, string name)
        {
            var frame = new ImportedFrame();
            frame.Name = name;
            frame.InitChildren(0);

            var m_LocalPosition = new[]
            {
                t.X,
                t.Y,
                t.Z
            };

            var m_LocalRotation = new[]
            {
                q.X,
                q.Y,
                q.Z,
                q.W
            };

            var m_LocalScale = new[]
            {
                s.X,
                s.Y,
                s.Z
            };

            float[] m_EulerRotation = Studio.QuatToEuler(new[]
            {
                m_LocalRotation[0],
                -m_LocalRotation[1],
                -m_LocalRotation[2],
                m_LocalRotation[3]
            });

            frame.LocalRotation = new[]
            {
                m_EulerRotation[0],
                m_EulerRotation[1],
                m_EulerRotation[2]
            };

            frame.LocalScale = new[]
            {
                m_LocalScale[0],
                m_LocalScale[1],
                m_LocalScale[2]
            };

            frame.LocalPosition = new[]
            {
                -m_LocalPosition[0],
                m_LocalPosition[1],
                m_LocalPosition[2]
            };

            return frame;
        }

        private void ConvertFrames(Transform trans, ImportedFrame parent)
        {
            ImportedFrame frame = this.ConvertFrame(trans);

            if (parent == null)
            {
                this.FrameList.Add(frame);
            }
            else
            {
                parent.AddChild(frame);
            }

            foreach (PPtr pptr in trans.m_Children)
            {
                if (pptr.TryGetTransform(out Transform child))
                {
                    this.ConvertFrames(child, frame);
                }
            }
        }

        private void ConvertMeshRenderer(Renderer meshR)
        {
            Mesh mesh = this.GetMesh(meshR);

            if (mesh == null)
            {
                return;
            }

            var iMesh = new ImportedMesh();

            meshR.m_GameObject.TryGetGameObject(out GameObject m_GameObject2);
            m_GameObject2.m_Transform.TryGetTransform(out Transform meshTransform);
            iMesh.Name = this.GetMeshPath(meshTransform);

            iMesh.SubmeshList = new List<ImportedSubmesh>();
            var subHashSet = new HashSet<int>();

            var combine = false;
            var firstSubMesh = 0;

            if (meshR.m_StaticBatchInfo?.subMeshCount > 0)
            {
                firstSubMesh = meshR.m_StaticBatchInfo.firstSubMesh;
                int finalSubMesh = meshR.m_StaticBatchInfo.firstSubMesh + meshR.m_StaticBatchInfo.subMeshCount;

                for (int i = meshR.m_StaticBatchInfo.firstSubMesh; i < finalSubMesh; i++)
                {
                    subHashSet.Add(i);
                }

                combine = true;
            }
            else if (meshR.m_SubsetIndices?.Length > 0)
            {
                firstSubMesh = (int) meshR.m_SubsetIndices.Min(x => x);

                foreach (uint index in meshR.m_SubsetIndices)
                {
                    subHashSet.Add((int) index);
                }

                combine = true;
            }

            var firstFace = 0;

            for (var i = 0; i < mesh.m_SubMeshes.Count; i++)
            {
                int numFaces = (int) mesh.m_SubMeshes[i].indexCount / 3;

                if (subHashSet.Count > 0 && !subHashSet.Contains(i))
                {
                    firstFace += numFaces;
                    continue;
                }

                Mesh.SubMesh submesh = mesh.m_SubMeshes[i];

                var iSubmesh = new ImportedSubmesh();

                Material mat = null;

                if (i - firstSubMesh < meshR.m_Materials.Length)
                {
                    if (meshR.m_Materials[i - firstSubMesh].TryGet(out ObjectReader MaterialPD))
                    {
                        mat = new Material(MaterialPD);
                    }
                }
                ImportedMaterial iMat = this.ConvertMaterial(mat);

                iSubmesh.Material = iMat.Name;
                iSubmesh.VertexList = new List<ImportedVertex>((int) submesh.vertexCount);

                bool vertexColours = mesh.m_Colors != null && (mesh.m_Colors.Length == mesh.m_VertexCount * 3 || mesh.m_Colors.Length == mesh.m_VertexCount * 4);

                for (uint j = mesh.m_SubMeshes[i].firstVertex; j < mesh.m_SubMeshes[i].firstVertex + mesh.m_SubMeshes[i].vertexCount; j++)
                {
                    ImportedVertex iVertex = vertexColours ? new ImportedVertexWithColour() : new ImportedVertex();

                    //Vertices
                    var c = 3;

                    if (mesh.m_Vertices.Length == mesh.m_VertexCount * 4)
                    {
                        c = 4;
                    }

                    iVertex.Position = new Vector3(-mesh.m_Vertices[j * c], mesh.m_Vertices[j * c + 1], mesh.m_Vertices[j * c + 2]);

                    //Normals
                    if (mesh.m_Normals?.Length > 0)
                    {
                        if (mesh.m_Normals.Length == mesh.m_VertexCount * 3)
                        {
                            c = 3;
                        }
                        else if (mesh.m_Normals.Length == mesh.m_VertexCount * 4)
                        {
                            c = 4;
                        }

                        iVertex.Normal = new Vector3(-mesh.m_Normals[j * c], mesh.m_Normals[j * c + 1], mesh.m_Normals[j * c + 2]);
                    }

                    //Colors
                    if (vertexColours)
                    {
                        if (mesh.m_Colors.Length == mesh.m_VertexCount * 3)
                        {
                            ((ImportedVertexWithColour) iVertex).Colour = new Color4(mesh.m_Colors[j * 3], mesh.m_Colors[j * 3 + 1], mesh.m_Colors[j * 3 + 2], 1.0f);
                        }
                        else
                        {
                            ((ImportedVertexWithColour) iVertex).Colour = new Color4(mesh.m_Colors[j * 4], mesh.m_Colors[j * 4 + 1], mesh.m_Colors[j * 4 + 2], mesh.m_Colors[j * 4 + 3]);
                        }
                    }

                    //UV
                    if (mesh.m_UV1 != null && mesh.m_UV1.Length == mesh.m_VertexCount * 2)
                    {
                        iVertex.UV = new[]
                        {
                            mesh.m_UV1[j * 2],
                            -mesh.m_UV1[j * 2 + 1]
                        };
                    }
                    else if (mesh.m_UV2 != null && mesh.m_UV2.Length == mesh.m_VertexCount * 2)
                    {
                        iVertex.UV = new[]
                        {
                            mesh.m_UV2[j * 2],
                            -mesh.m_UV2[j * 2 + 1]
                        };
                    }

                    //Tangent
                    if (mesh.m_Tangents != null && mesh.m_Tangents.Length == mesh.m_VertexCount * 4)
                    {
                        iVertex.Tangent = new Vector4(-mesh.m_Tangents[j * 4], mesh.m_Tangents[j * 4 + 1], mesh.m_Tangents[j * 4 + 2], mesh.m_Tangents[j * 4 + 3]);
                    }

                    //BoneInfluence
                    if (mesh.m_Skin?.Length > 0)
                    {
                        List<Mesh.BoneInfluence> inf = mesh.m_Skin[j];

                        iVertex.BoneIndices = new byte[inf.Count];
                        iVertex.Weights = new float[inf.Count];

                        for (var k = 0; k < inf.Count; k++)
                        {
                            iVertex.BoneIndices[k] = (byte) inf[k].boneIndex;
                            iVertex.Weights[k] = inf[k].weight;
                        }
                    }

                    iSubmesh.VertexList.Add(iVertex);
                }

                //Face
                iSubmesh.FaceList = new List<ImportedFace>(numFaces);

                int end = firstFace + numFaces;

                for (int f = firstFace; f < end; f++)
                {
                    var face = new ImportedFace();

                    face.VertexIndices = new int[3];
                    face.VertexIndices[0] = (int) (mesh.m_Indices[f * 3 + 2] - submesh.firstVertex);
                    face.VertexIndices[1] = (int) (mesh.m_Indices[f * 3 + 1] - submesh.firstVertex);
                    face.VertexIndices[2] = (int) (mesh.m_Indices[f * 3] - submesh.firstVertex);

                    iSubmesh.FaceList.Add(face);
                }

                firstFace = end;

                iMesh.SubmeshList.Add(iSubmesh);
            }

            //Bone
            iMesh.BoneList = new List<ImportedBone>();

            if (mesh.m_BindPose?.Length > 0 && mesh.m_BoneNameHashes?.Length > 0 && mesh.m_BindPose.Length == mesh.m_BoneNameHashes.Length)
            {
                for (var i = 0; i < mesh.m_BindPose.Length; i++)
                {
                    var bone = new ImportedBone();

                    uint boneHash = mesh.m_BoneNameHashes[i];

                    bone.Name = this.GetNameFromBonePathHashes(boneHash);

                    if (string.IsNullOrEmpty(bone.Name))
                    {
                        bone.Name = this.avatar?.FindBoneName(boneHash);
                    }

                    if (string.IsNullOrEmpty(bone.Name))
                    {
                        //throw new Exception("A Bone could neither be found by hash in Avatar nor by index in SkinnedMeshRenderer.");
                        continue;
                    }

                    var om = new float[4, 4];
                    float[,] m = mesh.m_BindPose[i];
                    om[0, 0] = m[0, 0];
                    om[0, 1] = -m[1, 0];
                    om[0, 2] = -m[2, 0];
                    om[0, 3] = m[3, 0];
                    om[1, 0] = -m[0, 1];
                    om[1, 1] = m[1, 1];
                    om[1, 2] = m[2, 1];
                    om[1, 3] = m[3, 1];
                    om[2, 0] = -m[0, 2];
                    om[2, 1] = m[1, 2];
                    om[2, 2] = m[2, 2];
                    om[2, 3] = m[3, 2];
                    om[3, 0] = -m[0, 3];
                    om[3, 1] = m[1, 3];
                    om[3, 2] = m[2, 3];
                    om[3, 3] = m[3, 3];

                    bone.Matrix = om;

                    iMesh.BoneList.Add(bone);
                }
            }

            if (meshR is SkinnedMeshRenderer sMesh)
            {
                //Bone for 4.3 down and other
                if (iMesh.BoneList.Count == 0)
                {
                    for (var i = 0; i < sMesh.m_Bones.Length; i++)
                    {
                        var bone = new ImportedBone();

                        if (sMesh.m_Bones[i].TryGetTransform(out Transform m_Transform))
                        {
                            if (m_Transform.m_GameObject.TryGetGameObject(out GameObject m_GameObject))
                            {
                                bone.Name = m_GameObject.m_Name;
                            }
                        }

                        if (string.IsNullOrEmpty(bone.Name))
                        {
                            uint boneHash = mesh.m_BoneNameHashes[i];
                            bone.Name = this.GetNameFromBonePathHashes(boneHash);

                            if (string.IsNullOrEmpty(bone.Name))
                            {
                                bone.Name = this.avatar?.FindBoneName(boneHash);
                            }

                            if (string.IsNullOrEmpty(bone.Name))
                            {
                                //throw new Exception("A Bone could neither be found by hash in Avatar nor by index in SkinnedMeshRenderer.");
                                continue;
                            }
                        }

                        var om = new float[4, 4];
                        float[,] m = mesh.m_BindPose[i];
                        om[0, 0] = m[0, 0];
                        om[0, 1] = -m[1, 0];
                        om[0, 2] = -m[2, 0];
                        om[0, 3] = m[3, 0];
                        om[1, 0] = -m[0, 1];
                        om[1, 1] = m[1, 1];
                        om[1, 2] = m[2, 1];
                        om[1, 3] = m[3, 1];
                        om[2, 0] = -m[0, 2];
                        om[2, 1] = m[1, 2];
                        om[2, 2] = m[2, 2];
                        om[2, 3] = m[3, 2];
                        om[3, 0] = -m[0, 3];
                        om[3, 1] = m[1, 3];
                        om[3, 2] = m[2, 3];
                        om[3, 3] = m[3, 3];

                        bone.Matrix = om;

                        iMesh.BoneList.Add(bone);
                    }
                }

                //Morphs
                if (mesh.m_Shapes != null)
                {
                    foreach (Mesh.BlendShapeData.MeshBlendShapeChannel channel in mesh.m_Shapes.channels)
                    {
                        this.morphChannelInfo[channel.nameHash] = channel.name;
                    }
                    if (mesh.m_Shapes.shapes.Count > 0)
                    {
                        ImportedMorph morph = null;
                        var lastGroup = "";
                        for (var i = 0; i < mesh.m_Shapes.channels.Count; i++)
                        {
                            string group = BlendShapeNameGroup(mesh, i);
                            if (group != lastGroup)
                            {
                                morph = new ImportedMorph();
                                this.MorphList.Add(morph);
                                morph.Name = iMesh.Name;
                                morph.ClipName = group;
                                morph.Channels = new List<Tuple<float, int, int>>(mesh.m_Shapes.channels.Count);
                                morph.KeyframeList = new List<ImportedMorphKeyframe>(mesh.m_Shapes.shapes.Count);
                                lastGroup = group;
                            }

                            morph.Channels.Add(new Tuple<float, int, int>(i < sMesh.m_BlendShapeWeights.Count ? sMesh.m_BlendShapeWeights[i] : 0f, morph.KeyframeList.Count, mesh.m_Shapes.channels[i].frameCount));
                            for (var frameIdx = 0; frameIdx < mesh.m_Shapes.channels[i].frameCount; frameIdx++)
                            {
                                var keyframe = new ImportedMorphKeyframe();
                                keyframe.Name = BlendShapeNameExtension(mesh, i) + "_" + frameIdx;
                                int shapeIdx = mesh.m_Shapes.channels[i].frameIndex + frameIdx;
                                keyframe.VertexList = new List<ImportedVertex>((int) mesh.m_Shapes.shapes[shapeIdx].vertexCount);
                                keyframe.MorphedVertexIndices = new List<ushort>((int) mesh.m_Shapes.shapes[shapeIdx].vertexCount);
                                keyframe.Weight = shapeIdx < mesh.m_Shapes.fullWeights.Count ? mesh.m_Shapes.fullWeights[shapeIdx] : 100f;
                                var lastVertIndex = (int) (mesh.m_Shapes.shapes[shapeIdx].firstVertex + mesh.m_Shapes.shapes[shapeIdx].vertexCount);
                                for (var j = (int) mesh.m_Shapes.shapes[shapeIdx].firstVertex; j < lastVertIndex; j++)
                                {
                                    Mesh.BlendShapeData.BlendShapeVertex morphVert = mesh.m_Shapes.vertices[j];
                                    ImportedVertex vert = GetSourceVertex(iMesh.SubmeshList, (int) morphVert.index);
                                    var destVert = new ImportedVertex();
                                    Vector3 morphPos = morphVert.vertex;
                                    morphPos.X *= -1;
                                    destVert.Position = vert.Position + morphPos;
                                    Vector3 morphNormal = morphVert.normal;
                                    morphNormal.X *= -1;
                                    destVert.Normal = morphNormal;
                                    var morphTangent = new Vector4(morphVert.tangent, 0);
                                    morphTangent.X *= -1;
                                    destVert.Tangent = morphTangent;
                                    keyframe.VertexList.Add(destVert);
                                    keyframe.MorphedVertexIndices.Add((ushort) morphVert.index);
                                }

                                morph.KeyframeList.Add(keyframe);
                            }
                        }
                    }
                }
            }

            //TODO
            if (combine)
            {
                meshR.m_GameObject.TryGetGameObject(out GameObject m_GameObject);
                ImportedFrame frame = ImportedHelpers.FindChildOrRoot(m_GameObject.m_Name, this.FrameList[0]);
                if (frame?.Parent != null)
                {
                    ImportedFrame parent = frame;
                    while (true)
                    {
                        if (parent.Parent != null)
                        {
                            parent = parent.Parent;
                        }
                        else
                        {
                            frame.LocalRotation = parent.LocalRotation;
                            frame.LocalScale = parent.LocalScale;
                            frame.LocalPosition = parent.LocalPosition;
                            break;
                        }
                    }
                }
            }

            this.MeshList.Add(iMesh);
        }

        private Mesh GetMesh(Renderer meshR)
        {
            if (meshR is SkinnedMeshRenderer sMesh)
            {
                if (sMesh.m_Mesh.TryGet(out ObjectReader MeshPD))
                {
                    return new Mesh(MeshPD);
                }
            }
            else
            {
                meshR.m_GameObject.TryGetGameObject(out GameObject m_GameObject);
                foreach (PPtr m_Component in m_GameObject.m_Components)
                {
                    if (m_Component.TryGet(out ObjectReader componentObject))
                    {
                        if (componentObject.type == ClassIDType.MeshFilter)
                        {
                            var m_MeshFilter = new MeshFilter(componentObject);
                            if (m_MeshFilter.m_Mesh.TryGet(out ObjectReader meshObject))
                            {
                                return new Mesh(meshObject);
                            }
                        }
                    }
                }
            }

            return null;
        }

        private string GetMeshPath(Transform meshTransform)
        {
            meshTransform.m_GameObject.TryGetGameObject(out GameObject m_GameObject);
            ImportedFrame curFrame = ImportedHelpers.FindChildOrRoot(m_GameObject.m_Name, this.FrameList[0]) ?? ImportedHelpers.FindFrame(m_GameObject.m_Name, this.FrameList[0]);
            string path = curFrame.Name;
            while (curFrame.Parent != null)
            {
                curFrame = curFrame.Parent;
                path = curFrame.Name + "/" + path;
            }

            return path;
        }

        private string GetTransformPath(Transform transform)
        {
            transform.m_GameObject.TryGetGameObject(out GameObject m_GameObject);
            if (transform.m_Father.TryGetTransform(out Transform father))
            {
                return this.GetTransformPath(father) + "/" + m_GameObject.m_Name;
            }

            return m_GameObject.m_Name;
        }

        private ImportedMaterial ConvertMaterial(Material mat)
        {
            ImportedMaterial iMat;
            if (mat != null)
            {
                iMat = ImportedHelpers.FindMaterial(mat.m_Name, this.MaterialList);
                if (iMat != null)
                {
                    return iMat;
                }
                iMat = new ImportedMaterial();
                iMat.Name = mat.m_Name;
                foreach (strColorPair col in mat.m_Colors)
                {
                    var color = new Color4(col.second[0], col.second[1], col.second[2], col.second[3]);
                    switch (col.first)
                    {
                        case "_Color":
                            iMat.Diffuse = color;
                            break;
                        case "_SColor":
                            iMat.Ambient = color;
                            break;
                        case "_EmissionColor":
                            iMat.Emissive = color;
                            break;
                        case "_SpecColor":
                            iMat.Specular = color;
                            break;
                        case "_RimColor":
                        case "_OutlineColor":
                        case "_ShadowColor":
                            break;
                    }
                }

                foreach (strFloatPair flt in mat.m_Floats)
                {
                    switch (flt.first)
                    {
                        case "_Shininess":
                            iMat.Power = flt.second;
                            break;
                        case "_RimPower":
                        case "_Outline":
                            break;
                    }
                }

                //textures
                iMat.Textures = new string[5];
                iMat.TexOffsets = new Vector2[5];
                iMat.TexScales = new Vector2[5];
                foreach (TexEnv texEnv in mat.m_TexEnvs)
                {
                    Texture2D tex2D = null;
                    if (texEnv.m_Texture.TryGet(out ObjectReader TexturePD) && TexturePD.type == ClassIDType.Texture2D) //TODO other Texture
                    {
                        tex2D = new Texture2D(TexturePD, true);
                    }

                    if (tex2D == null)
                    {
                        continue;
                    }
                    int dest = texEnv.name == "_MainTex" ? 0 : texEnv.name == "_BumpMap" ? 4 : texEnv.name.Contains("Spec") ? 2 : texEnv.name.Contains("Norm") ? 3 : -1;
                    if (dest < 0 || iMat.Textures[dest] != null)
                    {
                        continue;
                    }
                    iMat.Textures[dest] = TexturePD.exportName + ".png";
                    iMat.TexOffsets[dest] = new Vector2(texEnv.m_Offset[0], texEnv.m_Offset[1]);
                    iMat.TexScales[dest] = new Vector2(texEnv.m_Scale[0], texEnv.m_Scale[1]);
                    this.ConvertTexture2D(tex2D, iMat.Textures[dest]);
                }

                this.MaterialList.Add(iMat);
            }
            else
            {
                iMat = new ImportedMaterial();
            }
            return iMat;
        }

        private void ConvertTexture2D(Texture2D tex2D, string name)
        {
            ImportedTexture iTex = ImportedHelpers.FindTexture(name, this.TextureList);
            if (iTex != null)
            {
                return;
            }

            using (var memStream = new MemoryStream())
            {
                Bitmap bitmap = new Texture2DConverter(tex2D).ConvertToBitmap(true);

                if (bitmap == null)
                {
                    return;
                }

                bitmap.Save(memStream, ImageFormat.Png);

                memStream.Position = 0;

                iTex = new ImportedTexture(memStream, name);
                this.TextureList.Add(iTex);

                bitmap.Dispose();
            }
        }

        private void ConvertAnimations()
        {
            foreach (ObjectReader assetPreloadData in this.animationClipHashSet)
            {
                var animationClip = new AnimationClip(assetPreloadData);
                var iAnim = new ImportedKeyframedAnimation();

                this.AnimationList.Add(iAnim);

                iAnim.Name = animationClip.m_Name;
                iAnim.TrackList = new List<ImportedAnimationKeyframedTrack>();

                if (animationClip.m_Legacy)
                {
                    foreach (CompressedAnimationCurve m_CompressedRotationCurve in animationClip.m_CompressedRotationCurves)
                    {
                        string path = m_CompressedRotationCurve.m_Path;
                        string boneName = path.Substring(path.LastIndexOf('/') + 1);

                        ImportedAnimationKeyframedTrack track = iAnim.FindTrack(boneName);

                        uint numKeys = m_CompressedRotationCurve.m_Times.m_NumItems;
                        int[] data = m_CompressedRotationCurve.m_Times.UnpackInts();

                        var times = new float[numKeys];
                        var t = 0;

                        for (var i = 0; i < numKeys; i++)
                        {
                            t += data[i];
                            times[i] = t * 0.01f;
                        }
                        Quaternion[] quats = m_CompressedRotationCurve.m_Values.UnpackQuats();

                        for (var i = 0; i < numKeys; i++)
                        {
                            Quaternion quat = quats[i];
                            Vector3 value = Fbx.QuaternionToEuler(new Quaternion(quat.X, -quat.Y, -quat.Z, quat.W));
                            track.Rotations.Add(new ImportedKeyframe<Vector3>(times[i], value));
                        }
                    }

                    foreach (QuaternionCurve m_RotationCurve in animationClip.m_RotationCurves)
                    {
                        string path = m_RotationCurve.path;
                        string boneName = path.Substring(path.LastIndexOf('/') + 1);

                        ImportedAnimationKeyframedTrack track = iAnim.FindTrack(boneName);

                        foreach (Keyframe<Quaternion> m_Curve in m_RotationCurve.curve.m_Curve)
                        {
                            Vector3 value = Fbx.QuaternionToEuler(new Quaternion(m_Curve.value.X, -m_Curve.value.Y, -m_Curve.value.Z, m_Curve.value.W));
                            track.Rotations.Add(new ImportedKeyframe<Vector3>(m_Curve.time, value));
                        }
                    }

                    foreach (Vector3Curve m_PositionCurve in animationClip.m_PositionCurves)
                    {
                        string path = m_PositionCurve.path;
                        string boneName = path.Substring(path.LastIndexOf('/') + 1);

                        ImportedAnimationKeyframedTrack track = iAnim.FindTrack(boneName);

                        foreach (Keyframe<Vector3> m_Curve in m_PositionCurve.curve.m_Curve)
                        {
                            track.Translations.Add(new ImportedKeyframe<Vector3>(m_Curve.time, new Vector3(-m_Curve.value.X, m_Curve.value.Y, m_Curve.value.Z)));
                        }
                    }

                    foreach (Vector3Curve m_ScaleCurve in animationClip.m_ScaleCurves)
                    {
                        string path = m_ScaleCurve.path;
                        string boneName = path.Substring(path.LastIndexOf('/') + 1);

                        ImportedAnimationKeyframedTrack track = iAnim.FindTrack(boneName);

                        foreach (Keyframe<Vector3> m_Curve in m_ScaleCurve.curve.m_Curve)
                        {
                            track.Scalings.Add(new ImportedKeyframe<Vector3>(m_Curve.time, new Vector3(m_Curve.value.X, m_Curve.value.Y, m_Curve.value.Z)));
                        }
                    }

                    if (animationClip.m_EulerCurves != null)
                    {
                        foreach (Vector3Curve m_EulerCurve in animationClip.m_EulerCurves)
                        {
                            string path = m_EulerCurve.path;
                            string boneName = path.Substring(path.LastIndexOf('/') + 1);

                            ImportedAnimationKeyframedTrack track = iAnim.FindTrack(boneName);

                            foreach (Keyframe<Vector3> m_Curve in m_EulerCurve.curve.m_Curve)
                            {
                                track.Rotations.Add(new ImportedKeyframe<Vector3>(m_Curve.time, new Vector3(m_Curve.value.X, -m_Curve.value.Y, -m_Curve.value.Z)));
                            }
                        }
                    }

                    foreach (FloatCurve m_FloatCurve in animationClip.m_FloatCurves)
                    {
                        string path = m_FloatCurve.path;
                        string boneName = path.Substring(path.LastIndexOf('/') + 1);

                        ImportedAnimationKeyframedTrack track = iAnim.FindTrack(boneName);

                        foreach (Keyframe<float> m_Curve in m_FloatCurve.curve.m_Curve)
                        {
                            track.Curve.Add(new ImportedKeyframe<float>(m_Curve.time, m_Curve.value));
                        }
                    }
                }
                else
                {
                    Clip m_Clip = animationClip.m_MuscleClip.m_Clip;
                    List<StreamedClip.StreamedFrame> streamedFrames = m_Clip.m_StreamedClip.ReadData();
                    AnimationClipBindingConstant m_ClipBindingConstant = animationClip.m_ClipBindingConstant;

                    for (var frameIndex = 1; frameIndex < streamedFrames.Count - 1; frameIndex++)
                    {
                        StreamedClip.StreamedFrame frame = streamedFrames[frameIndex];
                        float[] streamedValues = frame.keyList.Select(x => x.value).ToArray();

                        for (var curveIndex = 0; curveIndex < frame.keyList.Count;)
                        {
                            this.ReadCurveData(iAnim, m_ClipBindingConstant, frame.keyList[curveIndex].index, frame.time, streamedValues, 0, ref curveIndex);
                        }
                    }

                    DenseClip m_DenseClip = m_Clip.m_DenseClip;
                    uint streamCount = m_Clip.m_StreamedClip.curveCount;

                    for (var frameIndex = 0; frameIndex < m_DenseClip.m_FrameCount; frameIndex++)
                    {
                        float time = m_DenseClip.m_BeginTime + frameIndex / m_DenseClip.m_SampleRate;
                        long frameOffset = frameIndex * m_DenseClip.m_CurveCount;

                        for (var curveIndex = 0; curveIndex < m_DenseClip.m_CurveCount;)
                        {
                            long index = streamCount + curveIndex;
                            this.ReadCurveData(iAnim, m_ClipBindingConstant, (int) index, time, m_DenseClip.m_SampleArray, (int) frameOffset, ref curveIndex);
                        }
                    }

                    if (m_Clip.m_ConstantClip != null)
                    {
                        ConstantClip m_ConstantClip = m_Clip.m_ConstantClip;
                        uint denseCount = m_Clip.m_DenseClip.m_CurveCount;
                        var time2 = 0.0f;

                        for (var i = 0; i < 2; i++)
                        {
                            for (var curveIndex = 0; curveIndex < m_ConstantClip.data.Length;)
                            {
                                long index = streamCount + denseCount + curveIndex;
                                this.ReadCurveData(iAnim, m_ClipBindingConstant, (int) index, time2, m_ConstantClip.data, 0, ref curveIndex);
                            }

                            time2 = animationClip.m_MuscleClip.m_StopTime;
                        }
                    }
                }
            }
        }

        private void ReadCurveData(ImportedKeyframedAnimation iAnim, AnimationClipBindingConstant m_ClipBindingConstant, int index, float time, float[] data, int offset, ref int curveIndex)
        {
            GenericBinding binding = m_ClipBindingConstant.FindBinding(index);
            if (binding.path == 0)
            {
                curveIndex++;
                return;
            }

            string boneName = this.GetNameFromHashes(binding.path, binding.attribute);
            ImportedAnimationKeyframedTrack track = iAnim.FindTrack(boneName);

            switch (binding.attribute)
            {
                case 1:
                    track.Translations.Add(new ImportedKeyframe<Vector3>(time, new Vector3(-data[curveIndex++ + offset], data[curveIndex++ + offset], data[curveIndex++ + offset])));
                    break;
                case 2:
                    Vector3 value = Fbx.QuaternionToEuler(new Quaternion(data[curveIndex++ + offset], -data[curveIndex++ + offset], -data[curveIndex++ + offset], data[curveIndex++ + offset]));
                    track.Rotations.Add(new ImportedKeyframe<Vector3>(time, value));
                    break;
                case 3:
                    track.Scalings.Add(new ImportedKeyframe<Vector3>(time, new Vector3(data[curveIndex++ + offset], data[curveIndex++ + offset], data[curveIndex++ + offset])));
                    break;
                case 4:
                    track.Rotations.Add(new ImportedKeyframe<Vector3>(time, new Vector3(data[curveIndex++ + offset], -data[curveIndex++ + offset], -data[curveIndex++ + offset])));
                    break;
                default:
                    track.Curve.Add(new ImportedKeyframe<float>(time, data[curveIndex++]));
                    break;
            }
        }

        private string GetNameFromHashes(uint path, uint attribute)
        {
            string boneName = this.GetNameFromBonePathHashes(path);

            if (string.IsNullOrEmpty(boneName))
            {
                boneName = this.avatar?.FindBoneName(path);
            }

            if (string.IsNullOrEmpty(boneName))
            {
                boneName = "unknown " + path;
            }

            if (attribute > 4)
            {
                if (this.morphChannelInfo.TryGetValue(attribute, out string morphChannel))
                {
                    return boneName + "." + morphChannel;
                }

                return boneName + ".unknown_morphChannel " + attribute;
            }

            return boneName;
        }

        private string GetNameFromBonePathHashes(uint path)
        {
            if (this.bonePathHash.TryGetValue(path, out string boneName))
            {
                boneName = boneName.Substring(boneName.LastIndexOf('/') + 1);
            }

            return boneName;
        }

        private static string BlendShapeNameGroup(Mesh mesh, int index)
        {
            string name = mesh.m_Shapes.channels[index].name;

            int dotPos = name.IndexOf('.');

            if (dotPos >= 0)
            {
                return name.Substring(0, dotPos);
            }

            return "Ungrouped";
        }

        private static string BlendShapeNameExtension(Mesh mesh, int index)
        {
            string name = mesh.m_Shapes.channels[index].name;

            int dotPos = name.IndexOf('.');

            if (dotPos >= 0)
            {
                return name.Substring(dotPos + 1);
            }

            return name;
        }

        private static ImportedVertex GetSourceVertex(List<ImportedSubmesh> submeshList, int morphVertIndex)
        {
            foreach (ImportedSubmesh submesh in submeshList)
            {
                List<ImportedVertex> vertList = submesh.VertexList;
                if (morphVertIndex < vertList.Count)
                {
                    return vertList[morphVertIndex];
                }
                morphVertIndex -= vertList.Count;
            }
            return null;
        }

        private void CreateBonePathHash(Transform m_Transform)
        {
            string name = this.GetTransformPath(m_Transform);
            var crc = new SevenZip.CRC();
            byte[] bytes = Encoding.UTF8.GetBytes(name);
            crc.Update(bytes, 0, (uint) bytes.Length);
            this.bonePathHash[crc.GetDigest()] = name;
            int index;
            while ((index = name.IndexOf("/", StringComparison.Ordinal)) >= 0)
            {
                name = name.Substring(index + 1);
                crc = new SevenZip.CRC();
                bytes = Encoding.UTF8.GetBytes(name);
                crc.Update(bytes, 0, (uint) bytes.Length);
                this.bonePathHash[crc.GetDigest()] = name;
            }
            foreach (PPtr pptr in m_Transform.m_Children)
            {
                if (pptr.TryGetTransform(out Transform child))
                {
                    this.CreateBonePathHash(child);
                }
            }
        }

        private void DeoptimizeTransformHierarchy()
        {
            if (this.avatar == null)
            {
                throw new Exception("Transform hierarchy has been optimized, but can't find Avatar to deoptimize.");
            }
            // 1. Figure out the skeletonPaths from the unstripped avatar
            var skeletonPaths = new List<string>();
            foreach (uint id in this.avatar.m_Avatar.m_AvatarSkeleton.m_ID)
            {
                string path = this.avatar.FindBonePath(id);
                skeletonPaths.Add(path);
            }
            // 2. Restore the original transform hierarchy
            // Prerequisite: skeletonPaths follow pre-order traversal
            ImportedFrame rootFrame = this.FrameList[0];
            rootFrame.ClearChild();
            for (var i = 1; i < skeletonPaths.Count; i++) // start from 1, skip the root transform because it will always be there.
            {
                string path = skeletonPaths[i];
                string[] strs = path.Split('/');
                string transformName;
                ImportedFrame parentFrame;
                if (strs.Length == 1)
                {
                    transformName = path;
                    parentFrame = rootFrame;
                }
                else
                {
                    transformName = strs.Last();
                    string parentFrameName = strs[strs.Length - 2];
                    parentFrame = ImportedHelpers.FindChildOrRoot(parentFrameName, rootFrame);
                }

                SkeletonPose skeletonPose = this.avatar.m_Avatar.m_DefaultPose;
                xform xform = skeletonPose.m_X[i];
                if (!(xform.t is Vector3 t))
                {
                    var v4 = (Vector4) xform.t;
                    t = (Vector3) v4;
                }
                if (!(xform.s is Vector3 s))
                {
                    var v4 = (Vector4) xform.s;
                    s = (Vector3) v4;
                }
                ImportedFrame frame = this.ConvertFrame(t, xform.q, s, transformName);
                parentFrame.AddChild(frame);
            }
        }

        private static float[] QuatToEuler(float[] q)
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