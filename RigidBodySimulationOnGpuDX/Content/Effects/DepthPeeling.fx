#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_5_0
	#define PS_SHADERMODEL ps_5_0
#endif

sampler2D DepthFrontLayer = sampler_state
{
    Filter = Point;
};
sampler2D DepthBackLayer = sampler_state
{
    Filter = Point;
};

matrix WorldViewProjection;
float4 ModelSize;
float3 ModelMinBounds;
float4 GridSize;

// Depth layers.
struct DepthVertexShaderInput
{
	float4 Position : POSITION0;
};

struct DepthVertexShaderOutput
{
	float4 Position : SV_POSITION;
    float4 ClipSpacePosition : TEXCOORD1;
};

DepthVertexShaderOutput DepthLayerVertexShader(in DepthVertexShaderInput input)
{
	DepthVertexShaderOutput output = (DepthVertexShaderOutput)0;

	output.Position = mul(input.Position, WorldViewProjection);
    output.ClipSpacePosition = mul(input.Position, WorldViewProjection);

	return output;
}

float4 DepthLayerFragmentShader(DepthVertexShaderOutput input) : SV_Target
{
    float depth = input.ClipSpacePosition.z;
    return float4(depth, 0, 0, 1);
}

// Depth slice buffer.
struct SliceVertexShaderInput
{
    float4 Position : POSITION0;
    float2 Uv : TEXCOORD0;
};

struct SliceVertexShaderOutput
{
    float4 Position : SV_POSITION;
    float2 Uv : TEXCOORD0;
};

SliceVertexShaderOutput DepthSliceBufferVertexShader(in SliceVertexShaderInput input)
{
    SliceVertexShaderOutput output = (SliceVertexShaderOutput) 0;

    output.Position = input.Position;
    output.Uv = input.Uv;

    return output;
}

float4 DepthSliceBufferFragmentShader(SliceVertexShaderOutput input) : SV_Target
{
    float2 flatGridSize = float2(GridSize.w, ceil(GridSize.z / GridSize.w));
    float2 position = input.Uv * flatGridSize.xy;
    
    int3 voxelIndex = int3(frac(position) * GridSize.xy, int(position.y) * flatGridSize.x + int(position.x));
    if (voxelIndex.z >= GridSize.z)
        return float4(0, 0, 0, 0);
    
    float particleDiameter = ModelSize.w;
    float3 modelCenter = ModelMinBounds + ModelSize.xyz / 2;
    
    float3 voxelPosition = voxelIndex * particleDiameter + particleDiameter / 2 - GridSize.xyz * particleDiameter / 2 + modelCenter;
    
    float3 maxBounds = ModelMinBounds + ModelSize.xyz;
    
    if (any(voxelPosition < ModelMinBounds || voxelPosition > maxBounds))
        return float4(0, 0, 0, 0);
    
    float2 uv = (voxelPosition.xy - ModelMinBounds.xy) / ModelSize.xy;
    uv.y = 1 - uv.y;
    
    float depthFront = tex2D(DepthFrontLayer, uv).r;
    float depthBack = tex2D(DepthBackLayer, uv).r;
    float voxelDepth = 1 - (voxelPosition.z - ModelMinBounds.z) / ModelSize.z;
    
    if (voxelDepth > depthFront && voxelDepth < depthBack)
        return float4(voxelPosition, 1);
    
    return float4(0, 0, 0, 0);
}

technique BasicColorDrawing
{
	pass P0
	{
        ZFunc = Less;
        CullMode = CCW;

        VertexShader = compile VS_SHADERMODEL DepthLayerVertexShader();
        PixelShader = compile PS_SHADERMODEL DepthLayerFragmentShader();
    }
    pass P1
    {
        ZFunc = Greater;
        CullMode = CW;

        VertexShader = compile VS_SHADERMODEL DepthLayerVertexShader();
        PixelShader = compile PS_SHADERMODEL DepthLayerFragmentShader();
    }
    pass P3
    {
        ZFunc = Less;
        CullMode = CCW;

        VertexShader = compile VS_SHADERMODEL DepthSliceBufferVertexShader();
        PixelShader = compile PS_SHADERMODEL DepthSliceBufferFragmentShader();
    }
};