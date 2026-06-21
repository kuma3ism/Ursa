using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Ursa.Editor
{
    /// <summary>
    /// Hierarchy 右クリックメニューから Ursa UI 用のボタンを生成するエディタ拡張。
    /// TextMesh Pro が有効な場合はラベルに TextMeshProUGUI を、
    /// そうでなければ標準の UnityEngine.UI.Text を使用する。
    /// </summary>
    public static class UrsaButtonMenu
    {
        private const string MenuPath = "GameObject/Ursa UI/UrsaButton";
        private const string MenuItemName = "UrsaButton";
        private const int Priority = 10;

        // TextMesh Pro の型を遅延解決するためのフルネーム
        private const string TextMeshProTypeName = "TMPro.TextMeshProUGUI, Unity.TextMeshPro";

        /// <summary>
        /// UrsaButton を持つ GameObject をシーンに生成する。
        /// </summary>
        [MenuItem(MenuPath, false, Priority)]
        private static void CreateUrsaButton()
        {
            // 親を決定する。Canvas 配下が望ましい。
            Transform parent = GetAppropriateParent();

            // ボタン GameObject を生成
            var go = new GameObject(MenuItemName);
            var rectTransform = go.AddComponent<RectTransform>();

            // 親があればアタッチし、ローカル座標をリセット
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }
            else
            {
                // Canvas がなければ新規作成してその下に配置
                var canvasGo = CreateCanvas();
                go.transform.SetParent(canvasGo.transform, false);
            }

            rectTransform.localScale = Vector3.one;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = new Vector2(160f, 48f);

            // UrsaButton は Button を RequireComponent しているため先に Button を追加
            var button = go.AddComponent<Button>();

            // UrsaButton を追加
            var ursaButton = go.AddComponent<Ursa.UI.UrsaButton>();

            // 生成直後にシリアライズフィールド _button に Button を設定しておく
            // （private フィールドなので SerializedObject を使用）
            var serializedObject = new SerializedObject(ursaButton);
            var buttonProperty = serializedObject.FindProperty("_button");
            if (buttonProperty != null)
            {
                buttonProperty.objectReferenceValue = button;
                serializedObject.ApplyModifiedProperties();
            }

            // 標準的なボタン見た目のため Image を追加し、ターゲットグラフィックに設定
            var image = go.AddComponent<Image>();
            button.targetGraphic = image;
            image.color = new Color(1f, 1f, 1f, 1f);

            // ラベル用オブジェクトを子に追加
            var labelGo = CreateButtonLabel(go);

            // Undo 履歴に登録して選択状態にする
            Undo.RegisterCreatedObjectUndo(go, $"Create {MenuItemName}");
            if (labelGo != null)
                Undo.RegisterCreatedObjectUndo(labelGo, $"Create {MenuItemName} Label");

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }

        /// <summary>
        /// 常に作成可能とする（シーンに Canvas がなくても自動生成するため）。
        /// </summary>
        [MenuItem(MenuPath, true, Priority)]
        private static bool ValidateCreateUrsaButton()
        {
            return true;
        }

        /// <summary>
        /// 新規 Canvas を作成する。
        /// </summary>
        private static GameObject CreateCanvas()
        {
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
            return canvasGo;
        }

        /// <summary>
        /// 現在の選択に応じた適切な親を取得する。
        /// 選択が Canvas 以下であればその Transform、そうでなければシーン内の最初の Canvasを探す。
        /// </summary>
        private static Transform GetAppropriateParent()
        {
            GameObject active = Selection.activeGameObject;
            if (active != null)
            {
                // 選択自身が Canvas の場合はその下に入れる
                if (active.GetComponent<Canvas>() != null)
                {
                    return active.transform;
                }

                // 親方向に Canvas を探す
                var canvas = active.GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    return canvas.transform;
                }
            }

            // シーン内に既存の Canvas があればその下に入れる
            var sceneCanvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            if (sceneCanvases.Length > 0)
            {
                return sceneCanvases[0].transform;
            }

            return null;
        }

        /// <summary>
        /// ボタンラベルを生成する。
        /// TextMesh Pro が有効な場合は TextMeshProUGUI、なければ標準 Text を使用する。
        /// </summary>
        private static GameObject CreateButtonLabel(GameObject parent)
        {
            var labelGo = new GameObject("Text");
            var labelTransform = labelGo.AddComponent<RectTransform>();
            labelGo.transform.SetParent(parent.transform, false);
            labelTransform.anchorMin = Vector2.zero;
            labelTransform.anchorMax = Vector2.one;
            labelTransform.offsetMin = Vector2.zero;
            labelTransform.offsetMax = Vector2.zero;

            // リフレクションではなく Type.GetType で解決し、存在しなければ標準 Text を使用
            Type tmpType = Type.GetType(TextMeshProTypeName, false);
            if (tmpType != null)
            {
                AddTextMeshProLabel(labelGo, tmpType);
            }
            else
            {
                AddUnityTextLabel(labelGo);
            }

            return labelGo;
        }

        /// <summary>
        /// TextMesh Pro ラベルを設定する。
        /// </summary>
        private static void AddTextMeshProLabel(GameObject labelGo, Type tmpType)
        {
            var component = labelGo.AddComponent(tmpType);

            SetProperty(component, "text", "Button");
            SetProperty(component, "alignment", 514); // TMPro.TextAlignmentOptions.Center
            SetProperty(component, "color", Color.black);
            SetProperty(component, "raycastTarget", false);
        }

        /// <summary>
        /// 標準 Text ラベルを設定する。
        /// </summary>
        private static void AddUnityTextLabel(GameObject labelGo)
        {
            var text = labelGo.AddComponent<Text>();
            text.text = "Button";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.color = Color.black;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
        }

        /// <summary>
        /// コンポーネントのプロパティをリフレクション経由で設定する。
        /// </summary>
        private static void SetProperty(Component component, string propertyName, object value)
        {
            if (component == null) return;

            var property = component.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.CanWrite)
            {
                property.SetValue(component, value, null);
            }
        }
    }
}