using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Ursa.Blur
{
    /// <summary>
    /// ブラーエフェクトを <see cref="BlurEffectBase"/> の実装に委譲するコンポーネント。
    /// このコンポーネントがあるシーンが Push されると、
    /// その一つ見た底のシーンにブラーが自動でかかります。
    /// </summary>
    [AddComponentMenu("Ursa/Blur Controller")]
    public class BlurController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("使用するブラーエフェクトのPrefab。未設定の場合は UrsaSettings のデフォルトを使用します。")]
        private BlurEffectBase _effectPrefab;

        public BlurEffectBase Effect { get; private set; }

        private void Awake()
        {
            var prefab = _effectPrefab ?? UrsaCore.Settings?.GetBlurPrefab();
            if (prefab != null)
                Effect = Instantiate(prefab);
        }

#if UNITY_EDITOR
        private void Reset()
        {
            var guids = AssetDatabase.FindAssets("ScreenshotBlurEffect t:Prefab");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _effectPrefab = AssetDatabase.LoadAssetAtPath<BlurEffectBase>(path);
            }
        }
#endif
    }
}
