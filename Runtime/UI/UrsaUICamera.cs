using System;
using System.Collections;
using UnityEngine;

namespace Ursa.UI
{
    /// <summary>
    /// Provides the shared camera used by Ursa-managed Screen Space - Camera canvases.
    /// </summary>
    public static class UrsaUICamera
    {
        private const string CameraName = "[Ursa] UICamera";
        private const string UniversalCameraDataTypeName =
            "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime";
        private const string CameraRenderTypeTypeName =
            "UnityEngine.Rendering.Universal.CameraRenderType, Unity.RenderPipelines.Universal.Runtime";
        private const string AntialiasingModeTypeName =
            "UnityEngine.Rendering.Universal.AntialiasingMode, Unity.RenderPipelines.Universal.Runtime";
        private const string CameraOverrideOptionTypeName =
            "UnityEngine.Rendering.Universal.CameraOverrideOption, Unity.RenderPipelines.Universal.Runtime";

        private static Camera _camera;
        private static Type _universalCameraDataType;
        private static Type _cameraRenderTypeType;
        private static Type _antialiasingModeType;
        private static Type _cameraOverrideOptionType;

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

            UnityEngine.Object.DontDestroyOnLoad(_camera.gameObject);
            Configure(_camera);
            return _camera;
        }

        public static void AttachToBaseCamera(Camera baseCamera)
        {
            AttachToBaseCamera(baseCamera, false);
        }

        public static void AttachToBaseCameraExclusive(Camera baseCamera)
        {
            AttachToBaseCamera(baseCamera, true);
        }

        private static void AttachToBaseCamera(Camera baseCamera, bool exclusive)
        {
            if (baseCamera == null)
                return;

            var uiCamera = Ensure();
            if (baseCamera == uiCamera)
                return;

            if (exclusive)
                DetachFromAllBaseCameras(uiCamera);

            var baseData = GetOrAddUniversalCameraData(baseCamera);
            if (baseData == null)
                return;
            if (!HasEnumPropertyValue(baseData, "renderType", "Base"))
                return;

            var uiData = GetOrAddUniversalCameraData(uiCamera);
            if (uiData == null)
                return;

            SetEnumPropertyValue(uiData, "renderType", "Overlay");
            AddCameraToStack(baseData, uiCamera);
        }

        public static void AttachToActiveBaseCameras()
        {
            AttachToBestActiveBaseCamera();
        }

        public static void AttachToBestActiveBaseCamera()
        {
            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            Camera bestCamera = null;
            foreach (var camera in cameras)
            {
                if (camera == null || !camera.enabled || camera == _camera)
                    continue;

                var cameraData = GetUniversalCameraData(camera);
                if (cameraData != null && !HasEnumPropertyValue(cameraData, "renderType", "Base"))
                    continue;

                if (bestCamera == null || camera.depth > bestCamera.depth)
                    bestCamera = camera;
            }

            if (bestCamera != null)
                AttachToBaseCameraExclusive(bestCamera);
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

            var cameraData = GetOrAddUniversalCameraData(camera);
            if (cameraData == null)
                return;

            SetEnumPropertyValue(cameraData, "renderType", "Overlay");
            SetPropertyValue(cameraData, "renderPostProcessing", false);
            SetEnumPropertyValue(cameraData, "antialiasing", "None");
            SetEnumPropertyValue(cameraData, "requiresColorOption", "Off");
            SetEnumPropertyValue(cameraData, "requiresDepthOption", "Off");
        }

        private static Component GetOrAddUniversalCameraData(Camera camera)
        {
            var cameraDataType = GetUniversalCameraDataType();
            if (cameraDataType == null)
                return null;

            var cameraData = camera.GetComponent(cameraDataType);
            if (cameraData == null)
                cameraData = camera.gameObject.AddComponent(cameraDataType);
            return cameraData;
        }

        private static Component GetUniversalCameraData(Camera camera)
        {
            var cameraDataType = GetUniversalCameraDataType();
            return cameraDataType == null ? null : camera.GetComponent(cameraDataType);
        }

        private static Type GetUniversalCameraDataType()
        {
            return _universalCameraDataType ??= Type.GetType(UniversalCameraDataTypeName);
        }

        private static bool HasEnumPropertyValue(object target, string propertyName, string valueName)
        {
            var property = target.GetType().GetProperty(propertyName);
            if (property == null)
                return false;

            var value = property.GetValue(target);
            return value != null && string.Equals(value.ToString(), valueName, StringComparison.Ordinal);
        }

        private static void SetEnumPropertyValue(object target, string propertyName, string valueName)
        {
            var property = target.GetType().GetProperty(propertyName);
            if (property == null || !property.CanWrite)
                return;

            var enumType = property.PropertyType;
            if (!enumType.IsEnum)
                enumType = ResolveUniversalEnumType(propertyName);
            if (enumType == null || !enumType.IsEnum)
                return;

            var value = Enum.Parse(enumType, valueName);
            property.SetValue(target, value);
        }

        private static Type ResolveUniversalEnumType(string propertyName)
        {
            return propertyName switch
            {
                "renderType" => _cameraRenderTypeType ??= Type.GetType(CameraRenderTypeTypeName),
                "antialiasing" => _antialiasingModeType ??= Type.GetType(AntialiasingModeTypeName),
                "requiresColorOption" or "requiresDepthOption" => _cameraOverrideOptionType ??= Type.GetType(CameraOverrideOptionTypeName),
                _ => null,
            };
        }

        private static void SetPropertyValue(object target, string propertyName, object value)
        {
            var property = target.GetType().GetProperty(propertyName);
            if (property == null || !property.CanWrite)
                return;

            property.SetValue(target, value);
        }

        private static void AddCameraToStack(object baseCameraData, Camera uiCamera)
        {
            var property = baseCameraData.GetType().GetProperty("cameraStack");
            if (property?.GetValue(baseCameraData) is not IList cameraStack)
                return;

            if (!cameraStack.Contains(uiCamera))
                cameraStack.Add(uiCamera);
        }

        private static void DetachFromAllBaseCameras(Camera uiCamera)
        {
            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var camera in cameras)
            {
                if (camera == null || camera == uiCamera)
                    continue;

                var cameraData = GetUniversalCameraData(camera);
                RemoveCameraFromStack(cameraData, uiCamera);
            }
        }

        private static void RemoveCameraFromStack(object baseCameraData, Camera uiCamera)
        {
            if (baseCameraData == null)
                return;

            var property = baseCameraData.GetType().GetProperty("cameraStack");
            if (property?.GetValue(baseCameraData) is not IList cameraStack)
                return;

            if (cameraStack.Contains(uiCamera))
                cameraStack.Remove(uiCamera);
        }
    }
}
