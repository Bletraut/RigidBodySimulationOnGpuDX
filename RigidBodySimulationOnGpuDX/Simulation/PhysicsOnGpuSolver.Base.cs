using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace RigidBodySimulationOnGpuDX
{
    public partial class PhysicsOnGpuSolver
    {
        public const int GridSize = 256;

        private const int BodiesBufferSize = 128;
        private const int ParticleBufferSize = 1024;

        private const int InverseInertiaTensorBufferSize = 12;

        public int IterationsCount { get; set; } = 3;
        public int BodiesCount { get; private set; }
        public int ParticleCount { get; private set; }

        public bool IsSimulationEnabled { get; set; } = true;

        private readonly GraphicsDevice _graphicsDevice;
        private readonly ContentManager _contentManager;
        private readonly float _particleRadius;

        private readonly ParticleShapeCreator _particleShapeCreator;

        // Bodies.
        private readonly RenderTarget2D _bodiesParticles;

        private RenderTarget2D _currentBodiesPositions;
        private RenderTarget2D _nextBodiesPositions;

        private RenderTarget2D _currentBodiesRotations;
        private RenderTarget2D _nextBodiesRotations;

        private RenderTarget2D _currentBodiesLinearMomenta;
        private RenderTarget2D _nextBodiesLinearMomenta;

        private RenderTarget2D _currentBodiesAngularMomenta;
        private RenderTarget2D _nextBodiesAngularMomenta;

        private readonly RenderTarget2D _bodiesAngularVelocities;

        // Particles.
        private readonly RenderTarget2D _particlePositions;
        private readonly RenderTarget2D _particleWorldPositions;
        private readonly RenderTarget2D _particleVelocities;
        private readonly RenderTarget2D _particleForces;

        // Grid for collisions.
        private readonly RenderTarget2D _grid;

        private readonly Vector4[] _bodiesParticlesData;
        private readonly Vector4[] _bodiesPositionsData;
        private readonly Vector4[] _bodiesRotationsData;
        private readonly Vector4[] _bodiesLinearMomentumData;
        private readonly Vector4[] _bodiesAngularMomentumData;
        private readonly Vector4[] _particlePositionsData;

        private readonly List<PendingBodyData> _pendingBodies = [];
        private readonly Dictionary<Model, BodyInstancesData> _bodyInstancesCache = [];

        private readonly VertexBuffer _particleVertexBuffer;
        private readonly IndexBuffer _particleIndexBuffer;

        private readonly Effect _particleValuesEffect;
        private readonly Effect _gridGenerationEffect;
        private readonly Effect _collisionReactionEffect;
        private readonly Effect _bodiesValuesEffect;

        private readonly Effect _particlesDebugRenderEffect;
        private readonly Effect _simulationRenderEffect;

        private readonly Effect _boxBlurEffect;

        // Textures.
        private readonly Texture2D _baseColorTexture;

        // Models.
        private readonly Model _unitSphereModel;
        private readonly Model _tableModel;

        private readonly Matrix[] _inverseInertiaTensorBuffer = new Matrix[InverseInertiaTensorBufferSize];
        private readonly RenderTarget2D[] _renderTargetsToClear;

        private int _freeBodyIndex;
        private int _freeParticleIndex;

        private int _freeInverseInertiaTensorIndex;

        public PhysicsOnGpuSolver(GraphicsDevice graphicsDevice, ContentManager contentManager,
            float particleRadius) 
        {
            _graphicsDevice = graphicsDevice;
            _contentManager = contentManager;
            _particleRadius = particleRadius;

            _particleShapeCreator = new ParticleShapeCreator(_graphicsDevice, _contentManager);

            // Bodies.
            _bodiesParticles = new RenderTarget2D(graphicsDevice, BodiesBufferSize, BodiesBufferSize,
                false, SurfaceFormat.Vector4, DepthFormat.None);

            _currentBodiesPositions = new RenderTarget2D(graphicsDevice, BodiesBufferSize, BodiesBufferSize,
                false, SurfaceFormat.Vector4, DepthFormat.None);
            _nextBodiesPositions = new RenderTarget2D(graphicsDevice, BodiesBufferSize, BodiesBufferSize,
                false, SurfaceFormat.Vector4, DepthFormat.None);

            _currentBodiesRotations = new RenderTarget2D(graphicsDevice, BodiesBufferSize, BodiesBufferSize,
                false, SurfaceFormat.Vector4, DepthFormat.None);
            _nextBodiesRotations = new RenderTarget2D(graphicsDevice, BodiesBufferSize, BodiesBufferSize,
                false, SurfaceFormat.Vector4, DepthFormat.None);

            _currentBodiesLinearMomenta = new RenderTarget2D(graphicsDevice, BodiesBufferSize, BodiesBufferSize,
                false, SurfaceFormat.Vector4, DepthFormat.None);
            _nextBodiesLinearMomenta = new RenderTarget2D(graphicsDevice, BodiesBufferSize, BodiesBufferSize,
                false, SurfaceFormat.Vector4, DepthFormat.None);

            _currentBodiesAngularMomenta = new RenderTarget2D(graphicsDevice, BodiesBufferSize, BodiesBufferSize,
                false, SurfaceFormat.Vector4, DepthFormat.None);
            _nextBodiesAngularMomenta = new RenderTarget2D(graphicsDevice, BodiesBufferSize, BodiesBufferSize,
                false, SurfaceFormat.Vector4, DepthFormat.None);

            _bodiesAngularVelocities = new RenderTarget2D(graphicsDevice, BodiesBufferSize, BodiesBufferSize,
                false, SurfaceFormat.Vector4, DepthFormat.None);

            _bodiesParticlesData = new Vector4[BodiesBufferSize * BodiesBufferSize];
            _bodiesPositionsData = new Vector4[BodiesBufferSize * BodiesBufferSize];
            _bodiesRotationsData = new Vector4[BodiesBufferSize * BodiesBufferSize];
            _bodiesLinearMomentumData = new Vector4[BodiesBufferSize * BodiesBufferSize];
            _bodiesAngularMomentumData = new Vector4[BodiesBufferSize * BodiesBufferSize];

            // Particles.
            _particlePositions = new RenderTarget2D(graphicsDevice, ParticleBufferSize, ParticleBufferSize,
                false, SurfaceFormat.Vector4, DepthFormat.None);
            _particleWorldPositions = new RenderTarget2D(graphicsDevice, ParticleBufferSize, ParticleBufferSize,
                false, SurfaceFormat.Vector4, DepthFormat.None);
            _particleVelocities = new RenderTarget2D(graphicsDevice, ParticleBufferSize, ParticleBufferSize,
                false, SurfaceFormat.Vector4, DepthFormat.None);
            _particleForces = new RenderTarget2D(graphicsDevice, ParticleBufferSize, ParticleBufferSize,
                false, SurfaceFormat.Vector4, DepthFormat.None);

            _particlePositionsData = new Vector4[ParticleBufferSize * ParticleBufferSize];

            _particleVertexBuffer = new VertexBuffer(_graphicsDevice, typeof(VertexPosition), 1, BufferUsage.WriteOnly);
            _particleVertexBuffer.SetData(new VertexPosition[] { new(Vector3.Zero) });
            _particleIndexBuffer = new IndexBuffer(_graphicsDevice, IndexElementSize.SixteenBits, 1, BufferUsage.WriteOnly);
            _particleIndexBuffer.SetData(Array.Empty<int>());

            // Grid.
            var gridScale = (int)MathF.Sqrt(GridSize);
            _grid = new RenderTarget2D(graphicsDevice, GridSize * gridScale, GridSize * gridScale,
                false, SurfaceFormat.Vector4, DepthFormat.Depth24Stencil8);

            _particleValuesEffect = _contentManager.Load<Effect>("Effects\\ParticleValues");
            _gridGenerationEffect = _contentManager.Load<Effect>("Effects\\GridGeneration");
            _collisionReactionEffect = _contentManager.Load<Effect>("Effects\\CollisionReaction");
            _bodiesValuesEffect = _contentManager.Load<Effect>("Effects\\BodiesValues");

            _particlesDebugRenderEffect = _contentManager.Load<Effect>("Effects\\ParticleDebugRender");
            _simulationRenderEffect = _contentManager.Load<Effect>("Effects\\SimulationRender");
            _boxBlurEffect = _contentManager.Load<Effect>("Effects\\BoxBlur");

            // Textures.
            _baseColorTexture = _contentManager.Load<Texture2D>("Textures\\BaseColor");

            // Models.
            _unitSphereModel = _contentManager.Load<Model>("Models\\UnitSphere");
            _tableModel = _contentManager.Load<Model>("Models\\Table");

            _renderTargetsToClear =
            [
                _bodiesParticles,
                _currentBodiesPositions, _nextBodiesPositions,
                _currentBodiesRotations, _nextBodiesRotations,
                _currentBodiesLinearMomenta, _nextBodiesLinearMomenta,
                _currentBodiesAngularMomenta, _nextBodiesAngularMomenta,
                _bodiesAngularVelocities,
                _particlePositions, _particleWorldPositions,
            ];
        }

        public void Reset()
        {
            _freeBodyIndex = 0;
            _freeParticleIndex = 0;

            BodiesCount = 0;
            ParticleCount = 0;

            foreach (var pair in _bodyInstancesCache)
                pair.Value.Reset();

            foreach (var renderTarget in _renderTargetsToClear)
            {
                _graphicsDevice.SetRenderTarget(renderTarget);
                _graphicsDevice.Clear(Color.Transparent);
            }

            _graphicsDevice.SetRenderTarget(null);
        }

        public void Update(float deltaTime)
        {
            AddPendingBodies();

            if (!IsSimulationEnabled)
                return;

            var iterationDeltaTime = deltaTime / IterationsCount;
            for (var i = 0; i < IterationsCount; i++)
                Simulate(MathF.Min(iterationDeltaTime, 1f / 60));

            _graphicsDevice.SetRenderTarget(null);
        }
    }
}
