using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace RigidBodySimulationOnGpuDX
{
    public partial class PhysicsOnGpuSolver
    {
        private Effect _lineEffect;

        public void DrawCube(Matrix viewProjection,
            Vector3 position, Vector3 rotation, Vector3 scale, Color color)
        {
            _lineEffect ??= _contentManager.Load<Effect>("Effects\\LineDebugRender");

            var modelMatrix = Matrix.CreateScale(scale)
                * Matrix.CreateFromYawPitchRoll(rotation.X, rotation.Y, rotation.Z)
                * Matrix.CreateTranslation(position);

            _lineEffect.Parameters["Color"].SetValue(color.ToVector4());
            _lineEffect.Parameters["WorldViewProjection"].SetValue(modelMatrix * viewProjection);

            var lastRasterizerState = _graphicsDevice.RasterizerState;
            _lineEffect.CurrentTechnique.Passes[0].Apply();
            Cube.Draw(_graphicsDevice);
            _graphicsDevice.RasterizerState = lastRasterizerState;
        }
    }
}
