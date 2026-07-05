using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ursa.UI
{
    /// <summary>
    /// Configures Ursa-managed canvases to use the shared UI camera and stable sorting orders.
    /// </summary>
    public static class UrsaUICanvasUtility
    {
        public static void ConfigureSceneCanvas(Canvas canvas, int sceneIndex)
        {
            Configure(canvas, UrsaUIRenderOrder.SceneBase + sceneIndex * UrsaUIRenderOrder.SceneStep);
        }

        public static void ConfigureDialogCanvas(Canvas canvas)
        {
            Configure(canvas, UrsaUIRenderOrder.Dialog);
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
                    ConfigureSceneCanvas(marker.GetComponent<Canvas>(), sceneIndex);
                    marker.GetComponent<Canvas>().enabled = visible;
                }

                var canvases = root.GetComponentsInChildren<Canvas>(true);
                foreach (var canvas in canvases)
                {
                    if (canvas.GetComponent<UrsaUICanvas>() != null)
                        continue;

                    if (canvas.gameObject.name == "UiCanvas")
                    {
                        ConfigureSceneCanvas(canvas, sceneIndex);
                        canvas.enabled = visible;
                    }
                    else if (canvas.gameObject.name == "TapEffectCanvas" || canvas.gameObject.name == "[Ursa] TapEffectCanvas")
                    {
                        ConfigureTapEffectCanvas(canvas);
                        canvas.enabled = visible;
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
    }
}
