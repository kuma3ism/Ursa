using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Ursa.Inputs;

namespace Ursa.InputSystemIntegration
{
    internal sealed class UrsaInputSystemProvider : IUrsaInputProvider
    {
        internal static readonly UrsaInputSystemProvider Instance = new UrsaInputSystemProvider();

        private readonly HashSet<int> _pressedPointerIds = new HashSet<int>();
        private bool _escapePressed;

        private UrsaInputSystemProvider()
        {
        }

        public void Poll(List<UrsaPointerDownEvent> pointerDowns, out bool backPressed)
        {
            var escapePressed = Keyboard.current?.escapeKey.isPressed == true;
            backPressed = escapePressed && !_escapePressed;
            _escapePressed = escapePressed;

            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                for (var index = 0; index < touchscreen.touches.Count; index++)
                {
                    var touch = touchscreen.touches[index];
                    var pointerId = (touchscreen.deviceId << 8) | index;
                    CollectPointer(
                        pointerDowns,
                        pointerId,
                        touch.press.isPressed,
                        touch.position.ReadValue(),
                        UrsaPointerDeviceKind.Touch);
                }
            }

            var pen = Pen.current;
            if (pen != null)
                CollectPointer(pointerDowns, pen.deviceId, pen.tip.isPressed, pen.position.ReadValue(), UrsaPointerDeviceKind.Pen);

            var mouse = Mouse.current;
            if (mouse != null)
                CollectPointer(pointerDowns, mouse.deviceId, mouse.leftButton.isPressed, mouse.position.ReadValue(), UrsaPointerDeviceKind.Mouse);
        }

        internal void Reset()
        {
            _pressedPointerIds.Clear();
            _escapePressed = false;
        }

        private void CollectPointer(
            List<UrsaPointerDownEvent> pointerDowns,
            int pointerId,
            bool isPressed,
            Vector2 position,
            UrsaPointerDeviceKind deviceKind)
        {
            if (!isPressed)
            {
                _pressedPointerIds.Remove(pointerId);
                return;
            }

            if (!_pressedPointerIds.Add(pointerId))
                return;

            pointerDowns.Add(new UrsaPointerDownEvent(pointerId, position, deviceKind));
        }
    }

    internal static class UrsaInputSystemBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            UrsaInputSystemProvider.Instance.Reset();
            UrsaInputProviderRegistry.Register(UrsaInputSystemProvider.Instance);
            UrsaInputRuntime.RequestEnsureExists();
        }
    }
}
