#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_5_0
	#define PS_SHADERMODEL ps_5_0
#endif

#define Gravity -9.81

Texture2D BodiesParticles;
Texture2D BodiesPositions;
Texture2D BodiesRotations;
Texture2D BodiesLinearMomenta;
Texture2D BodiesAngularMomenta;
Texture2D ParticleWorldPositions;
Texture2D ParticleForces;
// x = deltaTime, y = BodiesBufferSize, z = ParticleBufferSize, w = MaxMomenta
float4 Values;
float4x4 InverseInertialTensorArray[12];

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
    float4 Position : SV_Target0;
    float4 Rotation : SV_Target1;
    float4 LinearMomentum : SV_Target2;
    float4 AngularMomentum : SV_Target3;
    float4 AngularVelocity : SV_Target4;
};

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

float4 QuaternionMultiply(float4 q1, float4 q2)
{
    float4 result = float4(0, 0, 0, 0);
    result.xyz = q1.w * q2.xyz + q2.w * q1.xyz + cross(q1.xyz, q2.xyz);
    result.w = q1.w * q2.w - dot(q1.xyz, q2.xyz);
    
    return result;
}

FragmentShaderOutput MainPS(VertexShaderOutput input)
{
    FragmentShaderOutput output = (FragmentShaderOutput)0;
	
    int3 bodyIndex = float3(input.Uv.x, 1 - input.Uv.y, 0) * Values.y;
    float4 bodyParticle = BodiesParticles.Load(bodyIndex);
	if (bodyParticle.y <= 0)
        discard;
    
    float4 currentPosition = BodiesPositions.Load(bodyIndex);
    float4 currentRotation = BodiesRotations.Load(bodyIndex);
    float4 currentLinearMomentum = BodiesLinearMomenta.Load(bodyIndex);
    float4 currentAngularMomentum = BodiesAngularMomenta.Load(bodyIndex);
    
    float deltaTime = Values.x;
    float inverseMass = bodyParticle.w;
	
    float3 linearForce = float3(0, 0, 0);
    float3 angularForce = float3(0, 0, 0);
    
    for (int i = 0; i < bodyParticle.y; i++)
    {
        int index = bodyParticle.x + i;
        int3 particleIndex = int3(index % Values.z, index / Values.z, 0);
        
        float3 force = ParticleForces.Load(particleIndex).xyz;
        float3 relativePosition = ParticleWorldPositions.Load(particleIndex).xyz;
        linearForce += force;
        angularForce += cross(relativePosition - currentPosition.xyz, force);
    }
    linearForce += float3(0, Gravity, 0);
    
    float4x4 tensorMatrix = InverseInertialTensorArray[bodyParticle.z];
    float3x3 inverseInertiaTensor = float3x3(tensorMatrix[0].xyz, tensorMatrix[1].xyz, tensorMatrix[2].xyz);
    float3x3 rotationMatrix = QuaternionToMatrix(currentRotation);
    
    float3 newLinearMomentum = currentLinearMomentum.xyz + linearForce * deltaTime;
    newLinearMomentum = clamp(newLinearMomentum, -Values.www, Values.www);
    float3 velocity = newLinearMomentum * inverseMass;
    float3 newPosition = currentPosition.xyz + velocity * deltaTime;
    
    float3x3 inverseInertiaTensorAtTime = mul(mul(rotationMatrix, inverseInertiaTensor), transpose(rotationMatrix));
    float3 newAngularMomentum = currentAngularMomentum.xyz + angularForce * deltaTime;
    float3 angularVelocity = mul(inverseInertiaTensorAtTime, newAngularMomentum);
    
    float theta = length(angularVelocity) * deltaTime;
    float3 rotationAxis = length(angularVelocity) > 0 ? normalize(angularVelocity) : float3(0, 0, 0);
    float4 quaternionAtTime = float4(rotationAxis * sin(theta / 2), cos(theta / 2));
    float4 newRotation = QuaternionMultiply(quaternionAtTime, currentRotation);
    
    output.Position = float4(newPosition, 0);
    output.Rotation = newRotation;
    output.LinearMomentum = float4(newLinearMomentum, 0);
    output.AngularMomentum = float4(newAngularMomentum, 0);
    output.AngularVelocity = float4(angularVelocity, 0);
	
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