using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PixelizePass : ScriptableRenderPass
{

    // The profiler tag that will show up in the frame debugger.
    const string ProfilerTag = "Pixel Pass";

    // We will store our pass settings in this variable.
    private PixelizeFeature.CustomPassSettings settings;

    private RenderTargetIdentifier colorBuffer,pointBuffer, pixelBuffer;
    private int pointBufferID = Shader.PropertyToID("_PointBuffer");
    private int pixelBufferID = Shader.PropertyToID("_PixelBuffer");
    
    private Material material;
    private int pixelScreenHeight, pixelScreenWidth;

    public PixelizePass(PixelizeFeature.CustomPassSettings settings)
    {
        this.settings = settings;
        this.renderPassEvent = settings.renderPassEvent;
        if (material == null) material = CoreUtils.CreateEngineMaterial("Hidden/Pixelize");
    }

    // This method is called before executing the render pass.
    // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
    // When empty this render pass will render to the active camera render target.
    // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
    // The render pipeline will ensure target setup and clearing happens in a performant manner.
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
        // Grab the color buffer from the renderer camera color target.
        colorBuffer = renderingData.cameraData.renderer.cameraColorTarget;
        
        cmd.GetTemporaryRT(pointBufferID, descriptor.width, descriptor.height, 0, FilterMode.Point);
        pointBuffer = new RenderTargetIdentifier(pointBufferID);

        pixelScreenHeight = settings.screenHeight;
        pixelScreenWidth = (int)(pixelScreenHeight * renderingData.cameraData.camera.aspect);
        material.SetVector("_BlockCount", new Vector2(pixelScreenWidth, pixelScreenHeight));
        material.SetVector("_BlockSize", new Vector2(1.0f / pixelScreenWidth, 1.0f / pixelScreenHeight));
        material.SetVector("_HalfBlockSize", new Vector2(0.5f / pixelScreenWidth, 0.5f / pixelScreenHeight));
        descriptor.height = pixelScreenHeight;
        descriptor.width = pixelScreenWidth;


        //cmd.GetTemporaryRT(temporaryBufferID, pixelScreenHeight, pixelScreenWidth, 0, FilterMode.Point);
        cmd.GetTemporaryRT(pixelBufferID, descriptor, FilterMode.Point);
        pixelBuffer = new RenderTargetIdentifier(pixelBufferID);
        

    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get();
        using (new ProfilingScope(cmd, new ProfilingSampler(ProfilerTag)))
        {
            // Blit from the color buffer to a temporary buffer and back. This is needed for a two-pass shader.
            // Bad antialiasing ??!?!?!?!
            //Blit(cmd, colorBuffer, pixelBuffer, material);
            //Blit(cmd, pixelBuffer, colorBuffer);

            // Remove antialiasing...I'm sure there's a clever-er way to do this...
            Blit(cmd, colorBuffer, pointBuffer);
            Blit(cmd, pointBuffer, pixelBuffer); // shader pass 0
            Blit(cmd, pixelBuffer, colorBuffer);

        }
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    // Cleanup any allocated resources that were created during the execution of this render pass.
    // Called when the camera has finished rendering.
    // Here we release/cleanup any allocated resources that were created by this pass.
    // Gets called for all cameras i na camera stack.
    public override void OnCameraCleanup(CommandBuffer cmd)
    {
        if (cmd == null) throw new System.ArgumentNullException("cmd");

        // Since we created a temporary render texture in OnCameraSetup, we need to release the memory here to avoid a leak.
        cmd.ReleaseTemporaryRT(pixelBufferID);
        cmd.ReleaseTemporaryRT(pointBufferID);
    }
}