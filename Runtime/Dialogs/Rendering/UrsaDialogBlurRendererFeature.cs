using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

namespace Ursa.Dialogs.Rendering
{
    public sealed class UrsaDialogBlurRendererFeature : ScriptableRendererFeature
    {
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
            if (_pass == null)
                Create();

            _pass.Setup(_settings);
            renderer.EnqueuePass(_pass);
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

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
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
            }


            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();

                if (cameraData.cameraType != CameraType.Game && cameraData.cameraType != CameraType.SceneView)
                    return;

                var descriptor = cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;

                int downsample = Mathf.Max(1, _settings.Downsample);
                descriptor.width = Mathf.Max(1, descriptor.width / downsample);
                descriptor.height = Mathf.Max(1, descriptor.height / downsample);

                var destination = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph,
                    descriptor,
                    _settings.TextureName,
                    false,
                    _settings.FilterMode);

                using var builder = renderGraph.AddBlitPass(
                    resourceData.activeColorTexture,
                    destination,
                    Vector2.one,
                    Vector2.zero,
                    returnBuilder: true,
                    passName: "Ursa Dialog Blur Copy");
                builder.SetGlobalTextureAfterPass(destination, _textureId);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var cameraType = renderingData.cameraData.cameraType;
                if (cameraType != CameraType.Game && cameraType != CameraType.SceneView)
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
            }

            public void Dispose()
            {
                _copyTexture?.Release();
                _copyTexture = null;
            }
        }
    }
}
