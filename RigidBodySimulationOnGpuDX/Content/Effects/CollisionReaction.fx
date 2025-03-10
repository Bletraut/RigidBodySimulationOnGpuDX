#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_5_0
	#define PS_SHADERMODEL ps_5_0
#endif

Texture2D ParticleWorldPositions;
Texture2D ParticleVelocities;
Texture2D Grid;
// x = GridSize, y = ParticleBufferSize, z = ParticleRadius, w = GridTextureWidth.
float4 GridValues;
// x = k, y = n, z = kt, w = FloorPositionY
float4 BodyValues;

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

int3 GetVoxelIndex(float3 particlePosition)
{
    float3 smallestGridCorner = -GridValues.xxx * GridValues.z;
    int3 voxelIndex = (particlePosition.xyz - smallestGridCorner) / (GridValues.z * 2);
	
    return voxelIndex;
}

float4 GetVoxelData(int3 voxelIndex)
{
    float d = floor(GridValues.w / GridValues.x);
    int2 voxelUv = voxelIndex.xy + GridValues.xx * int2(voxelIndex.z % d, voxelIndex.z / d);
    voxelUv.y = GridValues.w - voxelUv.y;
    voxelUv -= 1;
    
    return Grid.Load(int3(voxelUv, 0));
}

void ComputeReactionForce(float4 positionA, float4 velocityA, float4 positionB, float4 velocityB, inout float3 force)
{
    float3 relativePosition = positionB.xyz - positionA.xyz;
    float3 relativeVelocity = velocityB.xyz - velocityA.xyz;
    
    float distance = length(relativePosition);
    if (distance >= GridValues.z * 2)
        return;
    
    float3 direction = normalize(relativePosition);
    float3 relativeTangentialVelocity = relativeVelocity - dot(relativeVelocity, direction) * direction;
    
    float3 springForce = -BodyValues.x * (GridValues.z * 2 - distance) * direction;
    float3 dampingForce = BodyValues.y * relativeVelocity;
    float3 shearForce = BodyValues.z * relativeTangentialVelocity;
    
    force += springForce + dampingForce + shearForce;
}

void Collision(float4 position, float4 velocity, float neighborIndex, inout float3 force)
{    
    neighborIndex -= 1;
    if (neighborIndex < 0)
        return;
    
    int2 neighborUv = int2(neighborIndex % GridValues.y, neighborIndex / GridValues.y);
    float4 neighborPosition = ParticleWorldPositions.Load(int3(neighborUv, 0));
    
    if (position.w == neighborPosition.w)
        return;
    
    float4 neighborVelocity = ParticleVelocities.Load(int3(neighborUv, 0));
    ComputeReactionForce(position, velocity, neighborPosition, neighborVelocity, force);
}

float4 MainPS(VertexShaderOutput input) : SV_Target
{
    int2 uv = float2(input.Uv.x, 1 - input.Uv.y) * GridValues.y;
    float4 position = ParticleWorldPositions.Load(int3(uv, 0));
    float bodyIndex = position.w - 1;
    if (bodyIndex < 0)
        return float4(0, 0, 0, 0);
	
    int3 voxelIndex = GetVoxelIndex(position.xyz);
	if (any(voxelIndex < 0 || voxelIndex > GridValues.x))
        return float4(0, 0, 0, 0);
    
    float4 velocity = ParticleVelocities.Load(int3(uv, 0));
    
    float3 force = float3(0, 0, 0);
    for (int i = -1; i <= 1; i++)
    {
        if (voxelIndex.x + i < 0 || voxelIndex.x + i > GridValues.x)
            continue;
        
        for (int j = -1; j <= 1; j++)
        {
            if (voxelIndex.y + j < 0 || voxelIndex.y + j > GridValues.x)
                continue;
            
            for (int k = -1; k <= 1; k++)
            {
                if (voxelIndex.z + k < 0 || voxelIndex.z + k > GridValues.x)
                    continue;
                
                int3 neighborIndex = voxelIndex + int3(i, j, k);
                float4 voxelData = GetVoxelData(neighborIndex);
                
                Collision(position, velocity, voxelData.r, force);
                Collision(position, velocity, voxelData.g, force);
                Collision(position, velocity, voxelData.b, force);
                Collision(position, velocity, voxelData.a, force);
            }
        }
    }
    
    float4 floorPosition = position;
    floorPosition.y = BodyValues.w + GridValues.z;
    ComputeReactionForce(position, velocity, floorPosition, (float4)0, force);
	
    return float4(force, 1);
}

technique BasicColorDrawing
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};