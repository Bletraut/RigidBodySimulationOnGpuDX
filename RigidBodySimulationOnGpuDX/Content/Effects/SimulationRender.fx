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

//Texture2D ShadowMap;
//SamplerComparisonState ShadowMapSampler;
sampler2D ShadowMap = sampler_state
{
    Filter = Point;
    AddressU = Border;
    AddressV = Border;
};
// x,y = Texel Size
float4 ShadowMapValues;

float3 LightDirection;
matrix LightViewProjection;

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

float SampleShadow(float2 uv, float depth)
{
    float shadowMapDepth = tex2D(ShadowMap, uv).r;
    return step(shadowMapDepth, depth);
}

float SampleShadowPcf3x3(float2 uv, float depth)
{
    float shadow = 0;
    for (float x = -1; x <= 1; x++)
    {
        for (float y = -1; y <= 1; y++)
        {
            shadow += SampleShadow(uv + float2(x, y) * ShadowMapValues.xy, depth);
        }
    }
    return shadow / 9;
}

float CalculateShadow(float4 lightSpacePosition, float lightFactor)
{
    float3 projected = lightSpacePosition.xyz / lightSpacePosition.w;
    
    //float currentDepth = projected.z;
    float currentDepth = projected.z - max(0.0085 * (1 - lightFactor), 0.00005);
    
    float2 shadowUv = projected.xy * 0.5 + 0.5;
    shadowUv.y = 1 - shadowUv.y;
    
    float2 texelCenter = frac(shadowUv / ShadowMapValues.xy) - 0.5;
    float2 offset = 2 * float2(step(0, texelCenter)) - 1;
    offset *= ShadowMapValues.xy;
    
    float center = SampleShadowPcf3x3(shadowUv, currentDepth);
    float horizontal = SampleShadowPcf3x3(shadowUv + float2(offset.x, 0), currentDepth);
    float vertical = SampleShadowPcf3x3(shadowUv + float2(0, offset.y), currentDepth);
    float diagonal = SampleShadowPcf3x3(shadowUv + offset, currentDepth);
    
    float2 t = abs(texelCenter) / 0.5;
    
    float h = lerp(center, horizontal, 0.5);
    float v = lerp(center, vertical, 0.5);
    float m = (center + horizontal + vertical + diagonal) / 4;
    
    if (t.x > t.y)
    {
        float a = 1 - t.x;
        float b = t.y;
        float c = 1 - a - b;
		
        return h * c + center * a + m * b;
    }
    else
    {
        float a = t.x;
        float b = 1 - t.y;
        float c = 1 - a - b;
		
        return v * c + center * b + m * a;
    }
    
    //return SampleShadowPcf3x3(shadowUv, currentDepth);
    
    //float shadow = SampleShadowPcf3x3(shadowUv, currentDepth);
    //return shadow * frac(shadowUv / ShadowMapValues.xy).x;
    
    //float shadow = 0;
    //for (float x = -1; x <= 1; x++)
    //{
    //    for (float y = -1; y <= 1; y++)
    //    {
    //        shadow += ShadowMap.SampleCmp(ShadowMapSampler, shadowUv + float2(x, y) * ShadowMapValues.xy, currentDepth).r;
    //    }
    //}
    //return shadow / 9;
}

float4 MainPS(VertexShaderOutput input) : SV_Target
{
    float3 lightDirection = normalize(LightDirection);
    float3 lightColor = float3(1.35, 1.35, 1.35);
    
    float lightFactor = dot(lightDirection, input.Normal.xyz);
    float shadow = CalculateShadow(input.LightSpacePosition, lightFactor);

    // Temp
    //float3 projected = input.LightSpacePosition.xyz / input.LightSpacePosition.w;
    //float2 shadowUv = projected.xy * 0.5 + 0.5;
    //shadowUv.y = 1 - shadowUv.y;
    //float2 texelCenter = frac(shadowUv / ShadowMapValues.xy) - 0.5;
    //float currentDepth = projected.z - max(0.0085 * (1 - lightFactor), 0.00005);
    //if (SampleShadowPcf3x3(shadowUv, currentDepth) > 0 && any(abs(texelCenter) > 0.485))
    //    return float4(1, 0, 0, 1);
    // END
    
    float3 shadowColor = float3(0.35, 0.35, 0.35) * shadow;
    float3 diffuseColor = tex2D(BaseColor, input.Uv1).xyz;
    diffuseColor *= 1 - shadowColor;
    
    float3 resultColor = diffuseColor * lightColor * (lightFactor * 0.5 + 0.5);
    
    return float4(resultColor, 1);
}

float4 ShadowPS(VertexShaderOutput input) : SV_Target
{
    float depth = input.Position.z / input.Position.w;
    return float4(depth, 0, 0, 1);
}

technique BasicColorDrawing
{
    pass P0
    {
        //CullMode = CW;

        VertexShader = compile VS_SHADERMODEL InstanceVS();
        PixelShader = compile PS_SHADERMODEL ShadowPS();
    }
    pass P1
    {
        //CullMode = CCW;

        VertexShader = compile VS_SHADERMODEL InstanceVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
    pass P2
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};