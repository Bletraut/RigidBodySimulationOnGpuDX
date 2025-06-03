#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_5_0
	#define PS_SHADERMODEL ps_5_0
#endif

Matrix InverseViewProjection;

struct VertexShaderInput
{
	float4 Position : POSITION0;
	float2 Uv : TEXCOORD0;
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float2 Uv : TEXCOORD0;
    float3 ViewDirection : TEXCOORD1;
};

VertexShaderOutput MainVS(in VertexShaderInput input)
{
	VertexShaderOutput output = (VertexShaderOutput)0;
    
    float4 screenPosition = float4(input.Uv * 2 - 1, 1, 1);
    float4 worldPosition = mul(screenPosition, InverseViewProjection);
	
    float4 position = input.Position;
    position.z = 1;
	
    output.Position = position;
    output.Uv = input.Uv;
    output.ViewDirection = normalize(worldPosition.xyz);

	return output;
}

float4 MainPS(VertexShaderOutput input) : SV_Target
{
    float gradient = input.ViewDirection.y * 0.5 + 0.5;
	
    float3 skyColor = float3(0, 0.58, 1);
    float3 horizontColor = float3(0.85, 0.99, 1);
    float3 groundColor = float3(0.3, 0.29, 0.3);
	
    float horizontHeight = 0.5;
    float skyHorizontSize = 0.15;
    float groundHorizontSize = 0.025;
	
    float skyToHorizont = smoothstep(horizontHeight, horizontHeight + skyHorizontSize, gradient);
    float horizontToGround = smoothstep(horizontHeight - groundHorizontSize, horizontHeight, gradient);
	
    float3 resultColor = lerp(horizontColor, skyColor, skyToHorizont) * step(horizontHeight, gradient);
    resultColor += lerp(groundColor, horizontColor, horizontToGround) * step(gradient, horizontHeight);
	
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