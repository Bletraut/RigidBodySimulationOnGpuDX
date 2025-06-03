using System;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace RigidBodySimulationOnGpuDX
{
    public partial class PhysicsOnGpuSolver
    {
        public const int DefaultShadowMapSize = 1024;

        public Vector3 LightDirection { get; set; } = new Vector3(2, 10, 2);

        public bool ShowParticles { get; set; }

        public int ShadowMapSize { get; set; } = DefaultShadowMapSize;
        public bool EnableShadows { get; set; } = true;

        // Shadows.
        private readonly Vector3[] _frustumCorners =
        [
            new(-1, 1, -1), new (1, 1, -1), new(-1, -1, -1), new(1, -1, -1),
            new(-1, 1, 1), new (1, 1, 1), new(-1, -1, 1), new(1, -1, 1)
        ];
        private Vector3[] _frustumCornersWorldPositions = new Vector3[8];

        private RenderTarget2D _shadowMap;

        // Debug.
        public RenderTarget2D ShadowMapDebug => _shadowMap;

        public void Render(Matrix viewProjection)
        {
            if (EnableShadows)
            {
                if (_shadowMap == null || _shadowMap.Width != ShadowMapSize)
                {
                    _shadowMap?.Dispose();
                    _shadowMap = new RenderTarget2D(_graphicsDevice, ShadowMapSize, ShadowMapSize,
                        false, SurfaceFormat.Single, DepthFormat.Depth24Stencil8);

                    var shadowMapValues = new Vector4(1f / ShadowMapSize, 1f / ShadowMapSize, 0, 0);
                    _simulationRenderEffect.Parameters["ShadowMapValues"].SetValue(shadowMapValues);
                }

                CalculateFrustumCornersWorldPositions(viewProjection);

                var viewMultiplier = 0.01f;
                for (var i = 0; i < 4; i++)
                {
                    _frustumCornersWorldPositions[i + 4] = _frustumCornersWorldPositions[i]
                        + (_frustumCornersWorldPositions[i + 4] - _frustumCornersWorldPositions[i]) * viewMultiplier;
                }

                var frustumCenter = _frustumCornersWorldPositions.Aggregate((a, b) => a + b) / _frustumCornersWorldPositions.Length;
                var lightViewMatrix = Matrix.CreateLookAt(frustumCenter + LightDirection, frustumCenter, Vector3.Up);

                var min = new Vector3(float.MaxValue);
                var max = new Vector3(float.MinValue);
                foreach (var position in _frustumCornersWorldPositions)
                {
                    var viewPosition = Vector3.Transform(position, lightViewMatrix);
                    min = Vector3.Min(min, viewPosition);
                    max = Vector3.Max(max, viewPosition);
                }

                var viewSize = 500;
                var lightViewProjectionMatrix = lightViewMatrix
                    * Matrix.CreateOrthographicOffCenter(min.X, max.X, min.Y, max.Y, -viewSize, viewSize);

                _graphicsDevice.SetRenderTarget(_shadowMap);
                _graphicsDevice.Clear(Color.White);
                DrawInstances(lightViewProjectionMatrix, _simulationRenderEffect.CurrentTechnique.Passes[0]);
                _graphicsDevice.SetRenderTarget(null);

                _simulationRenderEffect.Parameters["ShadowMap"].SetValue(_shadowMap);
                _simulationRenderEffect.Parameters["LightViewProjection"].SetValue(lightViewProjectionMatrix);
            }

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
            _simulationRenderEffect.Parameters["BaseColor"].SetValue(_baseColorTexture);
            _simulationRenderEffect.Parameters["LightDirection"].SetValue(LightDirection);

            DrawInstances(viewProjection, _simulationRenderEffect.CurrentTechnique.Passes[1]);
            DrawTable(viewProjection, _simulationRenderEffect.CurrentTechnique.Passes[2]);
        }

        private void DrawInstances(Matrix viewProjection, EffectPass pass)
        {
            _simulationRenderEffect.Parameters["ViewProjection"].SetValue(viewProjection);

            foreach (var (model, bodyInstanceCache) in _bodyInstancesCache)
            {
                if (bodyInstanceCache.InstancesCount == 0)
                    continue;

                _simulationRenderEffect.Parameters["CenterOfMass"].SetValue(new Vector4(bodyInstanceCache.CenterOfMass, 0));
                pass.Apply();

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
        }

        private void DrawTable(Matrix viewProjection, EffectPass pass)
        {
            var modelMatrix = Matrix.CreateScale(_particleRadius * GridSize * 2)
                * Matrix.CreateTranslation(0, FloorPositionY, 0);
            _simulationRenderEffect.Parameters["Model"].SetValue(modelMatrix);
            _simulationRenderEffect.Parameters["ViewProjection"].SetValue(viewProjection);
            pass.Apply();

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

        private void CalculateFrustumCornersWorldPositions(Matrix viewProjection)
        {
            var inverse = Matrix.Invert(viewProjection);
            for (var i = 0; i < _frustumCorners.Length; i++)
            {
                var position = Vector4.Transform(new Vector4(_frustumCorners[i], 1), inverse);
                _frustumCornersWorldPositions[i] = new Vector3(position.X, position.Y, position.Z) / position.W;
            }
        }
    }
}
