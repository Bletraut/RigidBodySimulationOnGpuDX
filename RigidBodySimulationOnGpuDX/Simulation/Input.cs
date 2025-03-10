using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace RigidBodySimulationOnGpuDX
{
    public static class Input
    {
        public static Point MousePosition => _currentMouseState.Position;

        public static Point MousePositionDelta
            => _currentMouseState.Position - _lastMouseState.Position;

        private static MouseState _lastMouseState;
        private static MouseState _currentMouseState;

        private static KeyboardState _lastKeyboardState;
        private static KeyboardState _currentKeyboardState;

        public static bool IsMouseButtonDown(MouseButton button)
        {
            var lastButtonState = GetMouseButtonState(button, ref _lastMouseState);
            var currentButtonState = GetMouseButtonState(button, ref _currentMouseState);

            var isDown = lastButtonState == ButtonState.Released
                && currentButtonState == ButtonState.Pressed;

            return isDown;
        }

        public static bool IsMouseButtonUp(MouseButton button)
        {
            var lastButtonState = GetMouseButtonState(button, ref _lastMouseState);
            var currentButtonState = GetMouseButtonState(button, ref _currentMouseState);

            var isUp = lastButtonState == ButtonState.Pressed
                && currentButtonState == ButtonState.Released;

            return isUp;
        }

        public static bool IsMouseButtonPressed(MouseButton button)
        {
            var currentButtonState = GetMouseButtonState(button, ref _currentMouseState);

            var isPressed = currentButtonState == ButtonState.Pressed;

            return isPressed;
        }

        public static bool IsKeyDown(Keys key)
            => _lastKeyboardState.IsKeyUp(key) && _currentKeyboardState.IsKeyDown(key);

        public static bool IsKeyUp(Keys key)
            => _lastKeyboardState.IsKeyDown(key) && _currentKeyboardState.IsKeyUp(key);

        public static bool IsKeyPressed(Keys key)
            => _currentKeyboardState.IsKeyDown(key);

        private static ButtonState GetMouseButtonState(MouseButton button, ref MouseState mouseState)
        {
            var buttonState = button switch
            {
                MouseButton.Left => mouseState.LeftButton,
                MouseButton.Right => mouseState.RightButton,
                MouseButton.Middle => mouseState.MiddleButton,
                MouseButton.XButton1 => mouseState.XButton1,
                MouseButton.YButton2 => mouseState.XButton2,
                _ => throw new System.NotImplementedException(),
            };

            return buttonState;
        }

        public static void Update()
        {
            _lastMouseState = _currentMouseState;
            _currentMouseState = Mouse.GetState();

            _lastKeyboardState = _currentKeyboardState;
            _currentKeyboardState = Keyboard.GetState();
        }
    }

    public enum MouseButton
    {
        Left = 0,
        Right = 1,
        Middle = 2,
        XButton1 = 3,
        YButton2 = 4,
    }
}
