using System;
using System.Collections.Concurrent;
using AssetStudio.Extensions;
using SharpDX;

namespace AssetStudio.StudioClasses
{
    public partial class UnityAssetSerializer
    {
        public static readonly ConcurrentDictionary<string, Deserializer> SimpleDeserializers = new ConcurrentDictionary<string, Deserializer>
        {
            { typeof(bool).FullName, (r, _) => r.ReadBoolean() },
            { typeof(byte).FullName, (r, _) => r.ReadByte() },
            { typeof(sbyte).FullName, (r, _) => r.ReadSByte() },
            { typeof(short).FullName, (r, _) => r.ReadInt16() },
            { typeof(ushort).FullName, (r, _) => r.ReadUInt16() },
            { typeof(int).FullName, (r, _) => r.ReadInt32() },
            { typeof(uint).FullName, (r, _) => r.ReadUInt32() },
            { typeof(long).FullName, (r, _) => r.ReadInt64() },
            { typeof(ulong).FullName, (r, _) => r.ReadUInt64() },
            { typeof(float).FullName, (r, _) => r.ReadSingle() },
            { typeof(double).FullName, (r, _) => r.ReadDouble() },
            { typeof(char).FullName, (r, _) => (char) r.ReadUInt16() },
            { typeof(string).FullName, (r, _) => r.ReadAlignedString() },
            { "UnityEngine.Vector2", (r, _) => r.ReadVector2() },
            { "UnityEngine.Vector3", (r, _) => r.ReadVector3() },
            { "UnityEngine.Vector4", (r, _) => r.ReadVector4() },
            { "UnityEngine.Quaternion", (r, _) => r.ReadQuaternion() },
            {
                typeof(IntPtr).FullName, (r, a) =>
                {
                    switch (a.m_TargetPlatform)
                    {
                        case BuildTarget.StandaloneLinux64:
                        case BuildTarget.StandaloneWindows64:
                        case BuildTarget.StandaloneOSXIntel64:
                            return (IntPtr) r.ReadInt64();
                        case BuildTarget.StandaloneLinux:
                        case BuildTarget.StandaloneWindows:
                        case BuildTarget.StandaloneOSXIntel:
                            return (IntPtr) r.ReadInt32();
                    }
                    throw new NotImplementedException(a.m_TargetPlatform.ToString());
                }
            },
            {
                typeof(UIntPtr).FullName, (r, a) =>
                {
                    switch (a.m_TargetPlatform)
                    {
                        case BuildTarget.StandaloneLinux64:
                        case BuildTarget.StandaloneWindows64:
                        case BuildTarget.StandaloneOSXIntel64:
                            return (UIntPtr) r.ReadUInt64();
                        case BuildTarget.StandaloneLinux:
                        case BuildTarget.StandaloneWindows:
                        case BuildTarget.StandaloneOSXIntel:
                            return (UIntPtr) r.ReadUInt32();
                    }
                    throw new NotImplementedException(a.m_TargetPlatform.ToString());
                }
            },
            {
                "UnityEngine.Rect", (r, _) =>
                {
                    float[] rect = r.ReadSingleArray(4);
                    // TODO: verify XYWH vs LTRB
                    return new RectangleF(rect[0], rect[1], rect[2], rect[3]);
                }
            },
            {
                "UnityEngine.RectOffset", (r, _) =>
                {
                    int[] rect = r.ReadInt32Array(4);
                    // TODO: verify XYWH vs LTRB
                    return new Rectangle(rect[0], rect[1], rect[2], rect[3]);
                }
            },
            {
                "UnityEngine.Color", (r, _) =>
                {
                    byte[] color = r.ReadBytes(4);
                    // TODO: verify order, BGRA vs ARGB
                    return new Color(color[0], color[1], color[2], color[3]);
                }
            },
            {
                "UnityEngine.Color32", (r, _) =>
                {
                    float[] color = r.ReadSingleArray(4);
                    // TODO: verify order, BGRA vs ARGB
                    return new Color4(color[0], color[1], color[2], color[3]);
                }
            },
            {
                "UnityEngine.Matrix4x4", (r, _) =>
                {
                    float[] matrix = r.ReadSingleArray(4 * 4);
                    // TODO: verify order TL -> BR
                    return new Matrix(matrix);
                }
            },
        };
    }
}