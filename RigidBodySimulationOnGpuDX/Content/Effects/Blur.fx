#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_5_0
	#define PS_SHADERMODEL ps_5_0
#endif

#define MaxKernelSize 32

sampler2D Source;
float2 TexelSize;

float2 Direction;

int KernelSize;
float KernelWeights[MaxKernelSize];

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

float4 BoxBlurPS(VertexShaderOutput input) : SV_Target
{
    float4 color = tex2D(Source, input.Uv)
		+ tex2D(Source, input.Uv + Direction * TexelSize)
		+ tex2D(Source, input.Uv - Direction * TexelSize);
    color /= 3;
	
    return color;

}

float4 GaussianBlurPS(VertexShaderOutput input) : SV_Target
{
    float4 color = tex2D(Source, input.Uv) * KernelWeights[0];

    for (int i = 1; i < KernelSize; ++i)
    {
        float2 offset = Direction * i * TexelSize;
        color += tex2D(Source, input.Uv + offset) * KernelWeights[i];
        color += tex2D(Source, input.Uv - offset) * KernelWeights[i];
    }
	
    return color;
}

technique BasicColorDrawing
{
	pass BoxBlur
	{
        CullMode = CCW;

		VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL BoxBlurPS();
    }
    pass GaussianBlur
    {
        CullMode = CCW;

        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL GaussianBlurPS();
    }
};