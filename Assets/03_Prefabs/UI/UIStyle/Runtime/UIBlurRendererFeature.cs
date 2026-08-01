using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace UIStyle
{
    /// <summary>
    /// UI 배경을 RenderTexture에 렌더링하여 블러 효과에 사용하는 Renderer Feature (URP Render Graph).
    /// </summary>
    public class UIBlurRendererFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            [Tooltip("UI 배경 텍스처 해상도 (0 = 화면 해상도)")]
            public int textureResolution = 0;

            [Tooltip("다운샘플링 (성능 최적화)")]
            [Range(1, 4)]
            public int downsampling = 2;
        }

        public Settings settings = new Settings();

        private UIBlurRenderPass m_RenderPass;
        private RenderTexture m_UITexture;
        private static readonly int s_UITextureID = Shader.PropertyToID("_UIBlurTexture");
        private static readonly int s_UITextureTexelSizeID = Shader.PropertyToID("_UIBlurTexture_TexelSize");

        public override void Create()
        {
            m_RenderPass = new UIBlurRenderPass();
            m_RenderPass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.camera.cameraType == CameraType.Game ||
                renderingData.cameraData.camera.cameraType == CameraType.SceneView)
            {
                int width = settings.textureResolution > 0
                    ? settings.textureResolution
                    : renderingData.cameraData.camera.pixelWidth / settings.downsampling;
                int height = settings.textureResolution > 0
                    ? settings.textureResolution
                    : renderingData.cameraData.camera.pixelHeight / settings.downsampling;

                if (m_UITexture == null || m_UITexture.width != width || m_UITexture.height != height)
                {
                    if (m_UITexture != null)
                        m_UITexture.Release();

                    m_UITexture = new RenderTexture(width, height, 0, RenderTextureFormat.DefaultHDR);
                    m_UITexture.name = "UIBlurTexture";
                    m_UITexture.Create();
                }

                m_RenderPass.Setup(m_UITexture);
                renderer.EnqueuePass(m_RenderPass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            m_RenderPass?.DisposeImportedTarget();
            if (m_UITexture != null)
            {
                m_UITexture.Release();
                m_UITexture = null;
            }
        }

        private class UIBlurRenderPass : ScriptableRenderPass
        {
            private RenderTexture m_TargetTexture;
            private RTHandle m_DestRTHandle;

            public void Setup(RenderTexture texture)
            {
                if (m_TargetTexture != texture)
                {
                    DisposeImportedTarget();
                    m_TargetTexture = texture;
                    if (m_TargetTexture != null)
                        m_DestRTHandle = RTHandles.Alloc(m_TargetTexture);
                }
            }

            public void DisposeImportedTarget()
            {
                if (m_DestRTHandle != null)
                {
                    RTHandles.Release(m_DestRTHandle);
                    m_DestRTHandle = null;
                }
                m_TargetTexture = null;
            }

            private class GlobalBindingPassData
            {
                internal Texture blurTex;
                internal Vector4 texelSize;
            }

            public override void RecordRenderGraph(
                UnityEngine.Rendering.RenderGraphModule.RenderGraph renderGraph,
                global::UnityEngine.Rendering.ContextContainer frameData)
            {
                if (m_TargetTexture == null || m_DestRTHandle == null)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                TextureHandle src = resourceData.activeColorTexture;
                if (!src.IsValid())
                    return;

                TextureHandle dst = renderGraph.ImportTexture(m_DestRTHandle);
                if (!dst.IsValid())
                    return;

                renderGraph.AddBlitPass(
                    src,
                    dst,
                    Vector2.one,
                    Vector2.zero,
                    passName: "UI Blur Capture");

                using (var builder = renderGraph.AddUnsafePass<GlobalBindingPassData>(
                           "UI Blur Set Globals",
                           out var passData))
                {
                    passData.blurTex = m_TargetTexture;
                    passData.texelSize = new Vector4(
                        1.0f / m_TargetTexture.width,
                        1.0f / m_TargetTexture.height,
                        m_TargetTexture.width,
                        m_TargetTexture.height);

                    builder.AllowGlobalStateModification(true);
                    builder.AllowPassCulling(false);
                    builder.UseTexture(dst, AccessFlags.Read);
                    builder.SetRenderFunc(static (GlobalBindingPassData data, UnsafeGraphContext ctx) =>
                    {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                        cmd.SetGlobalTexture(s_UITextureID, data.blurTex);
                        cmd.SetGlobalVector(s_UITextureTexelSizeID, data.texelSize);
                    });
                }
            }
        }
    }
}
