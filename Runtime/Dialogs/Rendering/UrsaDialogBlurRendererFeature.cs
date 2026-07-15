using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using Ursa.UI;

namespace Ursa.Dialogs.Rendering
{
    public sealed class UrsaDialogBlurRendererFeature : ScriptableRendererFeature
    {
        private static int _lastEnqueuedFrame = -1000;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _lastEnqueuedFrame = -1000;
        }

        public static bool WasEnqueuedRecently => Time.frameCount - _lastEnqueuedFrame <= 2;

        [Serializable]
        public sealed class Settings
        {
            public RenderPassEvent PassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            public string TextureName = "_UrsaDialogBlurTexture";
            [Range(1, 4)] public int Downsample = 1;
            public FilterMode FilterMode = FilterMode.Bilinear;
        }

        [SerializeField] private Settings _settings = new Settings();

        private CopyColorPass _pass;

        public override void Create()
        {
            _pass = new CopyColorPass();
            _pass.Setup(_settings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cameraType = renderingData.cameraData.cameraType;
            if (cameraType != CameraType.Game && cameraType != CameraType.SceneView)
                return;
            if (!ShouldCopyCamera(renderingData.cameraData.camera, renderingData.cameraData.renderType))
                return;

            if (_pass == null)
                Create();

            _pass.Setup(_settings);
            renderer.EnqueuePass(_pass);
            _lastEnqueuedFrame = Time.frameCount;
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            _pass = null;
        }

        private sealed class CopyColorPass : ScriptableRenderPass
        {
            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Ursa Dialog Blur Copy");
            private Settings _settings;
            private RTHandle _copyTexture;
            private int _textureId;

            public void Setup(Settings settings)
            {
                _settings = settings ?? new Settings();
                renderPassEvent = _settings.PassEvent;
                _textureId = Shader.PropertyToID(_settings.TextureName);
            }

            [Obsolete("Compatibility-mode path for URP versions that do not execute RenderGraph passes.")]
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
#pragma warning disable 0618
                var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;

                int downsample = Mathf.Max(1, _settings.Downsample);
                descriptor.width = Mathf.Max(1, descriptor.width / downsample);
                descriptor.height = Mathf.Max(1, descriptor.height / downsample);

                RenderingUtils.ReAllocateIfNeeded(
                    ref _copyTexture,
                    descriptor,
                    _settings.FilterMode,
                    TextureWrapMode.Clamp,
                    name: _settings.TextureName);
#pragma warning restore 0618
            }


            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();

                if (cameraData.cameraType != CameraType.Game && cameraData.cameraType != CameraType.SceneView)
                    return;
                if (!ShouldCopyCamera(cameraData.camera, cameraData.renderType))
                    return;

                var descriptor = cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;

                int downsample = Mathf.Max(1, _settings.Downsample);
                descriptor.width = Mathf.Max(1, descriptor.width / downsample);
                descriptor.height = Mathf.Max(1, descriptor.height / downsample);

                RenderingUtils.ReAllocateHandleIfNeeded(
                    ref _copyTexture,
                    descriptor,
                    _settings.FilterMode,
                    TextureWrapMode.Clamp,
                    name: _settings.TextureName);

                Shader.SetGlobalTexture(_textureId, _copyTexture);

                var destination = renderGraph.ImportTexture(_copyTexture);
                if (!resourceData.activeColorTexture.IsValid() || !destination.IsValid())
                    return;

                using var builder = renderGraph.AddBlitPass(
                    resourceData.activeColorTexture,
                    destination,
                    Vector2.one,
                    Vector2.zero,
                    returnBuilder: true,
                    passName: "Ursa Dialog Blur Copy");
                builder.AllowPassCulling(false);
            }

            [Obsolete("Compatibility-mode path for URP versions that do not execute RenderGraph passes.")]
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
#pragma warning disable 0618
                var cameraType = renderingData.cameraData.cameraType;
                if (cameraType != CameraType.Game && cameraType != CameraType.SceneView)
                    return;
                if (!ShouldCopyCamera(renderingData.cameraData.camera, renderingData.cameraData.renderType))
                    return;

                var cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, _profilingSampler))
                {
                    var source = renderingData.cameraData.renderer.cameraColorTargetHandle;
                    Blitter.BlitCameraTexture(cmd, source, _copyTexture);
                    cmd.SetGlobalTexture(_textureId, _copyTexture.nameID);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
#pragma warning restore 0618
            }

            public void Dispose()
            {
                _copyTexture?.Release();
                _copyTexture = null;
            }
        }

        private static bool ShouldCopyCamera(Camera camera, CameraRenderType renderType)
        {
            return renderType == CameraRenderType.Base || UrsaUICamera.IsBlurSourceUICamera(camera);
        }
    }
}
