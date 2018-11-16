using System;
using System.Collections.Generic;
using System.IO;
using AssetStudio.Extensions;
using AssetStudio.StudioClasses;
using SharpDX;

namespace AssetStudio
{
    public class Keyframe<T>
    {
        public float time;
        public T value;
        public T inSlope;
        public T outSlope;
        public int weightedMode;
        public T inWeight;
        public T outWeight;

        public Keyframe(ObjectReader reader, Func<T> readerFunc)
        {
            this.time = reader.ReadSingle();
            this.value = readerFunc();
            this.inSlope = readerFunc();
            this.outSlope = readerFunc();

            if (reader.version[0] >= 2018)
            {
                this.weightedMode = reader.ReadInt32();
                this.inWeight = readerFunc();
                this.outWeight = readerFunc();
            }
        }
    }

    public class AnimationCurve<T>
    {
        public List<Keyframe<T>> m_Curve;
        public int m_PreInfinity;
        public int m_PostInfinity;
        public int m_RotationOrder;

        public AnimationCurve(ObjectReader reader, Func<T> readerFunc)
        {
            int[] version = reader.version;
            int numCurves = reader.ReadInt32();

            this.m_Curve = new List<Keyframe<T>>(numCurves);

            for (var i = 0; i < numCurves; i++)
            {
                this.m_Curve.Add(new Keyframe<T>(reader, readerFunc));
            }

            this.m_PreInfinity = reader.ReadInt32();
            this.m_PostInfinity = reader.ReadInt32();

            if (version[0] > 5 || version[0] == 5 && version[1] >= 3) //5.3 and up
            {
                this.m_RotationOrder = reader.ReadInt32();
            }
        }
    }

    public class QuaternionCurve
    {
        public AnimationCurve<Quaternion> curve;
        public string path;

        public QuaternionCurve(ObjectReader reader)
        {
            this.curve = new AnimationCurve<Quaternion>(reader, reader.ReadQuaternion);
            this.path = reader.ReadAlignedString();
        }
    }

    public class PackedFloatVector
    {
        public uint m_NumItems;
        public float m_Range;
        public float m_Start;
        public byte[] m_Data;
        public byte m_BitSize;

        public PackedFloatVector(ObjectReader reader)
        {
            this.m_NumItems = reader.ReadUInt32();
            this.m_Range = reader.ReadSingle();
            this.m_Start = reader.ReadSingle();

            int numData = reader.ReadInt32();
            this.m_Data = reader.ReadBytes(numData);
            reader.AlignStream();

            this.m_BitSize = reader.ReadByte();
            reader.AlignStream();
        }

        public float[] UnpackFloats(int itemCountInChunk, int chunkStride, int start = 0, int numChunks = -1)
        {
            int bitPos = this.m_BitSize * start;
            int indexPos = bitPos / 8;
            bitPos %= 8;

            float scale = 1.0f / this.m_Range;
            if (numChunks == -1)
            {
                numChunks = (int) this.m_NumItems / itemCountInChunk;
            }
            int end = chunkStride * numChunks / 4;
            var data = new List<float>();
            for (var index = 0; index != end; index += chunkStride / 4)
            {
                for (var i = 0; i < itemCountInChunk; ++i)
                {
                    uint x = 0;

                    var bits = 0;
                    while (bits < this.m_BitSize)
                    {
                        x |= (uint) ((this.m_Data[indexPos] >> bitPos) << bits);
                        int num = Math.Min(this.m_BitSize - bits, 8 - bitPos);
                        bitPos += num;
                        bits += num;
                        if (bitPos == 8)
                        {
                            indexPos++;
                            bitPos = 0;
                        }
                    }
                    x &= (uint) (1 << this.m_BitSize) - 1u;
                    data.Add(x / (scale * ((1 << this.m_BitSize) - 1)) + this.m_Start);
                }
            }

            return data.ToArray();
        }
    }

    public class PackedIntVector
    {
        public uint m_NumItems;
        public byte[] m_Data;
        public byte m_BitSize;

        public PackedIntVector(ObjectReader reader)
        {
            this.m_NumItems = reader.ReadUInt32();

            int numData = reader.ReadInt32();
            this.m_Data = reader.ReadBytes(numData);
            reader.AlignStream();

            this.m_BitSize = reader.ReadByte();
            reader.AlignStream();
        }

        public int[] UnpackInts()
        {
            var data = new int[this.m_NumItems];
            var indexPos = 0;
            var bitPos = 0;
            for (var i = 0; i < this.m_NumItems; i++)
            {
                var bits = 0;
                data[i] = 0;
                while (bits < this.m_BitSize)
                {
                    data[i] |= (this.m_Data[indexPos] >> bitPos) << bits;
                    int num = Math.Min(this.m_BitSize - bits, 8 - bitPos);
                    bitPos += num;
                    bits += num;
                    if (bitPos == 8)
                    {
                        indexPos++;
                        bitPos = 0;
                    }
                }
                data[i] &= (1 << this.m_BitSize) - 1;
            }
            return data;
        }
    }

    public class PackedQuatVector
    {
        public uint m_NumItems;
        public byte[] m_Data;

        public PackedQuatVector(ObjectReader reader)
        {
            this.m_NumItems = reader.ReadUInt32();

            int numData = reader.ReadInt32();
            this.m_Data = reader.ReadBytes(numData);

            reader.AlignStream();
        }

        public Quaternion[] UnpackQuats()
        {
            var data = new Quaternion[this.m_NumItems];
            var indexPos = 0;
            var bitPos = 0;

            for (var i = 0; i < this.m_NumItems; i++)
            {
                uint flags = 0;

                var bits = 0;
                while (bits < 3)
                {
                    flags |= (uint) ((this.m_Data[indexPos] >> bitPos) << bits);
                    int num = Math.Min(3 - bits, 8 - bitPos);
                    bitPos += num;
                    bits += num;
                    if (bitPos == 8)
                    {
                        indexPos++;
                        bitPos = 0;
                    }
                }
                flags &= 7;

                var q = new Quaternion();
                float sum = 0;
                for (var j = 0; j < 4; j++)
                {
                    if ((flags & 3) != j)
                    {
                        int bitSize = ((flags & 3) + 1) % 4 == j ? 9 : 10;
                        uint x = 0;

                        bits = 0;
                        while (bits < bitSize)
                        {
                            x |= (uint) ((this.m_Data[indexPos] >> bitPos) << bits);
                            int num = Math.Min(bitSize - bits, 8 - bitPos);
                            bitPos += num;
                            bits += num;
                            if (bitPos == 8)
                            {
                                indexPos++;
                                bitPos = 0;
                            }
                        }
                        x &= (uint) ((1 << bitSize) - 1);
                        q[j] = x / (0.5f * ((1 << bitSize) - 1)) - 1;
                        sum += q[j] * q[j];
                    }
                }

                var lastComponent = (int) (flags & 3);
                q[lastComponent] = (float) Math.Sqrt(1 - sum);
                if ((flags & 4) != 0u)
                {
                    q[lastComponent] = -q[lastComponent];
                }
                data[i] = q;
            }

            return data;
        }
    }

    public class CompressedAnimationCurve
    {
        public string m_Path;
        public PackedIntVector m_Times;
        public PackedQuatVector m_Values;
        public PackedFloatVector m_Slopes;
        public int m_PreInfinity;
        public int m_PostInfinity;

        public CompressedAnimationCurve(ObjectReader reader)
        {
            this.m_Path = reader.ReadAlignedString();
            this.m_Times = new PackedIntVector(reader);
            this.m_Values = new PackedQuatVector(reader);
            this.m_Slopes = new PackedFloatVector(reader);
            this.m_PreInfinity = reader.ReadInt32();
            this.m_PostInfinity = reader.ReadInt32();
        }
    }

    public class Vector3Curve
    {
        public AnimationCurve<Vector3> curve;
        public string path;

        public Vector3Curve(ObjectReader reader)
        {
            this.curve = new AnimationCurve<Vector3>(reader, reader.ReadVector3);
            this.path = reader.ReadAlignedString();
        }
    }

    public class FloatCurve
    {
        public AnimationCurve<float> curve;
        public string attribute;
        public string path;
        public int classID;
        public PPtr script;

        public FloatCurve(ObjectReader reader)
        {
            this.curve = new AnimationCurve<float>(reader, reader.ReadSingle);
            this.attribute = reader.ReadAlignedString();
            this.path = reader.ReadAlignedString();
            this.classID = reader.ReadInt32();
            this.script = reader.ReadPPtr();
        }
    }

    public class PPtrKeyframe
    {
        public float time;
        public PPtr value;

        public PPtrKeyframe(ObjectReader reader)
        {
            this.time = reader.ReadSingle();
            this.value = reader.ReadPPtr();
        }
    }

    public class PPtrCurve
    {
        public List<PPtrKeyframe> curve;
        public string attribute;
        public string path;
        public int classID;
        public PPtr script;

        public PPtrCurve(ObjectReader reader)
        {
            int numCurves = reader.ReadInt32();

            this.curve = new List<PPtrKeyframe>(numCurves);

            for (var i = 0; i < numCurves; i++)
            {
                this.curve.Add(new PPtrKeyframe(reader));
            }

            this.attribute = reader.ReadAlignedString();
            this.path = reader.ReadAlignedString();
            this.classID = reader.ReadInt32();
            this.script = reader.ReadPPtr();
        }
    }

    public class AABB
    {
        public Vector3 m_Center;
        public Vector3 m_Extend;

        public AABB(ObjectReader reader)
        {
            this.m_Center = reader.ReadVector3();
            this.m_Extend = reader.ReadVector3();
        }
    }

    public class xform
    {
        public object t;
        public Quaternion q;
        public object s;

        public xform(ObjectReader reader)
        {
            int[] version = reader.version;

            this.t = version[0] > 5 || version[0] == 5 && version[1] >= 4 ? reader.ReadVector3() : (object) reader.ReadVector4(); //5.4 and up
            this.q = reader.ReadQuaternion();
            this.s = version[0] > 5 || version[0] == 5 && version[1] >= 4 ? reader.ReadVector3() : (object) reader.ReadVector4(); //5.4 and up
        }
    }

    public class HandPose
    {
        public xform m_GrabX;
        public float[] m_DoFArray;
        public float m_Override;
        public float m_CloseOpen;
        public float m_InOut;
        public float m_Grab;

        public HandPose(ObjectReader reader)
        {
            this.m_GrabX = new xform(reader);

            int numDoFs = reader.ReadInt32();
            this.m_DoFArray = reader.ReadSingleArray(numDoFs);

            this.m_Override = reader.ReadSingle();
            this.m_CloseOpen = reader.ReadSingle();
            this.m_InOut = reader.ReadSingle();
            this.m_Grab = reader.ReadSingle();
        }
    }

    public class HumanGoal
    {
        public xform m_X;
        public float m_WeightT;
        public float m_WeightR;
        public object m_HintT;
        public float m_HintWeightT;

        public HumanGoal(ObjectReader reader)
        {
            int[] version = reader.version;

            this.m_X = new xform(reader);
            this.m_WeightT = reader.ReadSingle();
            this.m_WeightR = reader.ReadSingle();

            if (version[0] >= 5) //5.0 and up
            {
                this.m_HintT = version[0] > 5 || version[0] == 5 && version[1] >= 4 ? reader.ReadVector3() : (object) reader.ReadVector4(); //5.4 and up
                this.m_HintWeightT = reader.ReadSingle();
            }
        }
    }

    public class HumanPose
    {
        public xform m_RootX;
        public object m_LookAtPosition;
        public Vector4 m_LookAtWeight;
        public List<HumanGoal> m_GoalArray;
        public HandPose m_LeftHandPose;
        public HandPose m_RightHandPose;
        public float[] m_DoFArray;
        public object[] m_TDoFArray;

        public HumanPose(ObjectReader reader)
        {
            int[] version = reader.version;

            this.m_RootX = new xform(reader);
            this.m_LookAtPosition = version[0] > 5 || version[0] == 5 && version[1] >= 4 ? reader.ReadVector3() : (object) reader.ReadVector4(); //5.4 and up
            this.m_LookAtWeight = reader.ReadVector4();

            int numGoals = reader.ReadInt32();
            this.m_GoalArray = new List<HumanGoal>(numGoals);

            for (var i = 0; i < numGoals; i++)
            {
                this.m_GoalArray.Add(new HumanGoal(reader));
            }

            this.m_LeftHandPose = new HandPose(reader);
            this.m_RightHandPose = new HandPose(reader);

            int numDoFs = reader.ReadInt32();
            this.m_DoFArray = reader.ReadSingleArray(numDoFs);

            if (version[0] > 5 || version[0] == 5 && version[1] >= 2) //5.2 and up
            {
                int numTDof = reader.ReadInt32();
                this.m_TDoFArray = new object[numTDof];

                for (var i = 0; i < numTDof; i++)
                {
                    this.m_TDoFArray[i] = version[0] > 5 || version[0] == 5 && version[1] >= 4 ? reader.ReadVector3() : (object) reader.ReadVector4(); //5.4 and up
                }
            }
        }
    }

    public class StreamedClip
    {
        public uint[] data;
        public uint curveCount;

        public StreamedClip(ObjectReader reader)
        {
            int numData = reader.ReadInt32();
            this.data = reader.ReadUInt32Array(numData);
            this.curveCount = reader.ReadUInt32();
        }

        public class StreamedCurveKey
        {
            public int index;
            public float[] coeff;

            public float value;
            public float outSlope;
            public float inSlope;

            public StreamedCurveKey(BinaryReader reader)
            {
                this.index = reader.ReadInt32();
                this.coeff = reader.ReadSingleArray(4);

                this.outSlope = this.coeff[2];
                this.value = this.coeff[3];
            }

            public float CalculateNextInSlope(float dx, StreamedCurveKey rhs)
            {
                //Stepped
                if (this.coeff[0] == 0f && this.coeff[1] == 0f && this.coeff[2] == 0f)
                {
                    return float.PositiveInfinity;
                }

                dx = Math.Max(dx, 0.0001f);
                float dy = rhs.value - this.value;
                float length = 1.0f / (dx * dx);
                float d1 = this.outSlope * dx;
                float d2 = dy + dy + dy - d1 - d1 - this.coeff[1] / length;
                return d2 / dx;
            }
        }

        public class StreamedFrame
        {
            public float time;
            public List<StreamedCurveKey> keyList;

            public StreamedFrame(BinaryReader reader)
            {
                this.time = reader.ReadSingle();

                int numKeys = reader.ReadInt32();
                this.keyList = new List<StreamedCurveKey>(numKeys);
                for (var i = 0; i < numKeys; i++)
                {
                    this.keyList.Add(new StreamedCurveKey(reader));
                }
            }
        }

        public List<StreamedFrame> ReadData()
        {
            var frameList = new List<StreamedFrame>();
            using (Stream stream = new MemoryStream())
            {
                var writer = new BinaryWriter(stream);
                writer.Write(this.data);
                stream.Position = 0;
                while (stream.Position < stream.Length)
                {
                    frameList.Add(new StreamedFrame(new BinaryReader(stream)));
                }
            }

            for (var frameIndex = 2; frameIndex < frameList.Count - 1; frameIndex++)
            {
                StreamedFrame frame = frameList[frameIndex];
                foreach (StreamedCurveKey curveKey in frame.keyList)
                {
                    for (int i = frameIndex - 1; i >= 0; i--)
                    {
                        StreamedFrame preFrame = frameList[i];
                        StreamedCurveKey preCurveKey = preFrame.keyList.Find(x => x.index == curveKey.index);
                        if (preCurveKey != null)
                        {
                            curveKey.inSlope = preCurveKey.CalculateNextInSlope(frame.time - preFrame.time, curveKey);
                            break;
                        }
                    }
                }
            }
            return frameList;
        }
    }

    public class DenseClip
    {
        public int m_FrameCount;
        public uint m_CurveCount;
        public float m_SampleRate;
        public float m_BeginTime;
        public float[] m_SampleArray;

        public DenseClip(ObjectReader reader)
        {
            this.m_FrameCount = reader.ReadInt32();
            this.m_CurveCount = reader.ReadUInt32();
            this.m_SampleRate = reader.ReadSingle();
            this.m_BeginTime = reader.ReadSingle();

            int numSamples = reader.ReadInt32();
            this.m_SampleArray = reader.ReadSingleArray(numSamples);
        }
    }

    public class ConstantClip
    {
        public float[] data;

        public ConstantClip(ObjectReader reader)
        {
            int numData = reader.ReadInt32();
            this.data = reader.ReadSingleArray(numData);
        }
    }

    public class ValueConstant
    {
        public uint m_ID;
        public uint m_TypeID;
        public uint m_Type;
        public uint m_Index;

        public ValueConstant(ObjectReader reader)
        {
            int[] version = reader.version;

            this.m_ID = reader.ReadUInt32();

            if (version[0] < 5 || version[0] == 5 && version[1] < 5) //5.5 down
            {
                this.m_TypeID = reader.ReadUInt32();
            }

            this.m_Type = reader.ReadUInt32();
            this.m_Index = reader.ReadUInt32();
        }
    }

    public class ValueArrayConstant
    {
        public List<ValueConstant> m_ValueArray;

        public ValueArrayConstant(ObjectReader reader)
        {
            int numVals = reader.ReadInt32();
            this.m_ValueArray = new List<ValueConstant>(numVals);

            for (var i = 0; i < numVals; i++)
            {
                this.m_ValueArray.Add(new ValueConstant(reader));
            }
        }
    }

    public class Clip
    {
        public StreamedClip m_StreamedClip;
        public DenseClip m_DenseClip;
        public ConstantClip m_ConstantClip;
        public ValueArrayConstant m_Binding;

        public Clip(ObjectReader reader)
        {
            int[] version = reader.version;

            this.m_StreamedClip = new StreamedClip(reader);
            this.m_DenseClip = new DenseClip(reader);

            if (version[0] > 4 || version[0] == 4 && version[1] >= 3) //4.3 and up
            {
                this.m_ConstantClip = new ConstantClip(reader);
            }

            this.m_Binding = new ValueArrayConstant(reader);
        }
    }

    public class ValueDelta
    {
        public float m_Start;
        public float m_Stop;

        public ValueDelta(ObjectReader reader)
        {
            this.m_Start = reader.ReadSingle();
            this.m_Stop = reader.ReadSingle();
        }
    }

    public class ClipMuscleConstant
    {
        public HumanPose m_DeltaPose;
        public xform m_StartX;
        public xform m_StopX;
        public xform m_LeftFootStartX;
        public xform m_RightFootStartX;
        public xform m_MotionStartX;
        public xform m_MotionStopX;
        public object m_AverageSpeed;
        public Clip m_Clip;
        public float m_StartTime;
        public float m_StopTime;
        public float m_OrientationOffsetY;
        public float m_Level;
        public float m_CycleOffset;
        public float m_AverageAngularSpeed;
        public int[] m_IndexArray;
        public List<ValueDelta> m_ValueArrayDelta;
        public float[] m_ValueArrayReferencePose;
        public bool m_Mirror;
        public bool m_LoopTime;
        public bool m_LoopBlend;
        public bool m_LoopBlendOrientation;
        public bool m_LoopBlendPositionY;
        public bool m_LoopBlendPositionXZ;
        public bool m_StartAtOrigin;
        public bool m_KeepOriginalOrientation;
        public bool m_KeepOriginalPositionY;
        public bool m_KeepOriginalPositionXZ;
        public bool m_HeightFromFeet;

        public ClipMuscleConstant(ObjectReader reader)
        {
            int[] version = reader.version;

            this.m_DeltaPose = new HumanPose(reader);
            this.m_StartX = new xform(reader);

            if (version[0] > 5 || version[0] == 5 && version[1] >= 5) //5.5 and up
            {
                this.m_StopX = new xform(reader);
            }

            this.m_LeftFootStartX = new xform(reader);
            this.m_RightFootStartX = new xform(reader);

            if (version[0] < 5) //5.0 down
            {
                this.m_MotionStartX = new xform(reader);
                this.m_MotionStopX = new xform(reader);
            }

            this.m_AverageSpeed = version[0] > 5 || version[0] == 5 && version[1] >= 4 ? reader.ReadVector3() : (object) reader.ReadVector4(); //5.4 and up
            this.m_Clip = new Clip(reader);
            this.m_StartTime = reader.ReadSingle();
            this.m_StopTime = reader.ReadSingle();
            this.m_OrientationOffsetY = reader.ReadSingle();
            this.m_Level = reader.ReadSingle();
            this.m_CycleOffset = reader.ReadSingle();
            this.m_AverageAngularSpeed = reader.ReadSingle();

            int numIndices = reader.ReadInt32();
            this.m_IndexArray = reader.ReadInt32Array(numIndices);

            if (version[0] < 4 || version[0] == 4 && version[1] < 3) //4.3 down
            {
                int numAdditionalCurveIndexs = reader.ReadInt32();
                var m_AdditionalCurveIndexArray = new List<int>(numAdditionalCurveIndexs);

                for (var i = 0; i < numAdditionalCurveIndexs; i++)
                {
                    m_AdditionalCurveIndexArray.Add(reader.ReadInt32());
                }
            }

            int numDeltas = reader.ReadInt32();
            this.m_ValueArrayDelta = new List<ValueDelta>(numDeltas);

            for (var i = 0; i < numDeltas; i++)
            {
                this.m_ValueArrayDelta.Add(new ValueDelta(reader));
            }

            if (version[0] > 5 || version[0] == 5 && version[1] >= 3) //5.3 and up
            {
                this.m_ValueArrayReferencePose = reader.ReadSingleArray(reader.ReadInt32());
            }

            this.m_Mirror = reader.ReadBoolean();
            this.m_LoopTime = reader.ReadBoolean();
            this.m_LoopBlend = reader.ReadBoolean();
            this.m_LoopBlendOrientation = reader.ReadBoolean();
            this.m_LoopBlendPositionY = reader.ReadBoolean();
            this.m_LoopBlendPositionXZ = reader.ReadBoolean();

            if (version[0] > 5 || version[0] == 5 && version[1] >= 5) //5.5 and up
            {
                this.m_StartAtOrigin = reader.ReadBoolean();
            }

            this.m_KeepOriginalOrientation = reader.ReadBoolean();
            this.m_KeepOriginalPositionY = reader.ReadBoolean();
            this.m_KeepOriginalPositionXZ = reader.ReadBoolean();
            this.m_HeightFromFeet = reader.ReadBoolean();

            reader.AlignStream();
        }
    }

    public class GenericBinding
    {
        public uint path;
        public uint attribute;
        public PPtr script;
        public ClassIDType typeID;
        public byte customType;
        public byte isPPtrCurve;

        public GenericBinding(ObjectReader reader)
        {
            int[] version = reader.version;

            this.path = reader.ReadUInt32();
            this.attribute = reader.ReadUInt32();
            this.script = reader.ReadPPtr();

            if (version[0] > 5 || version[0] == 5 && version[1] >= 6) //5.6 and up
            {
                this.typeID = (ClassIDType) reader.ReadInt32();
            }
            else
            {
                this.typeID = (ClassIDType) reader.ReadUInt16();
            }

            this.customType = reader.ReadByte();
            this.isPPtrCurve = reader.ReadByte();

            reader.AlignStream();
        }
    }

    public class AnimationClipBindingConstant
    {
        public List<GenericBinding> genericBindings;
        public List<PPtr> pptrCurveMapping;

        public AnimationClipBindingConstant(ObjectReader reader)
        {
            int numBindings = reader.ReadInt32();
            this.genericBindings = new List<GenericBinding>(numBindings);

            for (var i = 0; i < numBindings; i++)
            {
                this.genericBindings.Add(new GenericBinding(reader));
            }

            int numMappings = reader.ReadInt32();
            this.pptrCurveMapping = new List<PPtr>(numMappings);

            for (var i = 0; i < numMappings; i++)
            {
                this.pptrCurveMapping.Add(reader.ReadPPtr());
            }
        }

        public GenericBinding FindBinding(int index)
        {
            var curves = 0;

            foreach (GenericBinding b in this.genericBindings)
            {
                if (b.typeID == ClassIDType.Transform)
                {
                    switch (b.attribute)
                    {
                        case 1: //kBindTransformPosition
                        case 3: //kBindTransformScale
                        case 4: //kBindTransformEuler
                            curves += 3;
                            break;
                        case 2: //kBindTransformRotation
                            curves += 4;
                            break;
                        default:
                            curves += 1;
                            break;
                    }
                }
                else
                {
                    curves += 1;
                }

                if (curves > index)
                {
                    return b;
                }
            }

            return null;
        }
    }

    public enum AnimationType
    {
        kLegacy = 1,
        kGeneric = 2,
        kHumanoid = 3
    }

    public sealed class AnimationClip : NamedObject
    {
        public AnimationType m_AnimationType;
        public bool m_Legacy;
        public bool m_Compressed;
        public bool m_UseHighQualityCurve;
        public List<QuaternionCurve> m_RotationCurves;
        public List<CompressedAnimationCurve> m_CompressedRotationCurves;
        public List<Vector3Curve> m_EulerCurves;
        public List<Vector3Curve> m_PositionCurves;
        public List<Vector3Curve> m_ScaleCurves;
        public List<FloatCurve> m_FloatCurves;
        public List<PPtrCurve> m_PPtrCurves;
        public float m_SampleRate;
        public int m_WrapMode;
        public AABB m_Bounds;
        public uint m_MuscleClipSize;
        public ClipMuscleConstant m_MuscleClip;

        public AnimationClipBindingConstant m_ClipBindingConstant;
        //public List<AnimationEvent> m_Events;

        public AnimationClip(ObjectReader reader) : base(reader)
        {
            if (this.version[0] >= 5) //5.0 and up
            {
                this.m_Legacy = reader.ReadBoolean();
            }
            else if (this.version[0] >= 4) //4.0 and up
            {
                this.m_AnimationType = (AnimationType) reader.ReadInt32();

                if (this.m_AnimationType == AnimationType.kLegacy)
                {
                    this.m_Legacy = true;
                }
            }
            else
            {
                this.m_Legacy = true;
            }

            this.m_Compressed = reader.ReadBoolean();

            if (this.version[0] > 4 || this.version[0] == 4 && this.version[1] >= 3) //4.3 and up
            {
                this.m_UseHighQualityCurve = reader.ReadBoolean();
            }

            reader.AlignStream();

            int numRCurves = reader.ReadInt32();
            this.m_RotationCurves = new List<QuaternionCurve>(numRCurves);

            for (var i = 0; i < numRCurves; i++)
            {
                this.m_RotationCurves.Add(new QuaternionCurve(reader));
            }

            int numCRCurves = reader.ReadInt32();
            this.m_CompressedRotationCurves = new List<CompressedAnimationCurve>(numCRCurves);

            for (var i = 0; i < numCRCurves; i++)
            {
                this.m_CompressedRotationCurves.Add(new CompressedAnimationCurve(reader));
            }

            if (this.version[0] > 5 || this.version[0] == 5 && this.version[1] >= 3) //5.3 and up
            {
                int numEulerCurves = reader.ReadInt32();
                this.m_EulerCurves = new List<Vector3Curve>(numEulerCurves);

                for (var i = 0; i < numEulerCurves; i++)
                {
                    this.m_EulerCurves.Add(new Vector3Curve(reader));
                }
            }

            int numPCurves = reader.ReadInt32();
            this.m_PositionCurves = new List<Vector3Curve>(numPCurves);

            for (var i = 0; i < numPCurves; i++)
            {
                this.m_PositionCurves.Add(new Vector3Curve(reader));
            }

            int numSCurves = reader.ReadInt32();
            this.m_ScaleCurves = new List<Vector3Curve>(numSCurves);

            for (var i = 0; i < numSCurves; i++)
            {
                this.m_ScaleCurves.Add(new Vector3Curve(reader));
            }

            int numFCurves = reader.ReadInt32();
            this.m_FloatCurves = new List<FloatCurve>(numFCurves);

            for (var i = 0; i < numFCurves; i++)
            {
                this.m_FloatCurves.Add(new FloatCurve(reader));
            }

            if (this.version[0] > 4 || this.version[0] == 4 && this.version[1] >= 3) //4.3 and up
            {
                int numPtrCurves = reader.ReadInt32();
                this.m_PPtrCurves = new List<PPtrCurve>(numPtrCurves);

                for (var i = 0; i < numPtrCurves; i++)
                {
                    this.m_PPtrCurves.Add(new PPtrCurve(reader));
                }
            }

            this.m_SampleRate = reader.ReadSingle();
            this.m_WrapMode = reader.ReadInt32();

            if (this.version[0] > 3 || this.version[0] == 3 && this.version[1] >= 4) //3.4 and up
            {
                this.m_Bounds = new AABB(reader);
            }

            if (this.version[0] >= 4) //4.0 and up
            {
                this.m_MuscleClipSize = reader.ReadUInt32();
                this.m_MuscleClip = new ClipMuscleConstant(reader);
            }

            if (this.version[0] > 4 || this.version[0] == 4 && this.version[1] >= 3) //4.3 and up
            {
                this.m_ClipBindingConstant = new AnimationClipBindingConstant(reader);
            }

            /*int numEvents = reader.ReadInt32();
            m_Events = new List<AnimationEvent>(numEvents);
            for (int i = 0; i < numEvents; i++)
            {
                m_Events.Add(new AnimationEvent(stream, file.Version[0] - '0'));
            }*/
        }
    }
}