using SharpDX;
using System.Collections.Generic;
using AssetStudio.Extensions;
using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public class Node
    {
        public int m_ParentId;
        public int m_AxesId;

        public Node(ObjectReader reader)
        {
            this.m_ParentId = reader.ReadInt32();
            this.m_AxesId = reader.ReadInt32();
        }
    }

    public class Limit
    {
        public object m_Min;
        public object m_Max;

        public Limit(ObjectReader reader)
        {
            int[] version = reader.version;
            if (version[0] > 5 || version[0] == 5 && version[1] >= 4) //5.4 and up
            {
                this.m_Min = reader.ReadVector3();
                this.m_Max = reader.ReadVector3();
            }
            else
            {
                this.m_Min = reader.ReadVector4();
                this.m_Max = reader.ReadVector4();
            }
        }
    }

    public class Axes
    {
        public Vector4 m_PreQ;
        public Vector4 m_PostQ;
        public object m_Sgn;
        public Limit m_Limit;
        public float m_Length;
        public uint m_Type;

        public Axes(ObjectReader reader)
        {
            int[] version = reader.version;
            this.m_PreQ = reader.ReadVector4();
            this.m_PostQ = reader.ReadVector4();
            if (version[0] > 5 || version[0] == 5 && version[1] >= 4) //5.4 and up
            {
                this.m_Sgn = reader.ReadVector3();
            }
            else
            {
                this.m_Sgn = reader.ReadVector4();
            }
            this.m_Limit = new Limit(reader);
            this.m_Length = reader.ReadSingle();
            this.m_Type = reader.ReadUInt32();
        }
    }

    public class Skeleton
    {
        public List<Node> m_Node;
        public List<uint> m_ID;
        public List<Axes> m_AxesArray;

        public Skeleton(ObjectReader reader)
        {
            int numNodes = reader.ReadInt32();
            this.m_Node = new List<Node>(numNodes);
            for (var i = 0; i < numNodes; i++)
            {
                this.m_Node.Add(new Node(reader));
            }

            int numIDs = reader.ReadInt32();
            this.m_ID = new List<uint>(numIDs);
            for (var i = 0; i < numIDs; i++)
            {
                this.m_ID.Add(reader.ReadUInt32());
            }

            int numAxes = reader.ReadInt32();
            this.m_AxesArray = new List<Axes>(numAxes);
            for (var i = 0; i < numAxes; i++)
            {
                this.m_AxesArray.Add(new Axes(reader));
            }
        }
    }

    public class SkeletonPose
    {
        public List<xform> m_X;

        public SkeletonPose()
        {
            this.m_X = new List<xform>();
        }

        public SkeletonPose(ObjectReader reader)
        {
            int numXforms = reader.ReadInt32();
            this.m_X = new List<xform>(numXforms);
            for (var i = 0; i < numXforms; i++)
            {
                this.m_X.Add(new xform(reader));
            }
        }
    }

    public class Hand
    {
        public List<int> m_HandBoneIndex;

        public Hand(ObjectReader reader)
        {
            int numIndexes = reader.ReadInt32();
            this.m_HandBoneIndex = new List<int>(numIndexes);
            for (var i = 0; i < numIndexes; i++)
            {
                this.m_HandBoneIndex.Add(reader.ReadInt32());
            }
        }
    }

    public class Handle
    {
        public xform m_X;
        public uint m_ParentHumanIndex;
        public uint m_ID;

        public Handle(ObjectReader reader)
        {
            this.m_X = new xform(reader);
            this.m_ParentHumanIndex = reader.ReadUInt32();
            this.m_ID = reader.ReadUInt32();
        }
    }

    public class Collider
    {
        public xform m_X;
        public uint m_Type;
        public uint m_XMotionType;
        public uint m_YMotionType;
        public uint m_ZMotionType;
        public float m_MinLimitX;
        public float m_MaxLimitX;
        public float m_MaxLimitY;
        public float m_MaxLimitZ;

        public Collider(ObjectReader reader)
        {
            this.m_X = new xform(reader);
            this.m_Type = reader.ReadUInt32();
            this.m_XMotionType = reader.ReadUInt32();
            this.m_YMotionType = reader.ReadUInt32();
            this.m_ZMotionType = reader.ReadUInt32();
            this.m_MinLimitX = reader.ReadSingle();
            this.m_MaxLimitX = reader.ReadSingle();
            this.m_MaxLimitY = reader.ReadSingle();
            this.m_MaxLimitZ = reader.ReadSingle();
        }
    }

    public class Human
    {
        public xform m_RootX;
        public Skeleton m_Skeleton;
        public SkeletonPose m_SkeletonPose;
        public Hand m_LeftHand;
        public Hand m_RightHand;
        public List<Handle> m_Handles;
        public List<Collider> m_ColliderArray;
        public List<int> m_HumanBoneIndex;
        public List<float> m_HumanBoneMass;
        public List<int> m_ColliderIndex;
        public float m_Scale;
        public float m_ArmTwist;
        public float m_ForeArmTwist;
        public float m_UpperLegTwist;
        public float m_LegTwist;
        public float m_ArmStretch;
        public float m_LegStretch;
        public float m_FeetSpacing;
        public bool m_HasLeftHand;
        public bool m_HasRightHand;
        public bool m_HasTDoF;

        public Human(ObjectReader reader)
        {
            int[] version = reader.version;
            this.m_RootX = new xform(reader);
            this.m_Skeleton = new Skeleton(reader);
            this.m_SkeletonPose = new SkeletonPose(reader);
            this.m_LeftHand = new Hand(reader);
            this.m_RightHand = new Hand(reader);

            if (version[0] < 2018 || version[0] == 2018 && version[1] < 2) //2018.2 down
            {
                int numHandles = reader.ReadInt32();
                this.m_Handles = new List<Handle>(numHandles);
                for (var i = 0; i < numHandles; i++)
                {
                    this.m_Handles.Add(new Handle(reader));
                }

                int numColliders = reader.ReadInt32();
                this.m_ColliderArray = new List<Collider>(numColliders);
                for (var i = 0; i < numColliders; i++)
                {
                    this.m_ColliderArray.Add(new Collider(reader));
                }
            }

            int numIndexes = reader.ReadInt32();
            this.m_HumanBoneIndex = new List<int>(numIndexes);
            for (var i = 0; i < numIndexes; i++)
            {
                this.m_HumanBoneIndex.Add(reader.ReadInt32());
            }

            int numMasses = reader.ReadInt32();
            this.m_HumanBoneMass = new List<float>(numMasses);
            for (var i = 0; i < numMasses; i++)
            {
                this.m_HumanBoneMass.Add(reader.ReadSingle());
            }

            if (version[0] < 2018 || version[0] == 2018 && version[1] < 2) //2018.2 down
            {
                int numColliderIndexes = reader.ReadInt32();
                this.m_ColliderIndex = new List<int>(numColliderIndexes);
                for (var i = 0; i < numColliderIndexes; i++)
                {
                    this.m_ColliderIndex.Add(reader.ReadInt32());
                }
            }

            this.m_Scale = reader.ReadSingle();
            this.m_ArmTwist = reader.ReadSingle();
            this.m_ForeArmTwist = reader.ReadSingle();
            this.m_UpperLegTwist = reader.ReadSingle();
            this.m_LegTwist = reader.ReadSingle();
            this.m_ArmStretch = reader.ReadSingle();
            this.m_LegStretch = reader.ReadSingle();
            this.m_FeetSpacing = reader.ReadSingle();
            this.m_HasLeftHand = reader.ReadBoolean();
            this.m_HasRightHand = reader.ReadBoolean();
            this.m_HasTDoF = reader.ReadBoolean();
            reader.AlignStream(4);
        }
    }

    public class AvatarConstant
    {
        public Skeleton m_AvatarSkeleton;
        public SkeletonPose m_AvatarSkeletonPose;
        public SkeletonPose m_DefaultPose;
        public List<uint> m_SkeletonNameIDArray;
        public Human m_Human;
        public List<int> m_HumanSkeletonIndexArray;
        public List<int> m_HumanSkeletonReverseIndexArray;
        public int m_RootMotionBoneIndex;
        public xform m_RootMotionBoneX;
        public Skeleton m_RootMotionSkeleton;
        public SkeletonPose m_RootMotionSkeletonPose;
        public List<int> m_RootMotionSkeletonIndexArray;

        public AvatarConstant(ObjectReader reader)
        {
            int[] version = reader.version;
            this.m_AvatarSkeleton = new Skeleton(reader);
            this.m_AvatarSkeletonPose = new SkeletonPose(reader);

            if (version[0] > 4 || version[0] == 4 && version[1] >= 3) //4.3 and up
            {
                this.m_DefaultPose = new SkeletonPose(reader);
                int numIDs = reader.ReadInt32();
                this.m_SkeletonNameIDArray = new List<uint>(numIDs);
                for (var i = 0; i < numIDs; i++)
                {
                    this.m_SkeletonNameIDArray.Add(reader.ReadUInt32());
                }
            }

            this.m_Human = new Human(reader);

            int numIndexes = reader.ReadInt32();
            this.m_HumanSkeletonIndexArray = new List<int>(numIndexes);
            for (var i = 0; i < numIndexes; i++)
            {
                this.m_HumanSkeletonIndexArray.Add(reader.ReadInt32());
            }

            if (version[0] > 4 || version[0] == 4 && version[1] >= 3) //4.3 and up
            {
                int numReverseIndexes = reader.ReadInt32();
                this.m_HumanSkeletonReverseIndexArray = new List<int>(numReverseIndexes);
                for (var i = 0; i < numReverseIndexes; i++)
                {
                    this.m_HumanSkeletonReverseIndexArray.Add(reader.ReadInt32());
                }
            }

            this.m_RootMotionBoneIndex = reader.ReadInt32();
            this.m_RootMotionBoneX = new xform(reader);

            if (version[0] > 4 || version[0] == 4 && version[1] >= 3) //4.3 and up
            {
                this.m_RootMotionSkeleton = new Skeleton(reader);
                this.m_RootMotionSkeletonPose = new SkeletonPose(reader);

                int numMotionIndexes = reader.ReadInt32();
                this.m_RootMotionSkeletonIndexArray = new List<int>(numMotionIndexes);
                for (var i = 0; i < numMotionIndexes; i++)
                {
                    this.m_RootMotionSkeletonIndexArray.Add(reader.ReadInt32());
                }
            }
        }
    }

    public sealed class Avatar : NamedObject
    {
        public uint m_AvatarSize;
        public AvatarConstant m_Avatar;
        public List<KeyValuePair<uint, string>> m_TOS;

        public Avatar(ObjectReader reader) : base(reader)
        {
            this.m_AvatarSize = reader.ReadUInt32();
            this.m_Avatar = new AvatarConstant(reader);

            int numTOS = reader.ReadInt32();
            this.m_TOS = new List<KeyValuePair<uint, string>>(numTOS);
            for (var i = 0; i < numTOS; i++)
            {
                this.m_TOS.Add(new KeyValuePair<uint, string>(reader.ReadUInt32(), reader.ReadAlignedString()));
            }
        }

        public string FindBoneName(uint hash)
        {
            foreach (KeyValuePair<uint, string> pair in this.m_TOS)
            {
                if (pair.Key == hash)
                {
                    return pair.Value.Substring(pair.Value.LastIndexOf('/') + 1);
                }
            }
            return null;
        }

        public string FindBonePath(uint hash)
        {
            return this.m_TOS.Find(pair => pair.Key == hash).Value;
        }
    }
}