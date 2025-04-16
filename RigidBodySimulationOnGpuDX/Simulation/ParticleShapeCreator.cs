using System;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace RigidBodySimulationOnGpuDX
{
    public class ParticleShapeCreator
    {
        private const int DepthTextureSize = 256;

        private readonly GraphicsDevice _graphicsDevice;
        private readonly ContentManager _contentManager;

        private readonly RenderTarget2D _depthFrontLayer;
        private readonly RenderTarget2D _depthBackLayer;
        private RenderTarget2D _depthSliceBuffer;

        private readonly Effect _depthPeelingEffect;

        public ParticleShapeCreator(GraphicsDevice graphicsDevice,
            ContentManager contentManager) 
        {
            _graphicsDevice = graphicsDevice;
            _contentManager = contentManager;

            _depthFrontLayer = new RenderTarget2D(_graphicsDevice, DepthTextureSize, DepthTextureSize,
                false, SurfaceFormat.Single, DepthFormat.Depth24);
            _depthBackLayer = new RenderTarget2D(_graphicsDevice, DepthTextureSize, DepthTextureSize,
                false, SurfaceFormat.Single, DepthFormat.Depth24);

            _depthPeelingEffect = _contentManager.Load<Effect>("Effects\\DepthPeeling");
        }

        public Vector3[] CalculateParticlesPositions(Model model, float radius)
        {
            var modelBoundingBox = model.CalculateBoundingBox();
            var modelSize = modelBoundingBox.Max - modelBoundingBox.Min;

            RenderDepthLayers(model, modelSize, modelBoundingBox.Min);
            RenderDepthSliceBuffer(modelSize, modelBoundingBox.Min, radius * 2);

            var points = new Vector4[_depthSliceBuffer.Width * _depthSliceBuffer.Height];
            _depthSliceBuffer.GetData(points);
            _depthSliceBuffer.Dispose();

            var positions = points.Where(point => point.W > 0)
                .Select(point => new Vector3(point.X, point.Y, point.Z))
                .ToArray();

            return positions;
        }

        private void RenderDepthSliceBuffer(Vector3 modelSize, Vector3 modelMinBounds, float diameter)
        {
            var centeredHalfModelSize = (modelSize - new Vector3(diameter, diameter, diameter)) / 2;
            var gridSize = new Vector4(Vector3.Ceiling(centeredHalfModelSize / diameter) * 2 + Vector3.One, 0);
            gridSize.W = MathF.Ceiling(MathF.Sqrt(gridSize.Z));

            var bufferWidth = gridSize.X * gridSize.W;
            var bufferHeight = gridSize.Y * MathF.Ceiling(gridSize.Z / gridSize.W);
            _depthSliceBuffer = new RenderTarget2D(_graphicsDevice, (int)bufferWidth, (int)bufferHeight,
                false, SurfaceFormat.Vector4, DepthFormat.None);

            _graphicsDevice.SetRenderTarget(_depthSliceBuffer);
            _graphicsDevice.Clear(Color.Transparent);

            _depthPeelingEffect.Parameters["DepthFrontLayer"].SetValue(_depthFrontLayer);
            _depthPeelingEffect.Parameters["DepthBackLayer"].SetValue(_depthBackLayer);
            _depthPeelingEffect.Parameters["ModelSize"].SetValue(new Vector4(modelSize, diameter));
            _depthPeelingEffect.Parameters["ModelMinBounds"].SetValue(modelMinBounds);
            _depthPeelingEffect.Parameters["GridSize"].SetValue(gridSize);

            _depthPeelingEffect.CurrentTechnique.Passes[2].Apply();
            Quad.Draw(_graphicsDevice);

            _graphicsDevice.SetRenderTarget(null);
        }

        private void RenderDepthLayers(Model model, Vector3 modelSize, Vector3 modelMinBounds)
        {
            var modelHalfSize = modelSize / 2f;

            var clipPlane = new Vector2(0f, modelSize.Z);
            var cameraPosition = new Vector3(modelMinBounds.X + modelHalfSize.X,
                modelMinBounds.Y + modelHalfSize.Y, modelMinBounds.Z + modelSize.Z);

            var viewMatrix = Matrix.CreateLookAt(cameraPosition, modelMinBounds + modelHalfSize, Vector3.Up);
            var projectionMatrix = Matrix.CreateOrthographic(modelSize.X, modelSize.Y, clipPlane.X, clipPlane.Y);

            _depthPeelingEffect.Parameters["WorldViewProjection"].SetValue(viewMatrix * projectionMatrix);

            _graphicsDevice.SetRenderTarget(_depthFrontLayer);
            _graphicsDevice.Clear(Color.Transparent);
            foreach (var mesh in model.Meshes)
            {
                foreach (var meshPart in mesh.MeshParts)
                {
                    _graphicsDevice.SetVertexBuffer(meshPart.VertexBuffer);
                    _graphicsDevice.Indices = meshPart.IndexBuffer;

                    _depthPeelingEffect.CurrentTechnique.Passes[0].Apply();
                    _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList,
                        meshPart.VertexOffset, meshPart.StartIndex, meshPart.PrimitiveCount);
                }
            }

            _graphicsDevice.SetRenderTarget(_depthBackLayer);
            _graphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.Transparent, 0, 0);
            foreach (var mesh in model.Meshes)
            {
                foreach (var meshPart in mesh.MeshParts)
                {
                    _graphicsDevice.SetVertexBuffer(meshPart.VertexBuffer);
                    _graphicsDevice.Indices = meshPart.IndexBuffer;

                    _depthPeelingEffect.CurrentTechnique.Passes[1].Apply();
                    _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList,
                        meshPart.VertexOffset, meshPart.StartIndex, meshPart.PrimitiveCount);
                }
            }
        }
    }
}
