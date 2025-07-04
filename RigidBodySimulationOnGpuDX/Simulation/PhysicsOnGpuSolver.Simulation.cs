using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace RigidBodySimulationOnGpuDX
{
    public partial class PhysicsOnGpuSolver
    {
        public float MaxLinearMomenta { get; set; } = 85f;
        public float SpringCoefficient { get; set; } = 1000f;
        public float DampingCoefficient { get; set; } = 2.35f;
        public float TangentialStiffnessCoefficient { get; set; } = 0.85f;
        public float FloorPositionY { get; set; }

        private void Simulate(float deltaTime)
        {
            UpdateParticleValues();
            FillCollisionGrid();
            CalculateCollisionReactions();
            CalculateBodiesValues(deltaTime);
        }

        private void UpdateParticleValues()
        {
            _graphicsDevice.SetRenderTargets(_particleValuesRenderTargetBindings);

            _particleValuesEffect.Parameters["BodiesParticles"].SetValue(_bodiesParticles);
            _particleValuesEffect.Parameters["BodiesPositions"].SetValue(_currentBodiesPositions);
            _particleValuesEffect.Parameters["BodiesRotations"].SetValue(_currentBodiesRotations);
            _particleValuesEffect.Parameters["BodiesLinearMomenta"].SetValue(_currentBodiesLinearMomenta);
            _particleValuesEffect.Parameters["BodiesAngularVelocities"].SetValue(_bodiesAngularVelocities);
            _particleValuesEffect.Parameters["ParticlePositions"].SetValue(_particlePositions);
            _particleValuesEffect.Parameters["BodiesBufferSize"].SetValue(BodiesBufferSize);

            _particleValuesEffect.CurrentTechnique.Passes[0].Apply();
            Quad.Draw(_graphicsDevice);
        }

        private void FillCollisionGrid()
        {
            _graphicsDevice.SetRenderTarget(_grid);

            var gridValues = new Vector4(GridSize, ParticleBufferSize, _particleRadius, _grid.Width);
            _gridGenerationEffect.Parameters["ParticleWorldPositions"].SetValue(_particleWorldPositions);
            _gridGenerationEffect.Parameters["GridValues"].SetValue(gridValues);

            _graphicsDevice.SetVertexBuffer(_particleVertexBuffer);
            _graphicsDevice.Indices = _particleIndexBuffer;

            foreach (var pass in _gridGenerationEffect.CurrentTechnique.Passes)
            {
                _graphicsDevice.Clear(ClearOptions.Stencil, Color.Transparent, 0, 0);

                pass.Apply();
                _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.PointList, 0, 0, 1, _freeParticleIndex);
            }

            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.BlendState = BlendState.Opaque;
        }

        private void CalculateCollisionReactions()
        {
            _graphicsDevice.SetRenderTarget(_particleForces);

            var gridValues = new Vector4(GridSize, ParticleBufferSize, _particleRadius, _grid.Width);
            var bodyValues = new Vector4(SpringCoefficient, DampingCoefficient,
                TangentialStiffnessCoefficient, FloorPositionY);
            _collisionReactionEffect.Parameters["ParticleWorldPositions"].SetValue(_particleWorldPositions);
            _collisionReactionEffect.Parameters["ParticleVelocities"].SetValue(_particleVelocities);
            _collisionReactionEffect.Parameters["Grid"].SetValue(_grid);
            _collisionReactionEffect.Parameters["GridValues"].SetValue(gridValues);
            _collisionReactionEffect.Parameters["BodyValues"].SetValue(bodyValues);

            _collisionReactionEffect.CurrentTechnique.Passes[0].Apply();
            Quad.Draw(_graphicsDevice);
        }

        private void CalculateBodiesValues(float deltaTime)
        {
            _graphicsDevice.SetRenderTargets(_bodiesValuesRenderTargetBindings);

            var values = new Vector4(deltaTime, BodiesBufferSize, ParticleBufferSize, MaxLinearMomenta);
            _bodiesValuesEffect.Parameters["BodiesParticles"].SetValue(_bodiesParticles);
            _bodiesValuesEffect.Parameters["BodiesPositions"].SetValue(_currentBodiesPositions);
            _bodiesValuesEffect.Parameters["BodiesRotations"].SetValue(_currentBodiesRotations);
            _bodiesValuesEffect.Parameters["BodiesLinearMomenta"].SetValue(_currentBodiesLinearMomenta);
            _bodiesValuesEffect.Parameters["BodiesAngularMomenta"].SetValue(_currentBodiesAngularMomenta);
            _bodiesValuesEffect.Parameters["ParticleWorldPositions"].SetValue(_particleWorldPositions);
            _bodiesValuesEffect.Parameters["ParticleForces"].SetValue(_particleForces);
            _bodiesValuesEffect.Parameters["Values"].SetValue(values);
            _bodiesValuesEffect.Parameters["InverseInertialTensorArray"].SetValue(_inverseInertiaTensorBuffer);

            _bodiesValuesEffect.CurrentTechnique.Passes[0].Apply();
            Quad.Draw(_graphicsDevice);

            (_currentBodiesPositions, _nextBodiesPositions) = (_nextBodiesPositions, _currentBodiesPositions);
            (_currentBodiesRotations, _nextBodiesRotations) = (_nextBodiesRotations, _currentBodiesRotations);
            (_currentBodiesLinearMomenta, _nextBodiesLinearMomenta) = (_nextBodiesLinearMomenta, _currentBodiesLinearMomenta);
            (_currentBodiesAngularMomenta, _nextBodiesAngularMomenta) = (_nextBodiesAngularMomenta, _currentBodiesAngularMomenta);

            _bodiesValuesRenderTargetBindings[0] = _nextBodiesPositions;
            _bodiesValuesRenderTargetBindings[1] = _nextBodiesRotations;
            _bodiesValuesRenderTargetBindings[2] = _nextBodiesLinearMomenta;
            _bodiesValuesRenderTargetBindings[3] = _nextBodiesAngularMomenta;
        }
    }
}
