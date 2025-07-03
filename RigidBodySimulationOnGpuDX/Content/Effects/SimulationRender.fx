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

float3 LightDirection;
matrix LightViewProjection;

sampler2D ShadowMap = sampler_state
{
    Filter = Linear;
    AddressU = Border;
    AddressV = Border;
};
float3 ShadowColor;
// x - Sharpness
float4 ShadowMapValues;

matrix Model;
matrix ViewProjection;

struct VertexShaderInput
{
	float4 Position : POSITION0;
    float4 Normal : NORMAL0;
    float2 Uv : TEXCOORD0;
};

struct InstanceInput
{
    float2 BodyIndex : TEXCOORD2;
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
    float4 Normal : NORMAL0;
    float2 Uv : TEXCOORD0;
    float4 LightSpacePosition : TEXCOORD1;
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
    output.Uv = input.Uv;
    output.LightSpacePosition = mul(position, LightViewProjection);

    return output;
}

VertexShaderOutput MainVS(in VertexShaderInput input)
{
    VertexShaderOutput output = (VertexShaderOutput) 0;
	
    float4 worldPosition = mul(input.Position, Model);
    
    output.Position = mul(worldPosition, ViewProjection);
    output.Normal = normalize(mul(input.Normal, Model));
    output.Uv = input.Uv;
    output.LightSpacePosition = mul(worldPosition, LightViewProjection);

    return output;
}

float3 GetShadowValues(float4 lightSpacePosition)
{
    float3 projectedPosition = lightSpacePosition.xyz / lightSpacePosition.w;
    float2 shadowUv = projectedPosition.xy * 0.5 + 0.5;
    shadowUv.y = 1 - shadowUv.y;
    
    return float3(shadowUv, projectedPosition.z);
}

float SampleShadowMap(sampler2D shadowMap, float2 shadowUv, float currentDepth)
{
    float2 shadowValues = tex2D(shadowMap, shadowUv).rg;
    
    float scale = max(0, currentDepth - shadowValues.x);
    float shadowFactor = shadowValues.x * (1 - smoothstep(0, ShadowMapValues.x, scale));
    
    return shadowFactor;
}

float3 ComputeDiffuse(float2 uv, float3 normal, float shadowFactor)
{
    float3 lightDirection = normalize(LightDirection);
    float3 lightColor = float3(1.35, 1.35, 1.35);
    
    float lightFactor = dot(lightDirection, normal.xyz);
    
    float3 shadowColor = ShadowColor * (1 - shadowFactor);
    
    float3 diffuseColor = tex2D(BaseColor, uv).xyz;
    diffuseColor *= 1 - shadowColor;
    
    float3 resultColor = diffuseColor * lightColor * (lightFactor * 0.5 + 0.5);
    
    return resultColor;
}

float4 ShadowPS(VertexShaderOutput input) : SV_Target
{
    float depth = input.Position.z / input.Position.w;
    return float4(depth, 0, 0, 0);
}

float4 ShadowMapCompare_PS(VertexShaderOutput input) : SV_Target
{
    float3 shadowValues = GetShadowValues(input.LightSpacePosition);
    float shadowFactor = SampleShadowMap(ShadowMap, shadowValues.xy, shadowValues.z);
    
    float3 resultColor = ComputeDiffuse(input.Uv, input.Normal.xyz, shadowFactor);
    return float4(resultColor, 1);
}

float4 ShadowMap_PS(VertexShaderOutput input) : SV_Target
{
    float3 shadowValues = GetShadowValues(input.LightSpacePosition);
    float shadowFactor = tex2D(ShadowMap, shadowValues.xy).g;
    
    float3 resultColor = ComputeDiffuse(input.Uv, input.Normal.xyz, shadowFactor);
    return float4(resultColor, 1);
}

float4 NoShadows_PS(VertexShaderOutput input) : SV_Target
{
    float3 resultColor = ComputeDiffuse(input.Uv, input.Normal.xyz, 1);
    return float4(resultColor, 1);
}

technique BasicColorDrawing
{
    // Shadow pass.
    pass ShadowPass
    {
        CullMode = CW;

        VertexShader = compile VS_SHADERMODEL InstanceVS();
        PixelShader = compile PS_SHADERMODEL ShadowPS();
    }

    // Shadows.
    pass InstanceShadows
    {
        VertexShader = compile VS_SHADERMODEL InstanceVS();
        PixelShader = compile PS_SHADERMODEL ShadowMapCompare_PS();
    }
    pass ModelShadows
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL ShadowMap_PS();
    }

    // No shadows.
    pass InstanceNoShadows
    {
        VertexShader = compile VS_SHADERMODEL InstanceVS();
        PixelShader = compile PS_SHADERMODEL NoShadows_PS();
    }
    pass ModelNoShadows
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL NoShadows_PS();
    }
};