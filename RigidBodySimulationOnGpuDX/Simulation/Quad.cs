using Microsoft.Xna.Framework.Graphics;

namespace RigidBodySimulationOnGpuDX
{
    public static class Quad
    {
        public static VertexPositionTexture[] Vertices { get; private set; } =
        [
            new(new(-1, -1, 0), new(0, 0)),
            new(new(-1, 1, 0), new(0, 1)),
            new(new(1, 1, 0), new(1, 1)),
            new(new(1, -1, 0), new(1, 0)),
        ];
        public static short[] Indices { get; private set; } = [0, 1, 2, 0, 2, 3];

        public static void Draw(GraphicsDevice graphicsDevice)
        {
            graphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                Vertices, 0, Vertices.Length,
                Indices, 0, Indices.Length / 3);
        }
    }
}
