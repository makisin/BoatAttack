﻿using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Collections;

public class MainRenderPass : ScriptableRenderPass
{
    FilteringSettings m_OpaqueFilteringSettings;
    FilteringSettings m_TransparentFilteringSettings;
    ShaderTagId m_UniversalForwardPass = new ShaderTagId("UniversalForward");

    AttachmentDescriptor colorAttachmentDescriptor;
    AttachmentDescriptor depthAttachmentDescriptor;

    Material m_CausticsMaterial;
    public BoatRenderer renderer;

    public MainRenderPass(RenderPassEvent renderPassEvent, Material causticsMaterial)
    {
        this.renderPassEvent = renderPassEvent;
        m_OpaqueFilteringSettings = new FilteringSettings(RenderQueueRange.opaque);
        m_TransparentFilteringSettings = new FilteringSettings(RenderQueueRange.transparent);
        colorAttachmentDescriptor = new AttachmentDescriptor(RenderTextureFormat.RGB111110Float);
        depthAttachmentDescriptor = new AttachmentDescriptor(RenderTextureFormat.Depth);
        m_CausticsMaterial = causticsMaterial;
    }

    //public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    //{
    //    ConfigureTarget(BuiltinRenderTextureType.CurrentActive, BuiltinRenderTextureType.CurrentActive);
    //}

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var opaqueDrawingSettings = CreateDrawingSettings(m_UniversalForwardPass, ref renderingData, SortingCriteria.CommonOpaque);
        var transparentDrawingSettings = CreateDrawingSettings(m_UniversalForwardPass, ref renderingData, SortingCriteria.CommonTransparent);

        var cameraTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
        int width = cameraTargetDescriptor.width;
        int height = cameraTargetDescriptor.height;
        colorAttachmentDescriptor.ConfigureTarget(renderer.m_CameraColorAttachment.Identifier(), false, true);
        colorAttachmentDescriptor.ConfigureClear(renderingData.cameraData.camera.backgroundColor);
        var descriptors = new NativeArray<AttachmentDescriptor>(
                    new[] { colorAttachmentDescriptor, depthAttachmentDescriptor },
                    Allocator.Temp);

        using (context.BeginScopedRenderPass(width, height, 1, descriptors, 1))
        {
            descriptors.Dispose();
            NativeArray<int> attachmentIndices = new NativeArray<int>(new[] { 0 }, Allocator.Temp);
            
            // Opaques + Skybox Subpass
            using (context.BeginScopedSubPass(attachmentIndices))
            {
                attachmentIndices.Dispose();
                context.DrawRenderers(renderingData.cullResults, ref opaqueDrawingSettings, ref m_OpaqueFilteringSettings);
                context.DrawSkybox(renderingData.cameraData.camera);
            }

            // Caustics Subpass
            attachmentIndices = new NativeArray<int>(new[]{0}, Allocator.Temp);
            var inputIndices = new NativeArray<int>(new[]{1}, Allocator.Temp);
            using (context.BeginScopedSubPass(attachmentIndices, inputIndices, false))
            {
                attachmentIndices.Dispose();
                inputIndices.Dispose();
                var cmd = CommandBufferPool.Get("DrawCaustics");
                cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, m_CausticsMaterial);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

        }
    }
}
