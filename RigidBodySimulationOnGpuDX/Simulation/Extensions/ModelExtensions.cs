using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace RigidBodySimulationOnGpuDX
{
    public static class ModelExtensions
    {
        public static BoundingBox CalculateBoundingBox(this Model model)
        {
            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);

            foreach (var mesh in model.Meshes)
            {
                foreach (var meshPart in mesh.MeshParts)
                {
                    var vertexStride = meshPart.VertexBuffer.VertexDeclaration.VertexStride;

                    var vertices = new Vector3[meshPart.NumVertices];
                    meshPart.VertexBuffer.GetData(0, vertices, 0, meshPart.NumVertices, vertexStride);

                    foreach (var vertex in vertices)
                    {
                        min = Vector3.Min(min, vertex);
                        max = Vector3.Max(max, vertex);
                    }
                }
            }

            return new BoundingBox(min, max);
        }
    }
}
