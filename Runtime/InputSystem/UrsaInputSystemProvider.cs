using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;
using Ursa.Inputs;

namespace Ursa.InputSystemIntegration
{
    internal sealed class UrsaInputSystemProvider : IUrsaInputProvider
    {
        internal static readonly UrsaInputSystemProvider Instance = new UrsaInputSystemProvider();

        private readonly List<PendingPointerDown> _pendingPointerDowns = new List<PendingPointerDown>(8);
        private bool _backPressedPending;
        private IDisposable _buttonPressSubscription;

        private UrsaInputSystemProvider()
        {
        }

        public void Poll(List<UrsaPointerDownEvent> pointerDowns, out bool backPressed)
        {
            // onAnyButtonPress runs before the event is applied to device state.
            // Resolve the position here so a combined move-and-press event uses its new coordinates.
            for (var index = 0; index < _pendingPointerDowns.Count; index++)
            {
                var pending = _pendingPointerDowns[index];
                if (!pending.Position.device.added)
                    continue;

                pointerDowns.Add(new UrsaPointerDownEvent(
                    pending.PointerId,
                    pending.Position.ReadValue(),
                    pending.DeviceKind));
            }
            _pendingPointerDowns.Clear();
            CollectTouches(pointerDowns);
            backPressed = _backPressedPending;
            _backPressedPending = false;
        }

        internal void Enable()
        {
            _buttonPressSubscription?.Dispose();
            _pendingPointerDowns.Clear();
            _backPressedPending = false;
            _buttonPressSubscription = InputSystem.onAnyButtonPress.Call(OnButtonPressed);
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

        private void OnButtonPressed(InputControl control)
        {
            if (control.device is Keyboard keyboard && control == keyboard.escapeKey)
            {
                _backPressedPending = true;
                return;
            }

            if (control.device is Mouse mouse && control == mouse.leftButton)
            {
                _pendingPointerDowns.Add(new PendingPointerDown(
                    mouse.deviceId,
                    mouse.position,
                    UrsaPointerDeviceKind.Mouse));
                return;
            }

            if (control.device is Pen pen && control == pen.tip)
                _pendingPointerDowns.Add(new PendingPointerDown(
                    pen.deviceId,
                    pen.position,
                    UrsaPointerDeviceKind.Pen));
        }

        private readonly struct PendingPointerDown
        {
            internal PendingPointerDown(
                int pointerId,
                Vector2Control position,
                UrsaPointerDeviceKind deviceKind)
            {
                PointerId = pointerId;
                Position = position;
                DeviceKind = deviceKind;
            }

            internal int PointerId { get; }
            internal Vector2Control Position { get; }
            internal UrsaPointerDeviceKind DeviceKind { get; }
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
