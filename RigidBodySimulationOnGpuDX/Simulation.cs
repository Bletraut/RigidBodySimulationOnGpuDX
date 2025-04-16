using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace RigidBodySimulationOnGpuDX
{
    public class Simulation : Game
    {
        private static Quaternion GetRandomRotation()
        {
            var rotation = Quaternion.CreateFromYawPitchRoll(NextSingle() * MathF.PI,
                NextSingle() * MathF.PI, NextSingle() * MathF.PI);

            return rotation;
        }

        private static float NextSingle() => Random.Shared.NextSingle();

        private readonly GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        private Effect _skyboxEffect;

        private Vector3 _cameraPosition;
        private Vector3 _cameraYawPitchRoll;
        private Vector3 _cameraForward;
        private Vector3 _cameraRight;
        private Vector3 _cameraUp;
        private float _cameraMoveSpeed;
        private float _cameraSlowMoveSpeed;
        private readonly float _cameraRotationSpeed = MathF.PI / 360f;

        private readonly float _bodyMass = 1;
        private readonly float _particleRadius = 1f;
        private readonly float _shootForce = 100;

        private Matrix _viewMatrix;
        private Matrix _projectionMatrix;

        private Texture2D _pixelTexture;
        private SpriteFont _mainFont;

        private List<Model> _models;
        private Model _barrelModel;
        private PhysicsOnGpuSolver _physicsOnGpuSolver;

        private readonly (string Name, Action Action)[] _spawnActions;
        private int _currentSpawnActionIndex;

        private readonly StringBuilder _simulationInfo = new();
        private bool _showSimulationInfo = true;

        public Simulation()
        {
            _graphics = new GraphicsDeviceManager(this)
            {
                GraphicsProfile = GraphicsProfile.HiDef,
                PreferMultiSampling = true,
            };
            Content.RootDirectory = "Content";
            IsFixedTimeStep = false;
            IsMouseVisible = true;

            _spawnActions =
            [
                ("Big_Vertical_Flow", () => SpawnVerticalFlow(25, 900)),
                ("Pyramid", () => SpawnPyramid(_barrelModel, new Vector3(0, _physicsOnGpuSolver.FloorPositionY, 0), 21)),
                ("Small_Vertical_Flow", () =>
                {
                    SpawnPyramid(_barrelModel, new Vector3(0, _physicsOnGpuSolver.FloorPositionY, 0), 11);
                    SpawnVerticalFlow(200, 300);
                }),
            ];
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            _skyboxEffect = Content.Load<Effect>("Effects\\Skybox");
            _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
            _pixelTexture.SetData([Color.White]);
            _mainFont = Content.Load<SpriteFont>("Fonts\\MainFont");

            _models =
            [
                Content.Load<Model>("Models\\Apple"),
                Content.Load<Model>("Models\\Avocado"),
                Content.Load<Model>("Models\\Banana"),
                Content.Load<Model>("Models\\Broccoli"),
                Content.Load<Model>("Models\\Coconut"),
                Content.Load<Model>("Models\\Eggplant"),
                Content.Load<Model>("Models\\Ham"),
                Content.Load<Model>("Models\\Mango"),
                Content.Load<Model>("Models\\Onigiri"),
                Content.Load<Model>("Models\\Pumpkin"),
                Content.Load<Model>("Models\\Tuna"),
            ];
            _barrelModel = Content.Load<Model>("Models\\Barrel");

            _physicsOnGpuSolver = new PhysicsOnGpuSolver(GraphicsDevice, Content, _particleRadius)
            {
                FloorPositionY = -_particleRadius * PhysicsOnGpuSolver.GridSize / 2
            };
            RunCurrentSpawnAction();

            _cameraPosition = new Vector3()
            {
                Y = _physicsOnGpuSolver.FloorPositionY + _particleRadius * PhysicsOnGpuSolver.GridSize / 3f,
                Z = _particleRadius * PhysicsOnGpuSolver.GridSize / 1.5f
            };
            var viewDirection = Vector3.Normalize(new Vector3(0, _physicsOnGpuSolver.FloorPositionY, 0) - _cameraPosition);
            _cameraYawPitchRoll.Y = MathF.Asin(viewDirection.Y);
            _cameraMoveSpeed = _particleRadius;
            _cameraSlowMoveSpeed = _particleRadius / 10;

            var aspectRatio = GraphicsDevice.Viewport.AspectRatio;
            _projectionMatrix = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(45.0f), aspectRatio, 1.0f, 10000.0f);
        }

        protected override void Update(GameTime gameTime)
        {
            Input.Update();

            if (Input.IsKeyDown(Keys.Escape))
                Exit();

            if (Input.IsKeyDown(Keys.F1))
                _physicsOnGpuSolver.ShowParticles = !_physicsOnGpuSolver.ShowParticles;

            if (Input.IsKeyDown(Keys.F2))
                _showSimulationInfo = !_showSimulationInfo;

            if (Input.IsKeyDown(Keys.F))
                _physicsOnGpuSolver.IsSimulationEnabled = !_physicsOnGpuSolver.IsSimulationEnabled;

            if (Input.IsKeyDown(Keys.E))
            {
                _currentSpawnActionIndex++;
                if (_currentSpawnActionIndex >= _spawnActions.Length)
                    _currentSpawnActionIndex = 0;

                RunCurrentSpawnAction();
            }

            if (Input.IsKeyDown(Keys.R))
                RunCurrentSpawnAction();

            var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _physicsOnGpuSolver.Update(deltaTime);

            if (Input.IsMouseButtonPressed(MouseButton.Left))
            {
                var delta = new Vector3(-Input.MousePositionDelta.X, -Input.MousePositionDelta.Y, 0) * _cameraRotationSpeed;
                _cameraYawPitchRoll += delta;
                _cameraYawPitchRoll.Y = MathHelper.Clamp(_cameraYawPitchRoll.Y, -MathHelper.PiOver2, MathHelper.PiOver2);
            }
            if (Input.IsMouseButtonDown(MouseButton.Right))
            {
                var nearPoint = GraphicsDevice.Viewport.Unproject(new Vector3(Input.MousePosition.ToVector2(), 0),
                    _projectionMatrix, _viewMatrix, Matrix.Identity);
                var farPoint = GraphicsDevice.Viewport.Unproject(new Vector3(Input.MousePosition.ToVector2(), 1),
                    _projectionMatrix, _viewMatrix, Matrix.Identity);
                var shootDirection = Vector3.Normalize(farPoint - nearPoint);

                var model = _models[Random.Shared.Next(_models.Count)];
                _physicsOnGpuSolver.AddBody(model, 1f, _cameraPosition, Quaternion.Identity,
                    linearMomentum: shootDirection * _shootForce,
                    angularMomentum: NextSingle() * Vector3.One * 10);
            }

            var rotation = Quaternion.CreateFromYawPitchRoll(_cameraYawPitchRoll.X, _cameraYawPitchRoll.Y, _cameraYawPitchRoll.Z);
            _cameraForward = Vector3.Transform(Vector3.Forward, rotation);
            _cameraRight = Vector3.Transform(Vector3.Right, rotation);
            _cameraUp = Vector3.Cross(_cameraRight, _cameraForward);

            var moveSpeed = Input.IsKeyPressed(Keys.LeftShift) ? _cameraSlowMoveSpeed : _cameraMoveSpeed;
            if (Input.IsKeyPressed(Keys.W))
            {
                _cameraPosition += _cameraForward * moveSpeed;
            }
            else if (Input.IsKeyPressed(Keys.S))
            {
                _cameraPosition -= _cameraForward * moveSpeed;
            }
            if (Input.IsKeyPressed(Keys.A))
            {
                _cameraPosition -= _cameraRight * moveSpeed;
            }
            else if (Input.IsKeyPressed(Keys.D))
            {
                _cameraPosition += _cameraRight * moveSpeed;
            }

            _viewMatrix = Matrix.CreateLookAt(_cameraPosition, _cameraPosition + _cameraForward, _cameraUp);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            _skyboxEffect.Parameters["InverseViewProjection"].SetValue(Matrix.Invert(_viewMatrix * _projectionMatrix));
            _skyboxEffect.CurrentTechnique.Passes[0].Apply();
            Quad.Draw(GraphicsDevice);

            _physicsOnGpuSolver.Render(_viewMatrix * _projectionMatrix);

            if (_showSimulationInfo)
            {
                _simulationInfo.Clear();
                _simulationInfo.AppendLine("Move:WASD | Look:LMB | Shoot:RMB");
                _simulationInfo.AppendLine("Next:E | Restart:R | Debug:F1|F2");
                _simulationInfo.AppendLine($"{_spawnActions[_currentSpawnActionIndex].Name} [{_currentSpawnActionIndex + 1}\\{_spawnActions.Length}]");
                _simulationInfo.AppendLine($"Bodies:{_physicsOnGpuSolver.BodiesCount} Particles:{_physicsOnGpuSolver.ParticleCount}");
                _simulationInfo.Append($"Fps:{1f / gameTime.ElapsedGameTime.TotalSeconds: 00.0}");

                _spriteBatch.Begin();
                var border = _mainFont.MeasureString(_simulationInfo);
                _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, (int)border.X, (int)border.Y), new Color(0, 0, 0, 0.85f));
                _spriteBatch.DrawString(_mainFont, _simulationInfo, Vector2.Zero, Color.White);
                _spriteBatch.End();
            }

            base.Draw(gameTime);
        }

        private void RunCurrentSpawnAction()
        {
            _physicsOnGpuSolver.Reset();
            _spawnActions[_currentSpawnActionIndex].Action?.Invoke();
        }

        private void SpawnPyramid(Model model, Vector3 position, int width)
        {
            var modelSize = new Vector3(6.2f, 6, 6.2f);

            var startPosition = position;
            startPosition.X -= modelSize.X * width / 2;
            startPosition.Z -= modelSize.Z * width / 2;
            startPosition.Y += modelSize.Y - _particleRadius;

            for (int y = 0; y < width; y++)
            {
                for (int i = y; i < width - y; i++)
                {
                    for (int j = y; j < width - y; j++)
                    {
                        var objectPosition = new Vector3(modelSize.X * i, modelSize.Y * y, modelSize.Z * j);

                        _physicsOnGpuSolver.AddBody(model, _bodyMass,
                            startPosition + objectPosition, Quaternion.Identity);
                    }
                }
            }
        }

        private void SpawnVerticalFlow(float offset, int height)
        {
            for (int y = 0; y < height; y++)
            {
                var position = new Vector3(0, 12 * y, 0);
                position.Y += _physicsOnGpuSolver.FloorPositionY + _particleRadius * offset;
                SpawnInCircle(position, Vector3.One * 4, 24, 12);
                SpawnInCircle(position, Vector3.One * 4, 12, 5);
            }
        }

        private void SpawnInCircle(Vector3 position, Vector3 offset, float radius, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var angle = MathF.PI * 2 * i / (float)count;

                var offsetPosition = new Vector3(NextSingle(), NextSingle(), NextSingle()) * offset * 2 - offset;
                var objectPosition = new Vector3()
                {
                    X = radius * MathF.Cos(angle),
                    Z = radius * MathF.Sin(angle)
                };

                _physicsOnGpuSolver.AddBody(GetRandomModel(), _bodyMass,
                    position + objectPosition + offsetPosition, GetRandomRotation());
            }
        }

        private Model GetRandomModel() => _models[Random.Shared.Next(_models.Count)];
    }
}
