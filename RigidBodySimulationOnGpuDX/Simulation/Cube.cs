using Microsoft.Xna.Framework.Graphics;

namespace RigidBodySimulationOnGpuDX
{
    public static class Cube
    {
        public static VertexPosition[] Vertices { get; private set; } =
        [
            new(new(-1, 1, 1)), new(new(1, 1, 1)),
            new(new(-1, -1, 1)), new(new(1, -1, 1)),
            new(new(-1, 1, -1)), new(new(1, 1, -1)),
            new(new(-1, -1, -1)), new(new(1, -1, -1)),
        ];
        public static short[] Indices { get; private set; } = 
        [
            0, 1, 2, 1, 3, 2,
            4, 6, 7, 4, 7, 5,

            4, 0, 6, 0, 2, 6,
            1, 5, 3, 5, 7, 3,

            0, 4, 5, 5, 1, 0,
            6, 2, 3, 3, 7, 6,
        ];

        public static void Draw(GraphicsDevice graphicsDevice)
        {
            graphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                Vertices, 0, Vertices.Length,
                Indices, 0, Indices.Length / 3);
        }
    }
}
