#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_5_0
	#define PS_SHADERMODEL ps_5_0
#endif

Texture2D ParticleWorldPositions;
// x = ParticleRadius, y = ParticleBufferSize
float2 Values;

matrix ViewProjection;

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float4 Normal : NORMAL0;
    float2 Uv1 : TEXCOORD0;
    uint InstanceId : SV_InstanceID;
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
    float4 Normal : NORMAL0;
};

VertexShaderOutput MainVS(in VertexShaderInput input)
{
	VertexShaderOutput output = (VertexShaderOutput)0;

    int2 particleIndex = int2(input.InstanceId % Values.y, input.InstanceId / Values.y);
    float4 instancePosition = ParticleWorldPositions.Load(int3(particleIndex, 0));
	
    float4 position = instancePosition + input.Position * Values.x;
    position.w = 1;
	
	output.Position = mul(position, ViewProjection);
    output.Normal = input.Normal;

	return output;
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float3 lightDirection = normalize(float3(2, 10, 2));
    float3 lightColor = float3(1.2, 1.2, 1.2);
    
    float3 diffuseColor = float3(1, 1, 1);
    float3 resultColor = diffuseColor * lightColor * (dot(lightDirection, input.Normal.xyz) * 0.5 + 0.5);
    
    return float4(resultColor, 1);
}

technique BasicColorDrawing
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};