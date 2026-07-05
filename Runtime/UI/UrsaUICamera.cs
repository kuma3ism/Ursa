using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Ursa.UI
{
    /// <summary>
    /// Provides the shared camera used by Ursa-managed Screen Space - Camera canvases.
    /// </summary>
    public static class UrsaUICamera
    {
        private const string CameraName = "[Ursa] UICamera";
        private static Camera _camera;

        public static Camera Ensure()
        {
            if (_camera != null && _camera.gameObject != null)
            {
                Configure(_camera);
                return _camera;
            }

            var existing = GameObject.Find(CameraName);
            if (existing != null)
                _camera = existing.GetComponent<Camera>();

            if (_camera == null)
            {
                var go = existing != null ? existing : new GameObject(CameraName);
                _camera = go.AddComponent<Camera>();
            }

            Object.DontDestroyOnLoad(_camera.gameObject);
            Configure(_camera);
            return _camera;
        }

        public static void AttachToBaseCamera(Camera baseCamera)
        {
            if (baseCamera == null)
                return;

            var uiCamera = Ensure();
            if (baseCamera == uiCamera)
                return;

            var baseData = GetOrAddCameraData(baseCamera);
            if (baseData.renderType != CameraRenderType.Base)
                return;

            var uiData = GetOrAddCameraData(uiCamera);
            uiData.renderType = CameraRenderType.Overlay;

            if (!baseData.cameraStack.Contains(uiCamera))
                baseData.cameraStack.Add(uiCamera);
        }

        public static void AttachToActiveBaseCameras()
        {
            var cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var camera in cameras)
            {
                if (camera != null && camera.enabled)
                    AttachToBaseCamera(camera);
            }
        }

        private static void Configure(Camera camera)
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            camera.clearFlags = CameraClearFlags.Depth;
            camera.cullingMask = uiLayer >= 0 ? 1 << uiLayer : 0;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.depth = 100f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.useOcclusionCulling = false;

            var cameraData = GetOrAddCameraData(camera);
            cameraData.renderType = CameraRenderType.Overlay;
            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.None;
            cameraData.requiresColorOption = CameraOverrideOption.Off;
            cameraData.requiresDepthOption = CameraOverrideOption.Off;
        }

        private static UniversalAdditionalCameraData GetOrAddCameraData(Camera camera)
        {
            var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
                cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            return cameraData;
        }
    }
}
