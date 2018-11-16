using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AssetStudio.Extensions;
using AssetStudio.StudioClasses;
using SharpDX;

namespace AssetStudio
{
    public class HumanPoseMask
    {
        public uint word0;
        public uint word1;
        public uint word2;

        public HumanPoseMask(ObjectReader reader)
        {
            this.word0 = reader.ReadUInt32();
            this.word1 = reader.ReadUInt32();
            if (reader.version[0] >= 5) //5.0 and up
            {
                this.word2 = reader.ReadUInt32();
            }
        }
    }

    public class SkeletonMaskElement
    {
        public uint m_PathHash;
        public float m_Weight;

        public SkeletonMaskElement(ObjectReader reader)
        {
            this.m_PathHash = reader.ReadUInt32();
            this.m_Weight = reader.ReadSingle();
        }
    }

    public class SkeletonMask
    {
        public SkeletonMaskElement[] m_Data;

        public SkeletonMask(ObjectReader reader)
        {
            int numElements = reader.ReadInt32();
            this.m_Data = new SkeletonMaskElement[numElements];
            for (var i = 0; i < numElements; i++)
            {
                this.m_Data[i] = new SkeletonMaskElement(reader);
            }
        }
    }

    public class LayerConstant
    {
        public uint m_StateMachineIndex;
        public uint m_StateMachineMotionSetIndex;
        public HumanPoseMask m_BodyMask;
        public SkeletonMask m_SkeletonMask;
        public uint m_Binding;
        public int m_LayerBlendingMode;
        public float m_DefaultWeight;
        public bool m_IKPass;
        public bool m_SyncedLayerAffectsTiming;

        public LayerConstant(ObjectReader reader)
        {
            this.m_StateMachineIndex = reader.ReadUInt32();
            this.m_StateMachineMotionSetIndex = reader.ReadUInt32();
            this.m_BodyMask = new HumanPoseMask(reader);
            this.m_SkeletonMask = new SkeletonMask(reader);
            this.m_Binding = reader.ReadUInt32();
            this.m_LayerBlendingMode = reader.ReadInt32();
            this.m_DefaultWeight = reader.ReadSingle();
            this.m_IKPass = reader.ReadBoolean();
            this.m_SyncedLayerAffectsTiming = reader.ReadBoolean();
            reader.AlignStream();
        }
    }

    public class ConditionConstant
    {
        public uint m_ConditionMode;
        public uint m_EventID;
        public float m_EventThreshold;
        public float m_ExitTime;

        public ConditionConstant(ObjectReader reader)
        {
            this.m_ConditionMode = reader.ReadUInt32();
            this.m_EventID = reader.ReadUInt32();
            this.m_EventThreshold = reader.ReadSingle();
            this.m_ExitTime = reader.ReadSingle();
        }
    }

    public class TransitionConstant
    {
        public ConditionConstant[] m_ConditionConstantArray;
        public uint m_DestinationState;
        public uint m_FullPathID;
        public uint m_ID;
        public uint m_UserID;
        public float m_TransitionDuration;
        public float m_TransitionOffset;
        public float m_ExitTime;
        public bool m_HasExitTime;
        public bool m_HasFixedDuration;
        public int m_InterruptionSource;
        public bool m_OrderedInterruption;
        public bool m_Atomic;
        public bool m_CanTransitionToSelf;

        public TransitionConstant(ObjectReader reader)
        {
            int[] version = reader.version;
            int numConditions = reader.ReadInt32();
            this.m_ConditionConstantArray = new ConditionConstant[numConditions];
            for (var i = 0; i < numConditions; i++)
            {
                this.m_ConditionConstantArray[i] = new ConditionConstant(reader);
            }

            this.m_DestinationState = reader.ReadUInt32();
            if (version[0] >= 5) //5.0 and up
            {
                this.m_FullPathID = reader.ReadUInt32();
            }

            this.m_ID = reader.ReadUInt32();
            this.m_UserID = reader.ReadUInt32();
            this.m_TransitionDuration = reader.ReadSingle();
            this.m_TransitionOffset = reader.ReadSingle();
            if (version[0] >= 5) //5.0 and up
            {
                this.m_ExitTime = reader.ReadSingle();
                this.m_HasExitTime = reader.ReadBoolean();
                this.m_HasFixedDuration = reader.ReadBoolean();
                reader.AlignStream();
                this.m_InterruptionSource = reader.ReadInt32();
                this.m_OrderedInterruption = reader.ReadBoolean();
            }
            else
            {
                this.m_Atomic = reader.ReadBoolean();
            }

            this.m_CanTransitionToSelf = reader.ReadBoolean();
            reader.AlignStream();
        }
    }

    public class LeafInfoConstant
    {
        public uint[] m_IDArray;
        public uint m_IndexOffset;

        public LeafInfoConstant(ObjectReader reader)
        {
            this.m_IDArray = reader.ReadUInt32Array(reader.ReadInt32());
            this.m_IndexOffset = reader.ReadUInt32();
        }
    }

    public class MotionNeighborList
    {
        public uint[] m_NeighborArray;

        public MotionNeighborList(ObjectReader reader)
        {
            this.m_NeighborArray = reader.ReadUInt32Array(reader.ReadInt32());
        }
    }

    public class Blend2dDataConstant
    {
        public Vector2[] m_ChildPositionArray;
        public float[] m_ChildMagnitudeArray;
        public Vector2[] m_ChildPairVectorArray;
        public float[] m_ChildPairAvgMagInvArray;
        public MotionNeighborList[] m_ChildNeighborListArray;

        public Blend2dDataConstant(ObjectReader reader)
        {
            this.m_ChildPositionArray = reader.ReadVector2Array(reader.ReadInt32());
            this.m_ChildMagnitudeArray = reader.ReadSingleArray(reader.ReadInt32());
            this.m_ChildPairVectorArray = reader.ReadVector2Array(reader.ReadInt32());
            this.m_ChildPairAvgMagInvArray = reader.ReadSingleArray(reader.ReadInt32());

            int numNeighbours = reader.ReadInt32();
            this.m_ChildNeighborListArray = new MotionNeighborList[numNeighbours];
            for (var i = 0; i < numNeighbours; i++)
            {
                this.m_ChildNeighborListArray[i] = new MotionNeighborList(reader);
            }
        }
    }

    public class Blend1dDataConstant // wrong labeled
    {
        public float[] m_ChildThresholdArray;

        public Blend1dDataConstant(ObjectReader reader)
        {
            this.m_ChildThresholdArray = reader.ReadSingleArray(reader.ReadInt32());
        }
    }

    public class BlendDirectDataConstant
    {
        public uint[] m_ChildBlendEventIDArray;
        public bool m_NormalizedBlendValues;

        public BlendDirectDataConstant(ObjectReader reader)
        {
            this.m_ChildBlendEventIDArray = reader.ReadUInt32Array(reader.ReadInt32());
            this.m_NormalizedBlendValues = reader.ReadBoolean();
            reader.AlignStream();
        }
    }

    public class BlendTreeNodeConstant
    {
        public uint m_BlendType;
        public uint m_BlendEventID;
        public uint m_BlendEventYID;
        public uint[] m_ChildIndices;
        public Blend1dDataConstant m_Blend1dData;
        public Blend2dDataConstant m_Blend2dData;
        public BlendDirectDataConstant m_BlendDirectData;
        public uint m_ClipID;
        public uint m_ClipIndex;
        public float m_Duration;
        public float m_CycleOffset;
        public bool m_Mirror;

        public BlendTreeNodeConstant(ObjectReader reader)
        {
            int[] version = reader.version;
            this.m_BlendType = reader.ReadUInt32();
            this.m_BlendEventID = reader.ReadUInt32();
            this.m_BlendEventYID = reader.ReadUInt32();
            this.m_ChildIndices = reader.ReadUInt32Array(reader.ReadInt32());
            this.m_Blend1dData = new Blend1dDataConstant(reader);
            this.m_Blend2dData = new Blend2dDataConstant(reader);
            if (version[0] >= 5) //5.0 and up
            {
                this.m_BlendDirectData = new BlendDirectDataConstant(reader);
            }

            this.m_ClipID = reader.ReadUInt32();
            if (version[0] < 5) //5.0 down
            {
                this.m_ClipIndex = reader.ReadUInt32();
            }

            this.m_Duration = reader.ReadSingle();
            this.m_CycleOffset = reader.ReadSingle();
            this.m_Mirror = reader.ReadBoolean();
            reader.AlignStream();
        }
    }

    public class BlendTreeConstant
    {
        public BlendTreeNodeConstant[] m_NodeArray;

        public BlendTreeConstant(ObjectReader reader)
        {
            int numNodes = reader.ReadInt32();
            this.m_NodeArray = new BlendTreeNodeConstant[numNodes];
            for (var i = 0; i < numNodes; i++)
            {
                this.m_NodeArray[i] = new BlendTreeNodeConstant(reader);
            }
        }
    }

    public class StateConstant
    {
        public TransitionConstant[] m_TransitionConstantArray;
        public int[] m_BlendTreeConstantIndexArray;
        public LeafInfoConstant[] m_LeafInfoArray;
        public BlendTreeConstant[] m_BlendTreeConstantArray;
        public uint m_NameID;
        public uint m_PathID;
        public uint m_FullPathID;
        public uint m_TagID;
        public uint m_SpeedParamID;
        public uint m_MirrorParamID;
        public uint m_CycleOffsetParamID;
        public float m_Speed;
        public float m_CycleOffset;
        public bool m_IKOnFeet;
        public bool m_WriteDefaultValues;
        public bool m_Loop;
        public bool m_Mirror;

        public StateConstant(ObjectReader reader)
        {
            int[] version = reader.version;
            int numTransistions = reader.ReadInt32();
            this.m_TransitionConstantArray = new TransitionConstant[numTransistions];
            for (var i = 0; i < numTransistions; i++)
            {
                this.m_TransitionConstantArray[i] = new TransitionConstant(reader);
            }

            int numBlendIndices = reader.ReadInt32();
            this.m_BlendTreeConstantIndexArray = new int[numBlendIndices];
            for (var i = 0; i < numBlendIndices; i++)
            {
                this.m_BlendTreeConstantIndexArray[i] = reader.ReadInt32();
            }

            if (version[0] < 5) //5.0 down
            {
                int numInfos = reader.ReadInt32();
                this.m_LeafInfoArray = new LeafInfoConstant[numInfos];
                for (var i = 0; i < numInfos; i++)
                {
                    this.m_LeafInfoArray[i] = new LeafInfoConstant(reader);
                }
            }

            int numBlends = reader.ReadInt32();
            this.m_BlendTreeConstantArray = new BlendTreeConstant[numBlends];
            for (var i = 0; i < numBlends; i++)
            {
                this.m_BlendTreeConstantArray[i] = new BlendTreeConstant(reader);
            }

            this.m_NameID = reader.ReadUInt32();
            this.m_PathID = reader.ReadUInt32();
            if (version[0] >= 5) //5.0 and up
            {
                this.m_FullPathID = reader.ReadUInt32();
            }

            this.m_TagID = reader.ReadUInt32();
            if (version[0] >= 5) //5.0 and up
            {
                this.m_SpeedParamID = reader.ReadUInt32();
                this.m_MirrorParamID = reader.ReadUInt32();
                this.m_CycleOffsetParamID = reader.ReadUInt32();
            }

            if (version[0] > 2017 || version[0] == 2017 && version[1] >= 2) //2017.2 and up
            {
                uint m_TimeParamID = reader.ReadUInt32();
            }

            this.m_Speed = reader.ReadSingle();
            this.m_CycleOffset = reader.ReadSingle();
            this.m_IKOnFeet = reader.ReadBoolean();
            if (version[0] >= 5) //5.0 and up
            {
                this.m_WriteDefaultValues = reader.ReadBoolean();
            }

            this.m_Loop = reader.ReadBoolean();
            this.m_Mirror = reader.ReadBoolean();
            reader.AlignStream();
        }
    }

    public class SelectorTransitionConstant
    {
        public uint m_Destination;
        public ConditionConstant[] m_ConditionConstantArray;

        public SelectorTransitionConstant(ObjectReader reader)
        {
            this.m_Destination = reader.ReadUInt32();

            int numConditions = reader.ReadInt32();
            this.m_ConditionConstantArray = new ConditionConstant[numConditions];
            for (var i = 0; i < numConditions; i++)
            {
                this.m_ConditionConstantArray[i] = new ConditionConstant(reader);
            }
        }
    }

    public class SelectorStateConstant
    {
        public SelectorTransitionConstant[] m_TransitionConstantArray;
        public uint m_FullPathID;
        public bool m_isEntry;

        public SelectorStateConstant(ObjectReader reader)
        {
            int numTransitions = reader.ReadInt32();
            this.m_TransitionConstantArray = new SelectorTransitionConstant[numTransitions];
            for (var i = 0; i < numTransitions; i++)
            {
                this.m_TransitionConstantArray[i] = new SelectorTransitionConstant(reader);
            }

            this.m_FullPathID = reader.ReadUInt32();
            this.m_isEntry = reader.ReadBoolean();
            reader.AlignStream();
        }
    }

    public class StateMachineConstant
    {
        public StateConstant[] m_StateConstantArray;
        public TransitionConstant[] m_AnyStateTransitionConstantArray;
        public SelectorStateConstant[] m_SelectorStateConstantArray;
        public uint m_DefaultState;
        public uint m_MotionSetCount;

        public StateMachineConstant(ObjectReader reader)
        {
            int[] version = reader.version;
            int numStates = reader.ReadInt32();
            this.m_StateConstantArray = new StateConstant[numStates];
            for (var i = 0; i < numStates; i++)
            {
                this.m_StateConstantArray[i] = new StateConstant(reader);
            }

            int numAnyStates = reader.ReadInt32();
            this.m_AnyStateTransitionConstantArray = new TransitionConstant[numAnyStates];
            for (var i = 0; i < numAnyStates; i++)
            {
                this.m_AnyStateTransitionConstantArray[i] = new TransitionConstant(reader);
            }

            if (version[0] >= 5) //5.0 and up
            {
                int numSelectors = reader.ReadInt32();
                this.m_SelectorStateConstantArray = new SelectorStateConstant[numSelectors];
                for (var i = 0; i < numSelectors; i++)
                {
                    this.m_SelectorStateConstantArray[i] = new SelectorStateConstant(reader);
                }
            }

            this.m_DefaultState = reader.ReadUInt32();
            this.m_MotionSetCount = reader.ReadUInt32();
        }
    }

    public class ValueArray
    {
        public bool[] m_BoolValues;
        public int[] m_IntValues;
        public float[] m_FloatValues;
        public object[] m_PositionValues;
        public Vector4[] m_QuaternionValues;
        public object[] m_ScaleValues;

        public ValueArray(ObjectReader reader)
        {
            int[] version = reader.version;
            if (version[0] < 5 || version[0] == 5 && version[1] < 5) //5.5 down
            {
                int numBools = reader.ReadInt32();
                this.m_BoolValues = new bool[numBools];
                for (var i = 0; i < numBools; i++)
                {
                    this.m_BoolValues[i] = reader.ReadBoolean();
                }

                reader.AlignStream();

                this.m_IntValues = reader.ReadInt32Array(reader.ReadInt32());
                this.m_FloatValues = reader.ReadSingleArray(reader.ReadInt32());
            }

            int numPosValues = reader.ReadInt32();
            this.m_PositionValues = new object[numPosValues];
            for (var i = 0; i < numPosValues; i++)
            {
                this.m_PositionValues[i] = version[0] > 5 || version[0] == 5 && version[1] >= 4 ? (object) reader.ReadVector3() : (object) reader.ReadVector4(); //5.4 and up
            }

            this.m_QuaternionValues = reader.ReadVector4Array(reader.ReadInt32());

            int numScaleValues = reader.ReadInt32();
            this.m_ScaleValues = new object[numScaleValues];
            for (var i = 0; i < numScaleValues; i++)
            {
                this.m_ScaleValues[i] = version[0] > 5 || version[0] == 5 && version[1] >= 4 ? (object) reader.ReadVector3() : (object) reader.ReadVector4(); //5.4 adn up
            }

            if (version[0] > 5 || version[0] == 5 && version[1] >= 5) //5.5 and up
            {
                this.m_FloatValues = reader.ReadSingleArray(reader.ReadInt32());
                this.m_IntValues = reader.ReadInt32Array(reader.ReadInt32());

                int numBools = reader.ReadInt32();
                this.m_BoolValues = new bool[numBools];
                for (var i = 0; i < numBools; i++)
                {
                    this.m_BoolValues[i] = reader.ReadBoolean();
                }

                reader.AlignStream();
            }
        }
    }

    public class ControllerConstant
    {
        public LayerConstant[] m_LayerArray;
        public StateMachineConstant[] m_StateMachineArray;
        public ValueArrayConstant m_Values;
        public ValueArray m_DefaultValues;

        public ControllerConstant(ObjectReader reader)
        {
            int numLayers = reader.ReadInt32();
            this.m_LayerArray = new LayerConstant[numLayers];
            for (var i = 0; i < numLayers; i++)
            {
                this.m_LayerArray[i] = new LayerConstant(reader);
            }

            int numStates = reader.ReadInt32();
            this.m_StateMachineArray = new StateMachineConstant[numStates];
            for (var i = 0; i < numStates; i++)
            {
                this.m_StateMachineArray[i] = new StateMachineConstant(reader);
            }

            this.m_Values = new ValueArrayConstant(reader);
            this.m_DefaultValues = new ValueArray(reader);
        }
    }

    public sealed class AnimatorController : NamedObject
    {
        public PPtr[] m_AnimationClips;

        public AnimatorController(ObjectReader reader) : base(reader)
        {
            uint m_ControllerSize = reader.ReadUInt32();
            var m_Controller = new ControllerConstant(reader);

            int tosSize = reader.ReadInt32();
            var m_TOS = new List<KeyValuePair<uint, string>>(tosSize);
            for (var i = 0; i < tosSize; i++)
            {
                m_TOS.Add(new KeyValuePair<uint, string>(reader.ReadUInt32(), reader.ReadAlignedString()));
            }

            int numClips = reader.ReadInt32();
            this.m_AnimationClips = new PPtr[numClips];
            for (var i = 0; i < numClips; i++)
            {
                this.m_AnimationClips[i] = reader.ReadPPtr();
            }
        }
    }
}