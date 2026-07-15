using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using Ursa.UI;

namespace Ursa.UI.Rendering
{
    public sealed class UrsaTapRippleRendererFeature : ScriptableRendererFeature
    {
        [Serializable]
        public sealed class Settings
        {
            public RenderPassEvent PassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            public Shader Shader;
        }

        [SerializeField] private Settings _settings = new Settings();

        private RipplePass _pass;
        private Material _material;

        public static bool WasEnqueuedRecently => UrsaTapRippleState.WasRendererFeatureEnqueuedRecently;

        public override void Create()
        {
            CoreUtils.Destroy(_material);
            var shader = _settings.Shader != null
                ? _settings.Shader
                : Shader.Find("Hidden/Ursa/TapRipple");
            _material = shader == null ? null : CoreUtils.CreateEngineMaterial(shader);
            _pass = new RipplePass();
            _pass.Setup(_settings, _material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cameraData = renderingData.cameraData;
            if (cameraData.renderType != CameraRenderType.Base)
                return;
            if (cameraData.cameraType != CameraType.Game && cameraData.cameraType != CameraType.SceneView)
                return;
            if (!UrsaTapRippleState.HasActiveRipples || _material == null)
                return;

            if (_pass == null)
                Create();
            if (_material == null)
                return;

            _pass.Setup(_settings, _material);
            renderer.EnqueuePass(_pass);
            UrsaTapRippleState.MarkRendererFeatureEnqueued();
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            _pass = null;
            CoreUtils.Destroy(_material);
            _material = null;
        }

        private sealed class RipplePass : ScriptableRenderPass
        {
            private static readonly int RippleCountId = Shader.PropertyToID("_UrsaRippleCount");
            private static readonly int RippleAspectId = Shader.PropertyToID("_UrsaRippleAspect");
            private static readonly int RippleCentersId = Shader.PropertyToID("_UrsaRippleCenters");
            private static readonly int RippleParametersId = Shader.PropertyToID("_UrsaRippleParameters");

            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Ursa Tap Ripple");
            private readonly Vector4[] _centers = new Vector4[UrsaTapRippleState.MaximumRipples];
            private readonly Vector4[] _parameters = new Vector4[UrsaTapRippleState.MaximumRipples];

            private Material _material;
            private RTHandle _temporaryColor;

            internal void Setup(Settings settings, Material material)
            {
                renderPassEvent = settings?.PassEvent ?? RenderPassEvent.AfterRenderingPostProcessing;
                _material = material;
                requiresIntermediateTexture = true;
                ConfigureInput(ScriptableRenderPassInput.Color);
            }

            [Obsolete("Compatibility-mode path for URP versions that do not execute RenderGraph passes.")]
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
#pragma warning disable 0618
                var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;
                RenderingUtils.ReAllocateIfNeeded(
                    ref _temporaryColor,
                    descriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    name: "_UrsaTapRippleTemporaryColor");
#pragma warning restore 0618
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_material == null || !UrsaTapRippleState.HasActiveRipples)
                    return;

                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                if (resourceData.isActiveTargetBackBuffer || !resourceData.activeColorTexture.IsValid())
                    return;

                UpdateMaterial(cameraData.cameraTargetDescriptor);

                var source = resourceData.activeColorTexture;
                var destinationDescriptor = renderGraph.GetTextureDesc(source);
                destinationDescriptor.name = "Ursa Tap Ripple Color";
                destinationDescriptor.clearBuffer = false;
                var destination = renderGraph.CreateTexture(destinationDescriptor);

                var parameters = new RenderGraphUtils.BlitMaterialParameters(source, destination, _material, 0);
                renderGraph.AddBlitPass(parameters, "Ursa Tap Ripple");
                resourceData.cameraColor = destination;
            }

            [Obsolete("Compatibility-mode path for URP versions that do not execute RenderGraph passes.")]
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
#pragma warning disable 0618
                if (_material == null || !UrsaTapRippleState.HasActiveRipples)
                    return;

                UpdateMaterial(renderingData.cameraData.cameraTargetDescriptor);
                var commandBuffer = CommandBufferPool.Get();
                using (new ProfilingScope(commandBuffer, _profilingSampler))
                {
                    var source = renderingData.cameraData.renderer.cameraColorTargetHandle;
                    Blitter.BlitCameraTexture(commandBuffer, source, _temporaryColor, _material, 0);
                    Blitter.BlitCameraTexture(commandBuffer, _temporaryColor, source);
                }
                context.ExecuteCommandBuffer(commandBuffer);
                CommandBufferPool.Release(commandBuffer);
#pragma warning restore 0618
            }

            internal void Dispose()
            {
                _temporaryColor?.Release();
                _temporaryColor = null;
            }

            private void UpdateMaterial(RenderTextureDescriptor descriptor)
            {
                var count = UrsaTapRippleState.CopyShaderData(_centers, _parameters, descriptor.height);
                _material.SetInteger(RippleCountId, count);
                _material.SetFloat(RippleAspectId, descriptor.height <= 0 ? 1f : (float)descriptor.width / descriptor.height);
                _material.SetVectorArray(RippleCentersId, _centers);
                _material.SetVectorArray(RippleParametersId, _parameters);
            }
        }
    }
}
