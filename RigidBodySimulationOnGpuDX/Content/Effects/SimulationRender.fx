#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_5_0
	#define PS_SHADERMODEL ps_5_0
#endif

#define MinBias 0.01
#define MaxBias 0.1

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
// x - Min Variance, y - Light Bleeding Reduction
float4 ShadowMapValues;

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
    output.Uv1 = input.Uv1;
    output.LightSpacePosition = mul(position, LightViewProjection);

    return output;
}

VertexShaderOutput MainVS(in VertexShaderInput input)
{
    VertexShaderOutput output = (VertexShaderOutput) 0;
	
    float4 worldPosition = mul(input.Position, Model);
    
    output.Position = mul(worldPosition, ViewProjection);
    output.Normal = normalize(mul(input.Normal, Model));
    output.Uv1 = input.Uv1;
    output.LightSpacePosition = mul(worldPosition, LightViewProjection);

    return output;
}

float linstep(float min, float max, float value)
{
    return saturate((value - min) / (max - min));
}

float SampleVarianceShadowMap(sampler2D shadowMap, float2 shadowUv, float currentDepth,
    float minVariance, float lightBleedingReduction)
{
    float2 moments = tex2D(shadowMap, shadowUv);
    
    float p = currentDepth <= moments.x;
    float variance = max(moments.y - (moments.x * moments.x), minVariance);
    
    float d = currentDepth - moments.x;
    float pMax = linstep(lightBleedingReduction, 1, variance / (variance + d * d));
    
    return max(p, pMax);
}

float4 MainPS(VertexShaderOutput input) : SV_Target
{
    float3 lightDirection = normalize(LightDirection);
    float3 lightColor = float3(1.35, 1.35, 1.35);
    
    float lightFactor = dot(lightDirection, input.Normal.xyz);
    
    float3 projectedPosition = input.LightSpacePosition.xyz / input.LightSpacePosition.w;
    float2 shadowUv = projectedPosition.xy * 0.5 + 0.5;
    shadowUv.y = 1 - shadowUv.y;
    
    float shadowFactor = SampleVarianceShadowMap(ShadowMap, shadowUv, projectedPosition.z,
        ShadowMapValues.x, ShadowMapValues.y);
    
    float3 shadowColor = float3(0.35, 0.35, 0.35) * (1 - shadowFactor);
    float3 diffuseColor = tex2D(BaseColor, input.Uv1).xyz;
    diffuseColor *= 1 - shadowColor;
    
    float3 resultColor = diffuseColor * lightColor * (lightFactor * 0.5 + 0.5);
    
    return float4(resultColor, 1);
}

float4 ShadowPS(VertexShaderOutput input) : SV_Target
{
    float depth = input.Position.z / input.Position.w;
    
    float dx = ddx(depth);
    float dy = ddy(depth);
    float moment2 = depth * depth + 0.25 * (dx * dx + dy * dy);
    
    float2 moments = float2(depth, moment2);
    
    return float4(moments, 0, 1);
}

technique BasicColorDrawing
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL InstanceVS();
        PixelShader = compile PS_SHADERMODEL ShadowPS();
    }
    pass P1
    {
        VertexShader = compile VS_SHADERMODEL InstanceVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
    pass P2
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};