using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace RigidBodySimulationOnGpuDX
{
    public partial class PhysicsOnGpuSolver
    {
        public bool ShowParticles { get; set; } = false;

        public void Render(Matrix viewProjection)
        {
            if (ShowParticles)
            {
                var values = new Vector2(_particleRadius, ParticleBufferSize);
                _particlesDebugRenderEffect.Parameters["ParticleWorldPositions"].SetValue(_particleWorldPositions);
                _particlesDebugRenderEffect.Parameters["ViewProjection"].SetValue(viewProjection);
                _particlesDebugRenderEffect.Parameters["Values"].SetValue(values);
                _particlesDebugRenderEffect.CurrentTechnique.Passes[0].Apply();

                foreach (var mesh in _unitSphereModel.Meshes)
                {
                    foreach (var meshPart in mesh.MeshParts)
                    {
                        _graphicsDevice.SetVertexBuffer(meshPart.VertexBuffer);
                        _graphicsDevice.Indices = meshPart.IndexBuffer;

                        _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList,
                            meshPart.VertexOffset, meshPart.StartIndex, meshPart.PrimitiveCount, _freeParticleIndex);
                    }
                }
            }

            _simulationRenderEffect.Parameters["BodiesPositions"].SetValue(_currentBodiesPositions);
            _simulationRenderEffect.Parameters["BodiesRotations"].SetValue(_currentBodiesRotations);
            _simulationRenderEffect.Parameters["ViewProjection"].SetValue(viewProjection);
            _simulationRenderEffect.Parameters["BaseColor"].SetValue(_baseColorTexture);

            foreach (var (model, bodyInstanceCache) in _bodyInstancesCache)
            {
                _simulationRenderEffect.Parameters["CenterOfMass"].SetValue(new Vector4(bodyInstanceCache.CenterOfMass, 0));
                _simulationRenderEffect.CurrentTechnique.Passes[0].Apply();

                foreach (var mesh in model.Meshes)
                {
                    foreach (var meshPart in mesh.MeshParts)
                    {
                        _graphicsDevice.SetVertexBuffers(new VertexBufferBinding(meshPart.VertexBuffer, 0, 0),
                            new VertexBufferBinding(bodyInstanceCache.InstancesBuffer, 0, 1));
                        _graphicsDevice.Indices = meshPart.IndexBuffer;

                        _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList,
                            meshPart.VertexOffset, meshPart.StartIndex, meshPart.PrimitiveCount, bodyInstanceCache.InstancesCount);
                    }
                }
            }

            var modelMatrix = Matrix.CreateScale(_particleRadius * GridSize * 2)
                * Matrix.CreateTranslation(0, FloorPositionY, 0);
            _simulationRenderEffect.Parameters["Model"].SetValue(modelMatrix);
            _simulationRenderEffect.Parameters["ViewProjection"].SetValue(viewProjection);

            _simulationRenderEffect.CurrentTechnique.Passes[1].Apply();
            foreach (var mesh in _tableModel.Meshes)
            {
                foreach (var meshPart in mesh.MeshParts)
                {
                    _graphicsDevice.SetVertexBuffer(meshPart.VertexBuffer);
                    _graphicsDevice.Indices = meshPart.IndexBuffer;

                    _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList,
                        meshPart.VertexOffset, meshPart.StartIndex, meshPart.PrimitiveCount);
                }
            }
        }
    }
}
