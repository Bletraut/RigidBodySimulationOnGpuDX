#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_5_0
	#define PS_SHADERMODEL ps_5_0
#endif

sampler2D Source;
float2 TexelSize;

struct VertexShaderInput
{
	float4 Position : POSITION0;
    float2 Uv : TEXCOORD0;
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
    float2 Uv : TEXCOORD0;
};

VertexShaderOutput MainVS(in VertexShaderInput input)
{
	VertexShaderOutput output = (VertexShaderOutput)0;

    output.Position = input.Position;
    output.Uv = input.Uv;

	return output;
}

float4 LineBlur(float2 uv, float2 offset)
{
    float2 result = tex2D(Source, uv).rg
		+ tex2D(Source, uv + offset).rg
		+ tex2D(Source, uv - offset).rg;
    result /= 3;
	
    return float4(result, 0, 1);
}

float4 BlurX(VertexShaderOutput input) : SV_Target
{    
    float2 offset = float2(TexelSize.x, 0);
    return LineBlur(input.Uv, offset);
}
float4 BlurY(VertexShaderOutput input) : SV_Target
{
    float2 offset = float2(0, TexelSize.y);
    return LineBlur(input.Uv, offset);
}

technique BasicColorDrawing
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL BlurX();
    }
    pass P1
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL BlurY();
    }
};