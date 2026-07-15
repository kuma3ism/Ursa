using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using Ursa.Inputs;

namespace Ursa.InputSystemIntegration
{
    internal sealed class UrsaInputSystemProvider : IUrsaInputProvider
    {
        internal static readonly UrsaInputSystemProvider Instance = new UrsaInputSystemProvider();

        private readonly HashSet<int> _pressedPointerIds = new HashSet<int>();
        private readonly List<UrsaPointerDownEvent> _pendingPointerDowns = new List<UrsaPointerDownEvent>(8);
        private bool _escapePressed;
        private bool _backPressedPending;

        private UrsaInputSystemProvider()
        {
        }

        public void Poll(List<UrsaPointerDownEvent> pointerDowns, out bool backPressed)
        {
            pointerDowns.AddRange(_pendingPointerDowns);
            _pendingPointerDowns.Clear();
            CollectTouches(pointerDowns);
            backPressed = _backPressedPending;
            _backPressedPending = false;
        }

        internal void Enable()
        {
            InputSystem.onEvent -= OnInputEvent;
            _pressedPointerIds.Clear();
            _pendingPointerDowns.Clear();
            _escapePressed = false;
            _backPressedPending = false;
            InputSystem.onEvent += OnInputEvent;
        }

        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())
                return;

            if (device is Keyboard keyboard)
            {
                CollectBack(eventPtr, keyboard.escapeKey);
                return;
            }

            // Touchscreen rewrites low-level TouchState events internally. Read the finalized
            // per-finger controls from Poll instead of interpreting the preprocessed event here.
            if (device is Touchscreen)
                return;

            if (device is Pen pen)
            {
                CollectPointer(eventPtr, pen.deviceId, pen.tip, pen.position, UrsaPointerDeviceKind.Pen);
                return;
            }

            if (device is Mouse mouse)
                CollectPointer(eventPtr, mouse.deviceId, mouse.leftButton, mouse.position, UrsaPointerDeviceKind.Mouse);
        }

        private static void CollectTouches(List<UrsaPointerDownEvent> pointerDowns)
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen == null)
                return;

            for (var index = 0; index < touchscreen.touches.Count; index++)
            {
                var touch = touchscreen.touches[index];
                if (!touch.press.wasPressedThisFrame &&
                    touch.phase.ReadValue() != UnityEngine.InputSystem.TouchPhase.Began)
                    continue;

                pointerDowns.Add(new UrsaPointerDownEvent(
                    (touchscreen.deviceId << 8) | index,
                    touch.position.ReadValue(),
                    UrsaPointerDeviceKind.Touch));
            }
        }

        private void CollectPointer(
            InputEventPtr eventPtr,
            int pointerId,
            ButtonControl press,
            Vector2Control position,
            UrsaPointerDeviceKind deviceKind)
        {
            if (!press.ReadValueFromEvent(eventPtr, out var pressValue))
                return;

            var isPressed = press.IsValueConsideredPressed(pressValue);
            if (!isPressed)
            {
                _pressedPointerIds.Remove(pointerId);
                return;
            }

            if (!_pressedPointerIds.Add(pointerId))
                return;

            if (!position.ReadValueFromEvent(eventPtr, out var screenPosition))
                screenPosition = position.ReadValue();
            _pendingPointerDowns.Add(new UrsaPointerDownEvent(pointerId, screenPosition, deviceKind));
        }

        private void CollectBack(InputEventPtr eventPtr, ButtonControl escapeKey)
        {
            if (!escapeKey.ReadValueFromEvent(eventPtr, out var value))
                return;

            var isPressed = escapeKey.IsValueConsideredPressed(value);
            if (isPressed && !_escapePressed)
                _backPressedPending = true;
            _escapePressed = isPressed;
        }
    }

    internal static class UrsaInputSystemBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            UrsaInputSystemProvider.Instance.Enable();
            UrsaInputProviderRegistry.Register(UrsaInputSystemProvider.Instance);
            UrsaInputRuntime.RequestEnsureExists();
        }
    }
}
