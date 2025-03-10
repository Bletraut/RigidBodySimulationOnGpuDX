#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_5_0
	#define PS_SHADERMODEL ps_5_0
#endif

Texture2D ParticleWorldPositions;

// x = GridSize, y = ParticleBufferSize, z = ParticleRadius, w = GridTextureWidth.
float4 GridValues;

struct VertexShaderInput
{
	float4 Position : POSITION0;
    uint InstanceId : SV_InstanceID;
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
    uint InstanceId : TEXCOORD0;
};

VertexShaderOutput MainVS(in VertexShaderInput input)
{	
	VertexShaderOutput output = (VertexShaderOutput)0;

    int2 particleIndex = int2(input.InstanceId % GridValues.y, input.InstanceId / GridValues.y);
    float4 instancePosition = ParticleWorldPositions.Load(int3(particleIndex, 0));
	
    float3 smallestGridCorner = -GridValues.xxx * GridValues.z;
    int3 voxelIndex = (instancePosition.xyz - smallestGridCorner) / (GridValues.z * 2);
	
    float d = floor(GridValues.w / GridValues.x);
    float2 voxelUv = voxelIndex.xy + GridValues.xx * int2(voxelIndex.z % d, voxelIndex.z / d);
    voxelUv /= GridValues.w;
	
    float4 position = float4(voxelUv * 2 - 1, input.InstanceId / (GridValues.y * GridValues.y), 1);	
    output.Position = position;
    output.InstanceId = input.InstanceId + 1;

	return output;
}

float4 MainPS(VertexShaderOutput input) : SV_Target
{
    return float4(input.InstanceId, input.InstanceId, input.InstanceId, input.InstanceId);
}

technique BasicColorDrawing
{
	pass P0
	{
        ZFunc = Less;
        ColorWriteEnable = Red;

		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
    pass P1
    {
        ZFunc = Greater;

        StencilEnable = true;
        StencilFunc = Greater;
        StencilPass = Incr;
        StencilRef = 1;

        ColorWriteEnable = Green;

        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
    pass P2
    {
        ColorWriteEnable = Blue;

        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
    pass P3
    {
        ColorWriteEnable = Alpha;

        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};