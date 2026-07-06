using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ursa.UI
{
    /// <summary>
    /// Configures Ursa-managed canvases to use the shared UI camera and stable sorting orders.
    /// </summary>
    public static class UrsaUICanvasUtility
    {
        private static readonly HashSet<int> WarnedOverlayCanvasIds = new HashSet<int>();

        public static void ConfigureSceneCanvas(Canvas canvas, int sceneIndex)
        {
            Configure(canvas, UrsaUIRenderOrder.SceneBase + sceneIndex * UrsaUIRenderOrder.SceneStep);
        }

        public static void ConfigureDialogCanvas(Canvas canvas)
        {
            ConfigureDialogContentCanvas(canvas);
        }

        public static void ConfigureDialogBarrierCanvas(Canvas canvas)
        {
            Configure(canvas, UrsaUIRenderOrder.DialogBarrier);
            if (canvas != null)
                canvas.overrideSorting = true;
        }

        public static void ConfigureDialogContentCanvas(Canvas canvas)
        {
            Configure(canvas, UrsaUIRenderOrder.DialogContent);
            if (canvas != null)
                canvas.overrideSorting = true;
        }

        public static void ConfigureTransitionCanvas(Canvas canvas)
        {
            Configure(canvas, UrsaUIRenderOrder.Transition);
        }

        public static void ConfigureTapEffectCanvas(Canvas canvas)
        {
            Configure(canvas, UrsaUIRenderOrder.TapEffect);
        }

        public static void ConfigureManagedObject(GameObject go)
        {
            if (go == null) return;

            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
                SetLayerRecursively(go, uiLayer);
        }

        public static void SyncSceneCanvases(Scene scene, int sceneIndex)
        {
            SyncSceneCanvases(scene, sceneIndex, true);
        }

        public static void SyncSceneCanvases(Scene scene, int sceneIndex, bool visible)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                var markers = root.GetComponentsInChildren<UrsaUICanvas>(true);
                foreach (var marker in markers)
                {
                    var canvas = marker.GetComponent<Canvas>();
                    WarnIfOverlayCanvas(canvas, "UrsaUICanvas marker");
                    ConfigureSceneCanvas(canvas, sceneIndex);
                    canvas.enabled = visible;
                }

                var canvases = root.GetComponentsInChildren<Canvas>(true);
                foreach (var canvas in canvases)
                {
                    if (canvas.GetComponent<UrsaUICanvas>() != null)
                        continue;

                    if (canvas.gameObject.name == "UiCanvas")
                    {
                        WarnIfOverlayCanvas(canvas, "UiCanvas");
                        ConfigureSceneCanvas(canvas, sceneIndex);
                        canvas.enabled = visible;
                    }
                    else if (canvas.gameObject.name == "TapEffectCanvas" || canvas.gameObject.name == "[Ursa] TapEffectCanvas")
                    {
                        WarnIfOverlayCanvas(canvas, "TapEffectCanvas");
                        ConfigureTapEffectCanvas(canvas);
                        canvas.enabled = visible;
                    }
                    else
                    {
                        WarnIfOverlayCanvas(canvas, "unmanaged canvas");
                    }
                }
            }
        }

        private static void Configure(Canvas canvas, int sortingOrder)
        {
            if (canvas == null) return;

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = UrsaUICamera.Ensure();
            canvas.planeDistance = 1f;
            canvas.sortingOrder = sortingOrder;

            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
                SetLayerRecursively(canvas.gameObject, uiLayer);
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private static void WarnIfOverlayCanvas(Canvas canvas, string context)
        {
            if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                return;

            int id = canvas.GetInstanceID();
            if (!WarnedOverlayCanvasIds.Add(id))
                return;

            Debug.LogWarning($"[Ursa] Screen Space - Overlay Canvas detected ({context}): {canvas.name}. Ursa-managed realtime blur only includes camera-rendered UI. Use Screen Space - Camera / UrsaUICanvas for blur-compatible UI.");
        }
    }
}
