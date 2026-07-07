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
        private const string LegacyCameraName = "[Ursa] UICamera";
        private const string SceneCameraName = "[Ursa] Scene UICamera";
        private const string DialogBackgroundCameraName = "[Ursa] Dialog Background UICamera";
        private const string DialogCameraName = "[Ursa] Dialog UICamera";
        private const string UniversalCameraDataTypeName =
            "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime";
        private const string CameraRenderTypeTypeName =
            "UnityEngine.Rendering.Universal.CameraRenderType, Unity.RenderPipelines.Universal.Runtime";
        private const string AntialiasingModeTypeName =
            "UnityEngine.Rendering.Universal.AntialiasingMode, Unity.RenderPipelines.Universal.Runtime";
        private const string CameraOverrideOptionTypeName =
            "UnityEngine.Rendering.Universal.CameraOverrideOption, Unity.RenderPipelines.Universal.Runtime";

        private static Camera _sceneCamera;
        private static Camera _dialogBackgroundCamera;
        private static Camera _dialogCamera;
        private static Type _universalCameraDataType;
        private static Type _cameraRenderTypeType;
        private static Type _antialiasingModeType;
        private static Type _cameraOverrideOptionType;

        public static Camera Ensure()
        {
            return EnsureDialogCamera();
        }

        public static Camera EnsureSceneCamera()
        {
            if (_sceneCamera != null && _sceneCamera.gameObject != null)
            {
                Configure(_sceneCamera);
                return _sceneCamera;
            }

            var existing = GameObject.Find(SceneCameraName);
            if (existing != null)
                _sceneCamera = existing.GetComponent<Camera>();

            if (_sceneCamera == null)
            {
                var go = existing != null ? existing : new GameObject(SceneCameraName);
                go.name = SceneCameraName;
                _sceneCamera = go.AddComponent<Camera>();
            }

            UnityEngine.Object.DontDestroyOnLoad(_sceneCamera.gameObject);
            Configure(_sceneCamera);
            return _sceneCamera;
        }

        public static Camera EnsureDialogCamera()
        {
            if (_dialogCamera != null && _dialogCamera.gameObject != null)
            {
                Configure(_dialogCamera);
                return _dialogCamera;
            }

            var existing = GameObject.Find(DialogCameraName);
            if (existing == null)
                existing = GameObject.Find(LegacyCameraName);
            if (existing != null)
                _dialogCamera = existing.GetComponent<Camera>();

            if (_dialogCamera == null)
            {
                var go = existing != null ? existing : new GameObject(DialogCameraName);
                go.name = DialogCameraName;
                _dialogCamera = go.AddComponent<Camera>();
            }

            UnityEngine.Object.DontDestroyOnLoad(_dialogCamera.gameObject);
            Configure(_dialogCamera);
            return _dialogCamera;
        }

        public static Camera EnsureDialogBackgroundCamera()
        {
            if (_dialogBackgroundCamera != null && _dialogBackgroundCamera.gameObject != null)
            {
                Configure(_dialogBackgroundCamera);
                return _dialogBackgroundCamera;
            }

            var existing = GameObject.Find(DialogBackgroundCameraName);
            if (existing != null)
                _dialogBackgroundCamera = existing.GetComponent<Camera>();

            if (_dialogBackgroundCamera == null)
            {
                var go = existing != null ? existing : new GameObject(DialogBackgroundCameraName);
                go.name = DialogBackgroundCameraName;
                _dialogBackgroundCamera = go.AddComponent<Camera>();
            }

            UnityEngine.Object.DontDestroyOnLoad(_dialogBackgroundCamera.gameObject);
            Configure(_dialogBackgroundCamera);
            return _dialogBackgroundCamera;
        }

        public static bool IsBlurSourceUICamera(Camera camera)
        {
            var sceneCamera = _sceneCamera;
            var dialogBackgroundCamera = _dialogBackgroundCamera;
            return camera != null &&
                   ((sceneCamera != null && camera == sceneCamera) ||
                    (dialogBackgroundCamera != null && camera == dialogBackgroundCamera) ||
                    string.Equals(camera.gameObject.name, SceneCameraName, StringComparison.Ordinal) ||
                    string.Equals(camera.gameObject.name, DialogBackgroundCameraName, StringComparison.Ordinal));
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

            var sceneCamera = EnsureSceneCamera();
            var dialogBackgroundCamera = EnsureDialogBackgroundCamera();
            var dialogCamera = EnsureDialogCamera();
            if (baseCamera == sceneCamera || baseCamera == dialogBackgroundCamera || baseCamera == dialogCamera)
                return;

            if (exclusive)
            {
                DetachFromAllBaseCameras(sceneCamera);
                DetachFromAllBaseCameras(dialogBackgroundCamera);
                DetachFromAllBaseCameras(dialogCamera);
            }

            var baseData = GetOrAddUniversalCameraData(baseCamera);
            if (baseData == null)
                return;
            if (!HasEnumPropertyValue(baseData, "renderType", "Base"))
                return;

            var sceneData = GetOrAddUniversalCameraData(sceneCamera);
            var dialogBackgroundData = GetOrAddUniversalCameraData(dialogBackgroundCamera);
            var dialogData = GetOrAddUniversalCameraData(dialogCamera);
            if (sceneData == null || dialogBackgroundData == null || dialogData == null)
                return;

            SetEnumPropertyValue(sceneData, "renderType", "Overlay");
            SetEnumPropertyValue(dialogBackgroundData, "renderType", "Overlay");
            SetEnumPropertyValue(dialogData, "renderType", "Overlay");

            RemoveCameraFromStack(baseData, sceneCamera);
            RemoveCameraFromStack(baseData, dialogBackgroundCamera);
            RemoveCameraFromStack(baseData, dialogCamera);
            AddCameraToStack(baseData, sceneCamera);
            AddCameraToStack(baseData, dialogBackgroundCamera);
            AddCameraToStack(baseData, dialogCamera);
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
                if (camera == null || !camera.enabled || camera == _sceneCamera || camera == _dialogBackgroundCamera || camera == _dialogCamera)
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
