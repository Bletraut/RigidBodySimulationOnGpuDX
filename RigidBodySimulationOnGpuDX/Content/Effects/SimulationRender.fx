#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_5_0
	#define PS_SHADERMODEL ps_5_0
#endif

Texture2D BodiesPositions;
Texture2D BodiesRotations;
sampler2D BaseColor;
float4 CenterOfMass;

matrix Model;
matrix ViewProjection;

struct VertexShaderInput
{
	float4 Position : POSITION0;
    float4 Normal : NORMAL0;
    float2 Uv1 : TEXCOORD0;
};

struct InstanceInput
{
    float2 BodyIndex : TEXCOORD2;
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
    float4 Normal : NORMAL0;
    float2 Uv1 : TEXCOORD0;
};

float4 RotateByQuaternion(float4 v, float4 q)
{
    float3 t = 2.0 * cross(q.xyz, v.xyz);
    v.xyz += q.w * t + cross(q.xyz, t);
	
    return v;
}

VertexShaderOutput InstanceVS(in VertexShaderInput input, InstanceInput instanceInput)
{
    VertexShaderOutput output = (VertexShaderOutput)0;
	
    float4 instancePosition = BodiesPositions.Load(int3(instanceInput.BodyIndex, 0));
    float4 instanceRotation = BodiesRotations.Load(int3(instanceInput.BodyIndex, 0));
	
    float4 rotatedPosition = RotateByQuaternion(input.Position - CenterOfMass, instanceRotation);
    float4 position = rotatedPosition + instancePosition;
	
    output.Position = mul(position, ViewProjection);
    output.Normal = normalize(RotateByQuaternion(input.Normal, instanceRotation));
    output.Uv1 = input.Uv1;

    return output;
}

VertexShaderOutput MainVS(in VertexShaderInput input)
{
    VertexShaderOutput output = (VertexShaderOutput) 0;
		
    output.Position = mul(input.Position, mul(Model, ViewProjection));
    output.Normal = normalize(mul(input.Normal, Model));
    output.Uv1 = input.Uv1;

    return output;
}

float4 MainPS(VertexShaderOutput input) : SV_Target
{
    float3 lightDirection = normalize(float3(2, 10, 2));
    float3 lightColor = float3(1.35, 1.35, 1.35);
    
    float3 diffuseColor = tex2D(BaseColor, input.Uv1).xyz;
    float3 resultColor = diffuseColor * lightColor * (dot(lightDirection, input.Normal.xyz) * 0.5 + 0.5);
    
    return float4(resultColor, 1);
}

technique BasicColorDrawing
{
	pass P0
	{
        ZEnable = True;
        ZFunc = Less;

		VertexShader = compile VS_SHADERMODEL InstanceVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
    pass P1
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};