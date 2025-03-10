using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace RigidBodySimulationOnGpuDX
{
    public partial class PhysicsOnGpuSolver
    {
        public void AddBody(Model model, float mass, Vector3 position, Quaternion rotation,
            Vector3 linearMomentum = default, Vector3 angularMomentum = default)
        {
            var canAddBody = BodiesCount < BodiesBufferSize * BodiesBufferSize;
            if (!canAddBody)
                return;

            if (!_bodyInstancesCache.TryGetValue(model, out var bodyInstancesData))
            {
                var particlePositions = _particleShapeCreator.CalculateParticlesPositions(model, _particleRadius);

                var centerOfMass = particlePositions.Aggregate((a, b) => a + b) / particlePositions.Length;
                for (int i = 0; i < particlePositions.Length; i++)
                    particlePositions[i] -= centerOfMass;

                bodyInstancesData = new BodyInstancesData(_graphicsDevice, BodiesBufferSize,
                    particlePositions, centerOfMass, _freeInverseInertiaTensorIndex);
                _bodyInstancesCache.Add(model, bodyInstancesData);

                _inverseInertiaTensorBuffer[_freeInverseInertiaTensorIndex] = CalculateInverseInertiaTensor(mass, particlePositions);
                _freeInverseInertiaTensorIndex++;
            }
            bodyInstancesData.AddInstance(BodiesCount);

            BodiesCount++;
            _pendingBodies.Add(new PendingBodyData(bodyInstancesData.InverseInertiaTensorIndex,
                mass, bodyInstancesData.ParticlePositions, position, rotation, linearMomentum, angularMomentum));

            ParticleCount += bodyInstancesData.ParticlePositions.Length;
        }

        private void AddPendingBodies()
        {
            if (_pendingBodies.Count == 0)
                return;

            _bodiesParticles.GetData(_bodiesParticlesData);
            _currentBodiesPositions.GetData(_bodiesPositionsData);
            _currentBodiesRotations.GetData(_bodiesRotationsData);
            _currentBodiesLinearMomenta.GetData(_bodiesLinearMomentumData);
            _currentBodiesAngularMomenta.GetData(_bodiesAngularMomentumData);
            _particlePositions.GetData(_particlePositionsData);

            foreach (var pendingBody in _pendingBodies)
            {
                var canAddParticles = _freeBodyIndex + 1 < BodiesBufferSize * BodiesBufferSize
                    && _freeParticleIndex + pendingBody.ParticlePositions.Length < ParticleBufferSize * ParticleBufferSize;
                if (!canAddParticles)
                    continue;

                _bodiesParticlesData[_freeBodyIndex] = new Vector4(_freeParticleIndex, pendingBody.ParticlePositions.Length,
                    pendingBody.InverseInertiaTensorIndex, 1f / pendingBody.Mass);
                _bodiesPositionsData[_freeBodyIndex] = new Vector4(pendingBody.Position, 0);
                _bodiesRotationsData[_freeBodyIndex] = pendingBody.Rotation.ToVector4();
                _bodiesLinearMomentumData[_freeBodyIndex] = new Vector4(pendingBody.LinearMomentum, 0);
                _bodiesAngularMomentumData[_freeBodyIndex] = new Vector4(pendingBody.AngularMomentum, 0);

                for (int i = 0; i < pendingBody.ParticlePositions.Length; i++)
                {
                    var position = new Vector4(pendingBody.ParticlePositions[i], _freeBodyIndex + 1);
                    _particlePositionsData[_freeParticleIndex + i] = position;
                }

                _freeBodyIndex += 1;
                _freeParticleIndex += pendingBody.ParticlePositions.Length;
            }

            _bodiesParticles.SetData(_bodiesParticlesData);
            _currentBodiesPositions.SetData(_bodiesPositionsData);
            _currentBodiesRotations.SetData(_bodiesRotationsData);
            _currentBodiesLinearMomenta.SetData(_bodiesLinearMomentumData);
            _currentBodiesAngularMomenta.SetData(_bodiesAngularMomentumData);
            _particlePositions.SetData(_particlePositionsData);

            _pendingBodies.Clear();
        }

        private Matrix CalculateInverseInertiaTensor(float mass, Vector3[] positions)
        {
            var particleMass = mass / positions.Length;

            var tensor = new Matrix();
            foreach (var (x, y, z) in positions)
            {
                tensor.M11 += particleMass * (y * y + z * z);
                tensor.M12 -= particleMass * x * y;
                tensor.M13 -= particleMass * x * z;
                tensor.M21 -= particleMass * y * x;
                tensor.M22 += particleMass * (x * x + z * z);
                tensor.M23 -= particleMass * y * z;
                tensor.M31 -= particleMass * z * x;
                tensor.M32 -= particleMass * z * y;
                tensor.M33 += particleMass * (x * x + y * y);
            }
            tensor.M44 = 1;

            if (tensor.M11 == 0) tensor.M11 += (float)1e+6;
            if (tensor.M22 == 0) tensor.M22 += (float)1e+6;
            if (tensor.M33 == 0) tensor.M33 += (float)1e+6;

            Matrix.Invert(ref tensor, out var inverseTensor);

            return inverseTensor;
        }

        private class BodyInstancesData
        {
            private static readonly VertexDeclaration _vertexDeclaration;

            static BodyInstancesData()
            {
                var elements = new VertexElement[]
                {
                    new(0, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 2)
                };
                _vertexDeclaration = new VertexDeclaration(elements);
            }

            public VertexBuffer InstancesBuffer { get; private set; }
            public int InstancesCount { get; private set; }
            public Vector3[] ParticlePositions { get; private set; }
            public Vector3 CenterOfMass { get; private set; }
            public int InverseInertiaTensorIndex { get; private set; }

            private readonly int _bufferSize;
            private readonly Vector2[] _instancesData;

            public BodyInstancesData(GraphicsDevice graphicsDevice, int bufferSize,
                Vector3[] particlePositions, Vector3 centerOfMass, int inverseInertiaTensorIndex)
            {
                _bufferSize = bufferSize;
                _instancesData = new Vector2[bufferSize * bufferSize];

                ParticlePositions = particlePositions;
                CenterOfMass = centerOfMass;
                InverseInertiaTensorIndex = inverseInertiaTensorIndex;

                InstancesBuffer = new VertexBuffer(graphicsDevice, _vertexDeclaration,
                    _instancesData.Length, BufferUsage.WriteOnly);
            }

            public void AddInstance(int instanceIndex)
            {
                var x = instanceIndex % _bufferSize;
                var y = instanceIndex / _bufferSize;

                _instancesData[InstancesCount] = new Vector2(x, y);
                InstancesBuffer.SetData(_instancesData);

                InstancesCount++;
            }

            public void Reset()
            {
                InstancesCount = 0;
            }
        }

        private readonly struct PendingBodyData(int inverseInertialTensorIndex,
            float mass, Vector3[] particlesPositions,
            Vector3 position, Quaternion rotation,
            Vector3 linearMomentum, Vector3 angularMomentum)
        {
            public readonly int InverseInertiaTensorIndex = inverseInertialTensorIndex;
            public readonly float Mass = mass;
            public readonly Vector3[] ParticlePositions = particlesPositions;
            public readonly Vector3 Position = position;
            public readonly Quaternion Rotation = rotation;
            public readonly Vector3 LinearMomentum = linearMomentum;
            public readonly Vector3 AngularMomentum = angularMomentum;
        }
    }
}
