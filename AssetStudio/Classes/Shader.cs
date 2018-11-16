using System;
using System.Collections.Generic;
using System.IO;
using AssetStudio.Extensions;
using AssetStudio.StudioClasses;

namespace AssetStudio
{
    public class StructParameter
    {
        public List<MatrixParameter> m_MatrixParams;
        public List<VectorParameter> m_VectorParams;

        public StructParameter(BinaryReader reader)
        {
            int m_NameIndex = reader.ReadInt32();
            int m_Index = reader.ReadInt32();
            int m_ArraySize = reader.ReadInt32();
            int m_StructSize = reader.ReadInt32();

            int numVectorParams = reader.ReadInt32();
            this.m_VectorParams = new List<VectorParameter>(numVectorParams);
            for (var i = 0; i < numVectorParams; i++)
            {
                this.m_VectorParams.Add(new VectorParameter(reader));
            }

            int numMatrixParams = reader.ReadInt32();
            this.m_MatrixParams = new List<MatrixParameter>(numMatrixParams);
            for (var i = 0; i < numMatrixParams; i++)
            {
                this.m_MatrixParams.Add(new MatrixParameter(reader));
            }
        }
    }

    public class SamplerParameter
    {
        public uint sampler;
        public int bindPoint;

        public SamplerParameter(BinaryReader reader)
        {
            this.sampler = reader.ReadUInt32();
            this.bindPoint = reader.ReadInt32();
        }
    }

    public enum TextureDimension
    {
        kTexDimUnknown = -1,
        kTexDimNone = 0,
        kTexDimAny = 1,
        kTexDim2D = 2,
        kTexDim3D = 3,
        kTexDimCUBE = 4,
        kTexDim2DArray = 5,
        kTexDimCubeArray = 6,
        kTexDimForce32Bit = 2147483647
    };

    public class SerializedTextureProperty
    {
        public string m_DefaultName;
        public TextureDimension m_TexDim;

        public SerializedTextureProperty(BinaryReader reader)
        {
            this.m_DefaultName = reader.ReadAlignedString();
            this.m_TexDim = (TextureDimension) reader.ReadInt32();
        }
    }

    public enum SerializedPropertyType
    {
        kColor = 0,
        kVector = 1,
        kFloat = 2,
        kRange = 3,
        kTexture = 4
    };

    public class SerializedProperty
    {
        public string m_Name;
        public string m_Description;
        public List<string> m_Attributes;
        public SerializedPropertyType m_Type;
        public uint m_Flags;
        public List<float> m_DefValue;
        public SerializedTextureProperty m_DefTexture;

        public SerializedProperty(BinaryReader reader)
        {
            this.m_Name = reader.ReadAlignedString();
            this.m_Description = reader.ReadAlignedString();

            int numAttributes = reader.ReadInt32();
            this.m_Attributes = new List<string>(numAttributes);
            for (var i = 0; i < numAttributes; i++)
            {
                this.m_Attributes.Add(reader.ReadAlignedString());
            }

            this.m_Type = (SerializedPropertyType) reader.ReadInt32();
            this.m_Flags = reader.ReadUInt32();

            var numValues = 4;
            this.m_DefValue = new List<float>(numValues);
            for (var i = 0; i < numValues; i++)
            {
                this.m_DefValue.Add(reader.ReadSingle());
            }

            this.m_DefTexture = new SerializedTextureProperty(reader);
        }
    }

    public class SerializedProperties
    {
        public List<SerializedProperty> m_Props;

        public SerializedProperties(BinaryReader reader)
        {
            int numProps = reader.ReadInt32();
            this.m_Props = new List<SerializedProperty>(numProps);
            for (var i = 0; i < numProps; i++)
            {
                this.m_Props.Add(new SerializedProperty(reader));
            }
        }
    }

    public class SerializedShaderFloatValue
    {
        public float val;
        public string name;

        public SerializedShaderFloatValue(BinaryReader reader)
        {
            this.val = reader.ReadSingle();
            this.name = reader.ReadAlignedString();
        }
    }

    public class SerializedShaderRTBlendState
    {
        public SerializedShaderFloatValue srcBlend;
        public SerializedShaderFloatValue destBlend;
        public SerializedShaderFloatValue srcBlendAlpha;
        public SerializedShaderFloatValue destBlendAlpha;
        public SerializedShaderFloatValue blendOp;
        public SerializedShaderFloatValue blendOpAlpha;
        public SerializedShaderFloatValue colMask;

        public SerializedShaderRTBlendState(BinaryReader reader)
        {
            this.srcBlend = new SerializedShaderFloatValue(reader);
            this.destBlend = new SerializedShaderFloatValue(reader);
            this.srcBlendAlpha = new SerializedShaderFloatValue(reader);
            this.destBlendAlpha = new SerializedShaderFloatValue(reader);
            this.blendOp = new SerializedShaderFloatValue(reader);
            this.blendOpAlpha = new SerializedShaderFloatValue(reader);
            this.colMask = new SerializedShaderFloatValue(reader);
        }
    }

    public class SerializedStencilOp
    {
        public SerializedShaderFloatValue pass;
        public SerializedShaderFloatValue fail;
        public SerializedShaderFloatValue zFail;
        public SerializedShaderFloatValue comp;

        public SerializedStencilOp(BinaryReader reader)
        {
            this.pass = new SerializedShaderFloatValue(reader);
            this.fail = new SerializedShaderFloatValue(reader);
            this.zFail = new SerializedShaderFloatValue(reader);
            this.comp = new SerializedShaderFloatValue(reader);
        }
    }

    public class SerializedShaderVectorValue
    {
        public SerializedShaderFloatValue x;
        public SerializedShaderFloatValue y;
        public SerializedShaderFloatValue z;
        public SerializedShaderFloatValue w;
        public string name;

        public SerializedShaderVectorValue(BinaryReader reader)
        {
            this.x = new SerializedShaderFloatValue(reader);
            this.y = new SerializedShaderFloatValue(reader);
            this.z = new SerializedShaderFloatValue(reader);
            this.w = new SerializedShaderFloatValue(reader);
            this.name = reader.ReadAlignedString();
        }
    }

    public enum FogMode
    {
        kFogUnknown = -1,
        kFogDisabled = 0,
        kFogLinear = 1,
        kFogExp = 2,
        kFogExp2 = 3,
        kFogModeCount = 4
    };

    public class SerializedShaderState
    {
        public string m_Name;
        public SerializedShaderRTBlendState rtBlend0;
        public SerializedShaderRTBlendState rtBlend1;
        public SerializedShaderRTBlendState rtBlend2;
        public SerializedShaderRTBlendState rtBlend3;
        public SerializedShaderRTBlendState rtBlend4;
        public SerializedShaderRTBlendState rtBlend5;
        public SerializedShaderRTBlendState rtBlend6;
        public SerializedShaderRTBlendState rtBlend7;
        public bool rtSeparateBlend;
        public SerializedShaderFloatValue zClip;
        public SerializedShaderFloatValue zTest;
        public SerializedShaderFloatValue zWrite;
        public SerializedShaderFloatValue culling;
        public SerializedShaderFloatValue offsetFactor;
        public SerializedShaderFloatValue offsetUnits;
        public SerializedShaderFloatValue alphaToMask;
        public SerializedStencilOp stencilOp;
        public SerializedStencilOp stencilOpFront;
        public SerializedStencilOp stencilOpBack;
        public SerializedShaderFloatValue stencilReadMask;
        public SerializedShaderFloatValue stencilWriteMask;
        public SerializedShaderFloatValue stencilRef;
        public SerializedShaderFloatValue fogStart;
        public SerializedShaderFloatValue fogEnd;
        public SerializedShaderFloatValue fogDensity;
        public SerializedShaderVectorValue fogColor;
        public FogMode fogMode;
        public int gpuProgramID;
        public SerializedTagMap m_Tags;
        public int m_LOD;
        public bool lighting;

        public SerializedShaderState(ObjectReader reader)
        {
            int[] version = reader.version;

            this.m_Name = reader.ReadAlignedString();
            this.rtBlend0 = new SerializedShaderRTBlendState(reader);
            this.rtBlend1 = new SerializedShaderRTBlendState(reader);
            this.rtBlend2 = new SerializedShaderRTBlendState(reader);
            this.rtBlend3 = new SerializedShaderRTBlendState(reader);
            this.rtBlend4 = new SerializedShaderRTBlendState(reader);
            this.rtBlend5 = new SerializedShaderRTBlendState(reader);
            this.rtBlend6 = new SerializedShaderRTBlendState(reader);
            this.rtBlend7 = new SerializedShaderRTBlendState(reader);
            this.rtSeparateBlend = reader.ReadBoolean();
            reader.AlignStream();
            if (version[0] > 2017 || version[0] == 2017 && version[1] >= 2) //2017.2 and up
            {
                this.zClip = new SerializedShaderFloatValue(reader);
            }
            this.zTest = new SerializedShaderFloatValue(reader);
            this.zWrite = new SerializedShaderFloatValue(reader);
            this.culling = new SerializedShaderFloatValue(reader);
            this.offsetFactor = new SerializedShaderFloatValue(reader);
            this.offsetUnits = new SerializedShaderFloatValue(reader);
            this.alphaToMask = new SerializedShaderFloatValue(reader);
            this.stencilOp = new SerializedStencilOp(reader);
            this.stencilOpFront = new SerializedStencilOp(reader);
            this.stencilOpBack = new SerializedStencilOp(reader);
            this.stencilReadMask = new SerializedShaderFloatValue(reader);
            this.stencilWriteMask = new SerializedShaderFloatValue(reader);
            this.stencilRef = new SerializedShaderFloatValue(reader);
            this.fogStart = new SerializedShaderFloatValue(reader);
            this.fogEnd = new SerializedShaderFloatValue(reader);
            this.fogDensity = new SerializedShaderFloatValue(reader);
            this.fogColor = new SerializedShaderVectorValue(reader);
            this.fogMode = (FogMode) reader.ReadInt32();
            this.gpuProgramID = reader.ReadInt32();
            this.m_Tags = new SerializedTagMap(reader);
            this.m_LOD = reader.ReadInt32();
            this.lighting = reader.ReadBoolean();
            reader.AlignStream();
        }
    }

    public class ShaderBindChannel
    {
        public sbyte source;
        public sbyte target;

        public ShaderBindChannel(BinaryReader reader)
        {
            this.source = reader.ReadSByte();
            this.target = reader.ReadSByte();
        }
    }

    public class ParserBindChannels
    {
        public List<ShaderBindChannel> m_Channels;
        public uint m_SourceMap;

        public ParserBindChannels(BinaryReader reader)
        {
            int numChannels = reader.ReadInt32();
            this.m_Channels = new List<ShaderBindChannel>(numChannels);
            for (var i = 0; i < numChannels; i++)
            {
                this.m_Channels.Add(new ShaderBindChannel(reader));
            }
            reader.AlignStream();

            this.m_SourceMap = reader.ReadUInt32();
        }
    }

    public class VectorParameter
    {
        public int m_NameIndex;
        public int m_Index;
        public int m_ArraySize;
        public sbyte m_Type;
        public sbyte m_Dim;

        public VectorParameter(BinaryReader reader)
        {
            this.m_NameIndex = reader.ReadInt32();
            this.m_Index = reader.ReadInt32();
            this.m_ArraySize = reader.ReadInt32();
            this.m_Type = reader.ReadSByte();
            this.m_Dim = reader.ReadSByte();
            reader.AlignStream();
        }
    }

    public class MatrixParameter
    {
        public int m_NameIndex;
        public int m_Index;
        public int m_ArraySize;
        public sbyte m_Type;
        public sbyte m_RowCount;

        public MatrixParameter(BinaryReader reader)
        {
            this.m_NameIndex = reader.ReadInt32();
            this.m_Index = reader.ReadInt32();
            this.m_ArraySize = reader.ReadInt32();
            this.m_Type = reader.ReadSByte();
            this.m_RowCount = reader.ReadSByte();
            reader.AlignStream();
        }
    }

    public class TextureParameter
    {
        public int m_NameIndex;
        public int m_Index;
        public int m_SamplerIndex;
        public sbyte m_Dim;

        public TextureParameter(ObjectReader reader)
        {
            int[] version = reader.version;

            this.m_NameIndex = reader.ReadInt32();
            this.m_Index = reader.ReadInt32();
            this.m_SamplerIndex = reader.ReadInt32();
            if (version[0] > 2017 || version[0] == 2017 && version[1] >= 3) //2017.3 and up
            {
                bool m_MultiSampled = reader.ReadBoolean();
            }
            this.m_Dim = reader.ReadSByte();
            reader.AlignStream();
        }
    }

    public class BufferBinding
    {
        public int m_NameIndex;
        public int m_Index;

        public BufferBinding(BinaryReader reader)
        {
            this.m_NameIndex = reader.ReadInt32();
            this.m_Index = reader.ReadInt32();
        }
    }

    public class ConstantBuffer
    {
        public int m_NameIndex;
        public List<MatrixParameter> m_MatrixParams;
        public List<VectorParameter> m_VectorParams;
        public List<StructParameter> m_StructParams;
        public int m_Size;

        public ConstantBuffer(ObjectReader reader)
        {
            int[] version = reader.version;

            this.m_NameIndex = reader.ReadInt32();

            int numMatrixParams = reader.ReadInt32();
            this.m_MatrixParams = new List<MatrixParameter>(numMatrixParams);
            for (var i = 0; i < numMatrixParams; i++)
            {
                this.m_MatrixParams.Add(new MatrixParameter(reader));
            }

            int numVectorParams = reader.ReadInt32();
            this.m_VectorParams = new List<VectorParameter>(numVectorParams);
            for (var i = 0; i < numVectorParams; i++)
            {
                this.m_VectorParams.Add(new VectorParameter(reader));
            }
            if (version[0] > 2017 || version[0] == 2017 && version[1] >= 3) //2017.3 and up
            {
                int numStructParams = reader.ReadInt32();
                this.m_StructParams = new List<StructParameter>(numStructParams);
                for (var i = 0; i < numStructParams; i++)
                {
                    this.m_StructParams.Add(new StructParameter(reader));
                }
            }
            this.m_Size = reader.ReadInt32();
        }
    }

    public class UAVParameter
    {
        public int m_NameIndex;
        public int m_Index;
        public int m_OriginalIndex;

        public UAVParameter(BinaryReader reader)
        {
            this.m_NameIndex = reader.ReadInt32();
            this.m_Index = reader.ReadInt32();
            this.m_OriginalIndex = reader.ReadInt32();
        }
    }

    public enum ShaderGpuProgramType
    {
        kShaderGpuProgramUnknown = 0,
        kShaderGpuProgramGLLegacy = 1,
        kShaderGpuProgramGLES31AEP = 2,
        kShaderGpuProgramGLES31 = 3,
        kShaderGpuProgramGLES3 = 4,
        kShaderGpuProgramGLES = 5,
        kShaderGpuProgramGLCore32 = 6,
        kShaderGpuProgramGLCore41 = 7,
        kShaderGpuProgramGLCore43 = 8,
        kShaderGpuProgramDX9VertexSM20 = 9,
        kShaderGpuProgramDX9VertexSM30 = 10,
        kShaderGpuProgramDX9PixelSM20 = 11,
        kShaderGpuProgramDX9PixelSM30 = 12,
        kShaderGpuProgramDX10Level9Vertex = 13,
        kShaderGpuProgramDX10Level9Pixel = 14,
        kShaderGpuProgramDX11VertexSM40 = 15,
        kShaderGpuProgramDX11VertexSM50 = 16,
        kShaderGpuProgramDX11PixelSM40 = 17,
        kShaderGpuProgramDX11PixelSM50 = 18,
        kShaderGpuProgramDX11GeometrySM40 = 19,
        kShaderGpuProgramDX11GeometrySM50 = 20,
        kShaderGpuProgramDX11HullSM50 = 21,
        kShaderGpuProgramDX11DomainSM50 = 22,
        kShaderGpuProgramMetalVS = 23,
        kShaderGpuProgramMetalFS = 24,
        kShaderGpuProgramSPIRV = 25,
        kShaderGpuProgramConsole = 26,
    };

    public class SerializedSubProgram
    {
        public uint m_BlobIndex;
        public ParserBindChannels m_Channels;
        public List<ushort> m_KeywordIndices;
        public sbyte m_ShaderHardwareTier;
        public ShaderGpuProgramType m_GpuProgramType;
        public List<VectorParameter> m_VectorParams;
        public List<MatrixParameter> m_MatrixParams;
        public List<TextureParameter> m_TextureParams;
        public List<BufferBinding> m_BufferParams;
        public List<ConstantBuffer> m_ConstantBuffers;
        public List<BufferBinding> m_ConstantBufferBindings;
        public List<UAVParameter> m_UAVParams;
        public List<SamplerParameter> m_Samplers;

        public SerializedSubProgram(ObjectReader reader)
        {
            int[] version = reader.version;

            this.m_BlobIndex = reader.ReadUInt32();
            this.m_Channels = new ParserBindChannels(reader);

            int numIndices = reader.ReadInt32();
            this.m_KeywordIndices = new List<ushort>(numIndices);
            for (var i = 0; i < numIndices; i++)
            {
                this.m_KeywordIndices.Add(reader.ReadUInt16());
            }
            if (version[0] >= 2017) //2017 and up
            {
                reader.AlignStream();
            }
            this.m_ShaderHardwareTier = reader.ReadSByte();
            this.m_GpuProgramType = (ShaderGpuProgramType) reader.ReadSByte();
            reader.AlignStream();

            int numVectorParams = reader.ReadInt32();
            this.m_VectorParams = new List<VectorParameter>(numVectorParams);
            for (var i = 0; i < numVectorParams; i++)
            {
                this.m_VectorParams.Add(new VectorParameter(reader));
            }

            int numMatrixParams = reader.ReadInt32();
            this.m_MatrixParams = new List<MatrixParameter>(numMatrixParams);
            for (var i = 0; i < numMatrixParams; i++)
            {
                this.m_MatrixParams.Add(new MatrixParameter(reader));
            }

            int numTextureParams = reader.ReadInt32();
            this.m_TextureParams = new List<TextureParameter>(numTextureParams);
            for (var i = 0; i < numTextureParams; i++)
            {
                this.m_TextureParams.Add(new TextureParameter(reader));
            }

            int numBufferParams = reader.ReadInt32();
            this.m_BufferParams = new List<BufferBinding>(numBufferParams);
            for (var i = 0; i < numBufferParams; i++)
            {
                this.m_BufferParams.Add(new BufferBinding(reader));
            }

            int numConstantBuffers = reader.ReadInt32();
            this.m_ConstantBuffers = new List<ConstantBuffer>(numConstantBuffers);
            for (var i = 0; i < numConstantBuffers; i++)
            {
                this.m_ConstantBuffers.Add(new ConstantBuffer(reader));
            }

            int numConstantBufferBindings = reader.ReadInt32();
            this.m_ConstantBufferBindings = new List<BufferBinding>(numConstantBufferBindings);
            for (var i = 0; i < numConstantBufferBindings; i++)
            {
                this.m_ConstantBufferBindings.Add(new BufferBinding(reader));
            }

            int numUAVParams = reader.ReadInt32();
            this.m_UAVParams = new List<UAVParameter>(numUAVParams);
            for (var i = 0; i < numUAVParams; i++)
            {
                this.m_UAVParams.Add(new UAVParameter(reader));
            }

            if (version[0] >= 2017) //2017 and up
            {
                int numSamplers = reader.ReadInt32();
                this.m_Samplers = new List<SamplerParameter>(numSamplers);
                for (var i = 0; i < numSamplers; i++)
                {
                    this.m_Samplers.Add(new SamplerParameter(reader));
                }
            }
            if (version[0] > 2017 || version[0] == 2017 && version[1] >= 2) //2017.2 and up
            {
                int m_ShaderRequirements = reader.ReadInt32();
            }
        }
    }

    public class SerializedProgram
    {
        public List<SerializedSubProgram> m_SubPrograms;

        public SerializedProgram(ObjectReader reader)
        {
            int numSubPrograms = reader.ReadInt32();
            this.m_SubPrograms = new List<SerializedSubProgram>(numSubPrograms);
            for (var i = 0; i < numSubPrograms; i++)
            {
                this.m_SubPrograms.Add(new SerializedSubProgram(reader));
            }
        }
    }

    public enum PassType
    {
        kPassTypeNormal = 0,
        kPassTypeUse = 1,
        kPassTypeGrab = 2
    };

    public class SerializedPass
    {
        public List<KeyValuePair<string, int>> m_NameIndices;
        public PassType m_Type;
        public SerializedShaderState m_State;
        public uint m_ProgramMask;
        public SerializedProgram progVertex;
        public SerializedProgram progFragment;
        public SerializedProgram progGeometry;
        public SerializedProgram progHull;
        public SerializedProgram progDomain;
        public bool m_HasInstancingVariant;
        public string m_UseName;
        public string m_Name;
        public string m_TextureName;
        public SerializedTagMap m_Tags;

        public SerializedPass(ObjectReader reader)
        {
            int[] version = reader.version;

            int numIndices = reader.ReadInt32();
            this.m_NameIndices = new List<KeyValuePair<string, int>>(numIndices);
            for (var i = 0; i < numIndices; i++)
            {
                this.m_NameIndices.Add(new KeyValuePair<string, int>(reader.ReadAlignedString(), reader.ReadInt32()));
            }

            this.m_Type = (PassType) reader.ReadInt32();
            this.m_State = new SerializedShaderState(reader);
            this.m_ProgramMask = reader.ReadUInt32();
            this.progVertex = new SerializedProgram(reader);
            this.progFragment = new SerializedProgram(reader);
            this.progGeometry = new SerializedProgram(reader);
            this.progHull = new SerializedProgram(reader);
            this.progDomain = new SerializedProgram(reader);
            this.m_HasInstancingVariant = reader.ReadBoolean();
            if (version[0] >= 2018) //2018 and up
            {
                bool m_HasProceduralInstancingVariant = reader.ReadBoolean();
            }
            reader.AlignStream();
            this.m_UseName = reader.ReadAlignedString();
            this.m_Name = reader.ReadAlignedString();
            this.m_TextureName = reader.ReadAlignedString();
            this.m_Tags = new SerializedTagMap(reader);
        }
    }

    public class SerializedTagMap
    {
        public List<KeyValuePair<string, string>> tags;

        public SerializedTagMap(BinaryReader reader)
        {
            int numTags = reader.ReadInt32();
            this.tags = new List<KeyValuePair<string, string>>(numTags);
            for (var i = 0; i < numTags; i++)
            {
                this.tags.Add(new KeyValuePair<string, string>(reader.ReadAlignedString(), reader.ReadAlignedString()));
            }
        }
    }

    public class SerializedSubShader
    {
        public List<SerializedPass> m_Passes;
        public SerializedTagMap m_Tags;
        public int m_LOD;

        public SerializedSubShader(ObjectReader reader)
        {
            int numPasses = reader.ReadInt32();
            this.m_Passes = new List<SerializedPass>(numPasses);
            for (var i = 0; i < numPasses; i++)
            {
                this.m_Passes.Add(new SerializedPass(reader));
            }

            this.m_Tags = new SerializedTagMap(reader);
            this.m_LOD = reader.ReadInt32();
        }
    }

    public class SerializedShaderDependency
    {
        public string from;
        public string to;

        public SerializedShaderDependency(BinaryReader reader)
        {
            this.from = reader.ReadAlignedString();
            this.to = reader.ReadAlignedString();
        }
    }

    public class SerializedShader
    {
        public SerializedProperties m_PropInfo;
        public List<SerializedSubShader> m_SubShaders;
        public string m_Name;
        public string m_CustomEditorName;
        public string m_FallbackName;
        public List<SerializedShaderDependency> m_Dependencies;
        public bool m_DisableNoSubshadersMessage;

        public SerializedShader(ObjectReader reader)
        {
            this.m_PropInfo = new SerializedProperties(reader);

            int numSubShaders = reader.ReadInt32();
            this.m_SubShaders = new List<SerializedSubShader>(numSubShaders);
            for (var i = 0; i < numSubShaders; i++)
            {
                this.m_SubShaders.Add(new SerializedSubShader(reader));
            }

            this.m_Name = reader.ReadAlignedString();
            this.m_CustomEditorName = reader.ReadAlignedString();
            this.m_FallbackName = reader.ReadAlignedString();

            int numDependencies = reader.ReadInt32();
            this.m_Dependencies = new List<SerializedShaderDependency>(numDependencies);
            for (var i = 0; i < numDependencies; i++)
            {
                this.m_Dependencies.Add(new SerializedShaderDependency(reader));
            }

            this.m_DisableNoSubshadersMessage = reader.ReadBoolean();
            reader.AlignStream();
        }
    }

    public class Shader : NamedObject
    {
        public byte[] m_Script;

        //5.3 - 5.4
        public uint decompressedSize;

        public byte[] m_SubProgramBlob;

        //5.5 and up
        public SerializedShader m_ParsedForm;
        public List<uint> platforms;
        public List<uint> offsets;
        public List<uint> compressedLengths;
        public List<uint> decompressedLengths;
        public byte[] compressedBlob;

        public Shader(ObjectReader reader) : base(reader)
        {
            if (this.version[0] == 5 && this.version[1] >= 5 || this.version[0] > 5) //5.5 and up
            {
                this.m_ParsedForm = new SerializedShader(reader);
                int numPlatforms = reader.ReadInt32();
                this.platforms = new List<uint>(numPlatforms);
                for (var i = 0; i < numPlatforms; i++)
                {
                    this.platforms.Add(reader.ReadUInt32());
                }

                int numOffsets = reader.ReadInt32();
                this.offsets = new List<uint>(numOffsets);
                for (var i = 0; i < numOffsets; i++)
                {
                    this.offsets.Add(reader.ReadUInt32());
                }

                int numCompressedLengths = reader.ReadInt32();
                this.compressedLengths = new List<uint>(numCompressedLengths);
                for (var i = 0; i < numCompressedLengths; i++)
                {
                    this.compressedLengths.Add(reader.ReadUInt32());
                }

                int numDecompressedLengths = reader.ReadInt32();
                this.decompressedLengths = new List<uint>(numDecompressedLengths);
                for (var i = 0; i < numDecompressedLengths; i++)
                {
                    this.decompressedLengths.Add(reader.ReadUInt32());
                }

                this.compressedBlob = reader.ReadBytes(reader.ReadInt32());
            }
            else
            {
                this.m_Script = reader.ReadBytes(reader.ReadInt32());
                reader.AlignStream();
                string m_PathName = reader.ReadAlignedString();
                if (this.version[0] == 5 && this.version[1] >= 3) //5.3 - 5.4
                {
                    this.decompressedSize = reader.ReadUInt32();
                    this.m_SubProgramBlob = reader.ReadBytes(reader.ReadInt32());
                }
            }
        }
    }
}