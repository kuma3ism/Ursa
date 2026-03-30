using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Ursa.Blur
{
    /// <summary>
    /// スクリーンショット方式のブラーエフェクト。
    /// ポップアップが開く直前に画面をキャプチャし、ガウシアンブラーをかけたテクスチャを背景として表示します。
    /// カメラ構成への依存なし、URP/Built-in 両対応。
    ///
    /// 【セットアップ】
    /// 1. このコンポーネントを任意の GameObject にアタッチ
    /// 2. BlurImage に背景として使う RawImage を設定
    /// 3. BlurMaterial に ScreenshotBlur マテリアルを設定（未設定でもブラーなしで動作）
    ///
    /// 【BlurController との連携】
    /// BlurController を同シーンに置くことで、PushAsync / PushInstanceAsync 時に
    /// UrsaSceneManager が自動的に PlayBlurAsync / StopBlurAsync を呼び出します。
    /// </summary>
    public class ScreenshotBlurEffect : BlurEffectBase
    {
        [SerializeField] private RawImage _blurImage;
        [SerializeField] private Material _blurMaterial;
        [SerializeField] private float _blurSize = 3f;
        [SerializeField] private int _iterations = 4;
        [SerializeField] private float _fadeDuration = 0.2f;

        private Texture2D _screenshotTexture;
        private RenderTexture _blurredTexture;

        /// <summary>
        /// 現在の画面をキャプチャしてブラーをかけ、背景として表示します。
        /// スクリーンショットはこのメソッド冒頭で撮影するため、
        /// 呼び出し元はシーンロード前（ポップアップが映り込む前）に呼ぶ必要があります。
        /// </summary>
        public override async Task PlayBlurAsync()
        {
            // エンドオブフレームまで待ってから撮影することで、
            // 現在フレームの描画結果（ポップアップなし）を確実にキャプチャする
            yield return new WaitForEndOfFrame() を使いたいが Task では使えないため、
            // RendererFrame の完了を待つ
            await Task.Yield();

            CaptureScreenshot();
            ApplyBlur();
            await FadeIn();
        }

        /// <summary>
        /// ブラー背景をフェードアウトして非表示にします。
        /// </summary>
        public override async Task StopBlurAsync()
        {
            await FadeOut();
            Cleanup();
        }

        private void CaptureScreenshot()
        {
            if (_screenshotTexture != null)
                Destroy(_screenshotTexture);

            _screenshotTexture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            _screenshotTexture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            _screenshotTexture.Apply();
        }

        private void ApplyBlur()
        {
            if (_blurMaterial == null)
            {
                // マテリアル未設定の場合はそのまま表示（ブラーなし）
                _blurImage.texture = _screenshotTexture;
                _blurImage.gameObject.SetActive(true);
                return;
            }

            // RenderTexture にブラーをかけながら描画（解像度を半分にして軽量化）
            var rt = RenderTexture.GetTemporary(Screen.width / 2, Screen.height / 2, 0);
            Graphics.Blit(_screenshotTexture, rt);

            for (int i = 0; i < _iterations; i++)
            {
                var rt2 = RenderTexture.GetTemporary(rt.width, rt.height, 0);
                _blurMaterial.SetFloat("_BlurSize", _blurSize * (i + 1));
                Graphics.Blit(rt, rt2, _blurMaterial);
                RenderTexture.ReleaseTemporary(rt);
                rt = rt2;
            }

            if (_blurredTexture != null)
                RenderTexture.ReleaseTemporary(_blurredTexture);

            _blurredTexture = rt;
            _blurImage.texture = _blurredTexture;
            _blurImage.gameObject.SetActive(true);
            _blurImage.color = new Color(1, 1, 1, 0);
        }

        private async Task FadeIn()
        {
            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Clamp01(elapsed / _fadeDuration);
                _blurImage.color = new Color(1, 1, 1, alpha);
                await Task.Yield();
            }
            _blurImage.color = Color.white;
        }

        private async Task FadeOut()
        {
            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = 1f - Mathf.Clamp01(elapsed / _fadeDuration);
                _blurImage.color = new Color(1, 1, 1, alpha);
                await Task.Yield();
            }
            _blurImage.color = new Color(1, 1, 1, 0);
            _blurImage.gameObject.SetActive(false);
        }

        private void Cleanup()
        {
            if (_screenshotTexture != null)
            {
                Destroy(_screenshotTexture);
                _screenshotTexture = null;
            }

            if (_blurredTexture != null)
            {
                RenderTexture.ReleaseTemporary(_blurredTexture);
                _blurredTexture = null;
            }
        }

        private void OnDestroy()
        {
            Cleanup();
        }
    }
}
