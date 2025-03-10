#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_5_0
	#define PS_SHADERMODEL ps_5_0
#endif

Texture2D BodiesParticles;
Texture2D BodiesPositions;
Texture2D BodiesRotations;
Texture2D BodiesLinearMomenta;
Texture2D BodiesAngularVelocities;
sampler2D ParticlePositions;
float BodiesBufferSize;

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

struct FragmentShaderOutput
{
    float4 WorldPosition : SV_Target0;
    float4 Velocity : SV_Target1;
};

float4 RotateByQuaternion(float4 v, float4 q)
{
    float3 t = 2.0 * cross(q.xyz, v.xyz);
    v.xyz += q.w * t + cross(q.xyz, t);
	
    return v;
}

float3x3 QuaternionToMatrix(float4 q)
{
    float3 v = q.xyz;
    float s = q.w;
	
    float3x3 m =
    {
        { 1 - 2 * v.y * v.y - 2 * v.z * v.z, 2 * v.x * v.y - 2 * s * v.z, 2 * v.x * v.z + 2 * s * v.y },
        { 2 * v.x * v.y + 2 * s * v.z, 1 - 2 * v.x * v.x - 2 * v.z * v.z, 2 * v.y * v.z - 2 * s * v.x },
        { 2 * v.x * v.z - 2 * s * v.y, 2 * v.y * v.z + 2 * s * v.x, 1 - 2 * v.x * v.x - 2 * v.y * v.y }
    };
	
    return m;
};

FragmentShaderOutput MainPS(VertexShaderOutput input)
{
    FragmentShaderOutput output = (FragmentShaderOutput)0;
	
    float2 uv = float2(input.Uv.x, 1 - input.Uv.y);
    float4 position = tex2D(ParticlePositions, uv);
    float bodyIndex = position.w - 1;
	if (bodyIndex < 0)
        discard;
	
    int3 bodyGridIndex = int3(bodyIndex % BodiesBufferSize, bodyIndex / BodiesBufferSize, 0);
    float4 bodyParticle = BodiesParticles.Load(bodyGridIndex);
    float4 bodyPosition = BodiesPositions.Load(bodyGridIndex);
    float4 bodyRotation = BodiesRotations.Load(bodyGridIndex);
    float4 bodyLinearMomentum = BodiesLinearMomenta.Load(bodyGridIndex);
    float4 bodyAngularVelocity = BodiesAngularVelocities.Load(bodyGridIndex);
    float inverseBodyMass = bodyParticle.w;

    float4 relativePosition = RotateByQuaternion(position, bodyRotation);
    float3 velocity = bodyLinearMomentum.xyz * inverseBodyMass + cross(bodyAngularVelocity.xyz, relativePosition.xyz);
    
    output.WorldPosition = float4(bodyPosition.xyz + relativePosition.xyz, position.w);
    output.Velocity = float4(velocity, 1);
	
    return output;
}

technique BasicColorDrawing
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};