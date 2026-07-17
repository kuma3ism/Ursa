using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Ursa.Inputs
{
    internal enum UrsaPointerDeviceKind
    {
        Mouse,
        Touch,
        Pen
    }

    internal readonly struct UrsaPointerDownEvent
    {
        public UrsaPointerDownEvent(int pointerId, Vector2 screenPosition, UrsaPointerDeviceKind deviceKind)
        {
            PointerId = pointerId;
            ScreenPosition = screenPosition;
            DeviceKind = deviceKind;
        }

        public int PointerId { get; }
        public Vector2 ScreenPosition { get; }
        public UrsaPointerDeviceKind DeviceKind { get; }
    }

    internal interface IUrsaInputProvider
    {
        void Poll(List<UrsaPointerDownEvent> pointerDowns, out bool backPressed);
    }

    internal interface IUrsaBackHandler
    {
        int BackPriority { get; }
        bool CanHandleBack();
        Task HandleBackAsync();
    }

    internal static class UrsaInputProviderRegistry
    {
        private static IUrsaInputProvider _provider;

        internal static IUrsaInputProvider Current => _provider;

        internal static void Register(IUrsaInputProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }
    }

    internal sealed class LegacyUrsaInputProvider : IUrsaInputProvider
    {
        private bool _disabled;
        private bool _warningEmitted;

        public void Poll(List<UrsaPointerDownEvent> pointerDowns, out bool backPressed)
        {
            backPressed = false;
            if (_disabled)
                return;

            try
            {
                backPressed = UnityEngine.Input.GetKeyDown(KeyCode.Escape);

                for (var index = 0; index < UnityEngine.Input.touchCount; index++)
                {
                    var touch = UnityEngine.Input.GetTouch(index);
                    if (touch.phase != TouchPhase.Began)
                        continue;

                    pointerDowns.Add(new UrsaPointerDownEvent(
                        touch.fingerId,
                        touch.position,
                        UrsaPointerDeviceKind.Touch));
                }

                if (UnityEngine.Input.GetMouseButtonDown(0))
                {
                    pointerDowns.Add(new UrsaPointerDownEvent(
                        -1,
                        UnityEngine.Input.mousePosition,
                        UrsaPointerDeviceKind.Mouse));
                }
            }
            catch (InvalidOperationException exception)
            {
                _disabled = true;
                if (_warningEmitted)
                    return;

                _warningEmitted = true;
                Debug.LogWarning($"[Ursa] Legacy Input is unavailable. Install the Input System adapter or enable the old Input Manager. {exception.Message}");
            }
        }

        internal void Reset()
        {
            _disabled = false;
            _warningEmitted = false;
        }
    }
}
