using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace RigidBodySimulationOnGpuDX
{
    public partial class PhysicsOnGpuSolver
    {
        public const int DefaultShadowMapSize = 1024;

        public const int DefaultBoxBlurIterations = 2;
        public const float DefaultGaussianKernelSigma = 1.25f;
        public const int DefaultGaussianKernerSize = 3;
        public const int MaxGaussianKernerSize = 32;

        private const float LightProjectionFarPlane = 350;

        public Vector3 LightDirection { get; set; } = new Vector3(6, 10, 0);

        public bool ShowParticles { get; set; }

        public int ShadowMapSize { get; set; } = DefaultShadowMapSize;
        public Color ShadowColor { get; set; } = Color.Gray;
        public float Sharpness { get; set; } = 0.1f;
        public bool EnableShadows { get; set; } = true;

        public BlurType BlurType { get; set; } = BlurType.Gaussian;

        public int BoxBlurIterations { get; set; } = DefaultBoxBlurIterations;

        private float _gaussianKernelSigma = DefaultGaussianKernelSigma;
        public float GaussianKernelSigma
        {
            get => _gaussianKernelSigma;
            set
            {
                var absValue = MathF.Abs(value);
                if (_gaussianKernelSigma == absValue)
                    return;

                _gaussianKernelSigma = absValue;
                _isGaussianKernelDirty = true;
            }
        }
        private int _gaussianHalfKernelSize = DefaultGaussianKernerSize;
        public int GaussianHalfKernelSize
        {
            get => _gaussianHalfKernelSize;
            set
            {
                var clampedValue = Math.Clamp(value, 0, MaxGaussianKernerSize);
                if (_gaussianHalfKernelSize == clampedValue)
                    return;

                _gaussianHalfKernelSize = clampedValue;
                _isGaussianKernelDirty = true;
            }
        }

        // Shadows.
        private readonly float[] _gaussianKernelWeights = new float[MaxGaussianKernerSize];

        private RenderTarget2D _shadowMap;
        private RenderTarget2D _shadowMapTemp;

        private Matrix _tableModelMatrix;

        private bool _isGaussianKernelDirty = true;

        public void Render(Matrix viewProjection)
        {
            _tableModelMatrix = Matrix.CreateScale(_particleRadius * GridSize * 2)
                * Matrix.CreateTranslation(0, FloorPositionY, 0);

            if (EnableShadows)
            {
                if (_shadowMap == null || _shadowMap.Width != ShadowMapSize)
                {
                    _shadowMap?.Dispose();
                    _shadowMapTemp?.Dispose();

                    _shadowMap = new RenderTarget2D(_graphicsDevice, ShadowMapSize, ShadowMapSize,
                        false, SurfaceFormat.Vector2, DepthFormat.Depth24Stencil8);
                    _shadowMapTemp = new RenderTarget2D(_graphicsDevice, ShadowMapSize, ShadowMapSize,
                        false, SurfaceFormat.Vector2, DepthFormat.Depth24Stencil8);

                    _blurEffect.Parameters["TexelSize"].SetValue(new Vector2(1f / _shadowMap.Width, 1f / _shadowMap.Height));
                }

                _simulationRenderEffect.Parameters["ShadowColor"].SetValue(ShadowColor.ToVector3());
                _simulationRenderEffect.Parameters["ShadowMapValues"].SetValue(new Vector4(Sharpness, 0, 0, 0));

                var center = new Vector3(0, -FloorPositionY, 0);
                var lightViewMatrix = Matrix.CreateLookAt(center + LightDirection, center, Vector3.Up);
                var modelViewMatrix = Matrix.Multiply(_tableModelMatrix, lightViewMatrix);

                var min = new Vector3(float.MaxValue);
                var max = new Vector3(float.MinValue);
                foreach (var position in _tableCorners)
                {
                    if (position.Y < 0)
                        continue;

                    var viewPosition = Vector3.Transform(position, modelViewMatrix);
                    min = Vector3.Min(min, viewPosition);
                    max = Vector3.Max(max, viewPosition);
                }

                var lightViewProjectionMatrix = lightViewMatrix
                    * Matrix.CreateOrthographicOffCenter(min.X, max.X, min.Y, max.Y, -LightProjectionFarPlane, LightProjectionFarPlane);

                _graphicsDevice.SetRenderTarget(_shadowMap);
                _graphicsDevice.Clear(Color.White);
                DrawInstances(lightViewProjectionMatrix, _simulationRenderEffect.CurrentTechnique.Passes[0]);
                ApplyBlur();
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

            DrawInstances(viewProjection, _simulationRenderEffect.CurrentTechnique.Passes[EnableShadows ? 1 : 3]);
            DrawTable(viewProjection, _simulationRenderEffect.CurrentTechnique.Passes[EnableShadows ? 2 : 4]);
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
            _simulationRenderEffect.Parameters["Model"].SetValue(_tableModelMatrix);
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

        private void ApplyBlur()
        {
            if (BlurType is BlurType.None)
            {
                _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
            }
            else if (BlurType is BlurType.Box)
            {
                for (var i = 0; i < BoxBlurIterations; i++)
                {
                    ApplyBlurPass(Vector2.UnitX, 0);
                    ApplyBlurPass(Vector2.UnitY, 0);
                }
            }
            else if (BlurType is BlurType.Gaussian)
            {
                GenerateHalfGaussianKernelIfDirty();
                ApplyBlurPass(Vector2.UnitX, 1);
                ApplyBlurPass(Vector2.UnitY, 1);
            }
        }

        private void ApplyBlurPass(Vector2 direction, int passIndex)
        {
            _graphicsDevice.SetRenderTarget(_shadowMapTemp);

            _blurEffect.Parameters["Source"].SetValue(_shadowMap);
            _blurEffect.Parameters["Direction"].SetValue(direction);
            _blurEffect.CurrentTechnique.Passes[passIndex].Apply();
            Quad.Draw(_graphicsDevice);

            (_shadowMap, _shadowMapTemp) = (_shadowMapTemp, _shadowMap);
        }

        private void GenerateHalfGaussianKernelIfDirty()
        {
            if (!_isGaussianKernelDirty)
                return;

            _isGaussianKernelDirty = false;

            _gaussianKernelWeights[0] = 1;
            if (GaussianKernelSigma == 0)
            {
                for (int i = 1; i < MaxGaussianKernerSize; i++)
                    _gaussianKernelWeights[i] = 0;
            }
            else
            {
                var varianceDenominator = GaussianKernelSigma * GaussianKernelSigma * 2;
                var sum = _gaussianKernelWeights[0];

                for (int i = 1; i < GaussianHalfKernelSize; i++)
                {
                    var result = MathF.Exp(-i * i / varianceDenominator);
                    _gaussianKernelWeights[i] = result;
                    sum += result * 2;
                }

                for (int i = 0; i < GaussianHalfKernelSize; i++)
                    _gaussianKernelWeights[i] /= sum;
            }

            _blurEffect.Parameters["KernelSize"].SetValue(GaussianHalfKernelSize);
            _blurEffect.Parameters["KernelWeights"].SetValue(_gaussianKernelWeights);
        }
    }

    public enum BlurType
    {
        None,
        Box,
        Gaussian
    }
}
