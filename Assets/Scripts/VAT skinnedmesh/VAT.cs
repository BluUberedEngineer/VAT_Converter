using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public class VAT
{
    public SkinnedMeshRenderer skinnedMeshRenderer;
    public Animator animator;
    public AnimationClip animationClip;
    public CustomRenderTexture renderTexture;
    public int amountFramesToRecord;
    public int textureWidth;
    public int frameWidth;
    public int amountFramesSqrt;
    
    private int currentDisplayFrame;
    private ComputeShader VATWriter;
    private Mesh mesh;
    private GraphicsBuffer gpuVertices;
    private GraphicsBuffer[] gpuVerticesArray;
    private Vector3 threadGroupSize;
    private int kernelID;
    private int vertexCount => mesh.vertexCount;

    public VAT(SkinnedMeshRenderer skinnedMeshRenderer, Animator animator, AnimationClip animationClip)
    {
        this.skinnedMeshRenderer = skinnedMeshRenderer;
        this.animator = animator;
        this.animationClip = animationClip;

        VATWriter = Resources.Load<ComputeShader>("VATWriter");
        
        mesh = skinnedMeshRenderer.sharedMesh;
        mesh.vertexBufferTarget |= GraphicsBuffer.Target.Structured;

        amountFramesToRecord = (int)(animationClip.frameRate * animationClip.length);
        amountFramesSqrt = Mathf.CeilToInt(Mathf.Sqrt(amountFramesToRecord));
        frameWidth = Mathf.CeilToInt(Mathf.Sqrt(vertexCount));
        
        textureWidth = frameWidth * amountFramesSqrt;
        gpuVerticesArray = new GraphicsBuffer[amountFramesToRecord];
        
        renderTexture = new CustomRenderTexture(textureWidth, textureWidth, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        renderTexture.enableRandomWrite = true;

        kernelID = 0;
        VATWriter.GetKernelThreadGroupSizes(kernelID, out uint threadGroupSizeX, out _, out _);
        
        threadGroupSize.x = Mathf.CeilToInt((float)vertexCount / threadGroupSizeX);
    }

    public void Destroy()
    {
        gpuVertices?.Release();
        gpuVertices = null;
        
        for (int i = 0; i < gpuVerticesArray.Length; i++)
        {
            gpuVerticesArray[i]?.Release();
            gpuVerticesArray[i] = null;
        }
        
        renderTexture.Release();
        renderTexture = null;
    }

    public CustomRenderTexture WriteToVAT()
    {
        FillGPUVerticesArray();

        for (int i = 0; i < amountFramesToRecord; i++)
        {
            kernelID = 0;
        
            int frameX = i % amountFramesSqrt;
            int frameY = i / amountFramesSqrt;
            frameX *= frameWidth;
            frameY *= frameWidth;
            Vector2 startPixel = new Vector2(frameX, frameY);

            VATWriter.SetTexture(kernelID, "result", renderTexture);
            VATWriter.SetBuffer(kernelID, "gpuVertices", gpuVerticesArray[i]);
            VATWriter.SetVector("startPixel", startPixel);
            VATWriter.SetInt("frameWidth", frameWidth);
            VATWriter.Dispatch(kernelID, (int)threadGroupSize.x, 1, 1);
        }

        return renderTexture;
    }

    //Use VATReader surface shader
    public void ReadFromVAT(Mesh newMesh, int currentFrame)
    {
        Debug.LogWarning("Use the VATReader surface shader");
        if (newMesh.vertexCount != mesh.vertexCount)
        {
            Debug.LogWarning($"{newMesh.name} has {newMesh.vertexCount} vertices and {mesh.name} has {mesh.vertexCount} vertices. They should match unless you know what you are doing");
        }

        kernelID = 1;

        gpuVertices ??= newMesh.GetVertexBuffer(0);
        
        int frameX = currentFrame % amountFramesSqrt;
        int frameY = currentFrame / amountFramesSqrt;
        frameX *= frameWidth;
        frameY *= frameWidth;
        Vector2 startPixel = new Vector2(frameX, frameY);

        VATWriter.SetTexture(kernelID, "result", renderTexture);
        VATWriter.SetBuffer(kernelID, "gpuVertices", gpuVertices);
        VATWriter.SetVector("startPixel", startPixel);
        VATWriter.SetInt("frameWidth", frameWidth);
        VATWriter.Dispatch(kernelID, (int)threadGroupSize.x, 1, 1);
    }

    private void FillGPUVerticesArray()
    {
        int iLayer = 0;
        AnimatorStateInfo aniStateInfo = animator.GetCurrentAnimatorStateInfo(iLayer);

        float sampleTime = 0;
        float perFrameTime = (float)1 / amountFramesToRecord;

        for (int i = 0; i < amountFramesToRecord; i++)
        {
            Mesh bakedMesh = new Mesh();

            animator.Play(aniStateInfo.shortNameHash, iLayer, sampleTime);
            animator.Update(0f);

            skinnedMeshRenderer.BakeMesh(bakedMesh);
            bakedMesh.vertexBufferTarget |= GraphicsBuffer.Target.Structured;

            gpuVerticesArray[i] = bakedMesh.GetVertexBuffer(0);

            sampleTime += perFrameTime;
        }
    }
    
    #region UpdateComponents

    public void UpdateComponents(SkinnedMeshRenderer skinnedMeshRenderer, Animator animator, AnimationClip animationClip)
    {
        this.skinnedMeshRenderer = skinnedMeshRenderer;
        this.animator = animator;
        this.animationClip = animationClip;

        VATWriter = Resources.Load<ComputeShader>("VATWriter");
        
        mesh = skinnedMeshRenderer.sharedMesh;
        mesh.vertexBufferTarget |= GraphicsBuffer.Target.Structured;

        amountFramesToRecord = (int)(animationClip.frameRate * animationClip.length);
        amountFramesSqrt = (int)Mathf.Sqrt(amountFramesToRecord);
        frameWidth = Mathf.CeilToInt(Mathf.Sqrt(vertexCount));
        
        textureWidth = frameWidth * amountFramesSqrt;
        gpuVerticesArray = new GraphicsBuffer[amountFramesToRecord];
        
        renderTexture = new CustomRenderTexture(textureWidth, textureWidth, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        renderTexture.enableRandomWrite = true;

        kernelID = 0;
        VATWriter.GetKernelThreadGroupSizes(kernelID, out uint threadGroupSizeX, out _, out _);
        
        threadGroupSize.x = Mathf.CeilToInt((float)vertexCount / threadGroupSizeX);
    }
    
    public void UpdateComponents(Animator animator, AnimationClip animationClip)
    {
        this.animator = animator;
        this.animationClip = animationClip;

        VATWriter = Resources.Load<ComputeShader>("VATWriter");
        
        mesh = skinnedMeshRenderer.sharedMesh;
        mesh.vertexBufferTarget |= GraphicsBuffer.Target.Structured;

        amountFramesToRecord = (int)(animationClip.frameRate * animationClip.length);
        amountFramesSqrt = (int)Mathf.Sqrt(amountFramesToRecord);
        frameWidth = Mathf.CeilToInt(Mathf.Sqrt(vertexCount));
        
        textureWidth = frameWidth * amountFramesSqrt;
        gpuVerticesArray = new GraphicsBuffer[amountFramesToRecord];
        
        renderTexture = new CustomRenderTexture(textureWidth, textureWidth, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        renderTexture.enableRandomWrite = true;

        kernelID = 0;
        VATWriter.GetKernelThreadGroupSizes(kernelID, out uint threadGroupSizeX, out _, out _);
        
        threadGroupSize.x = Mathf.CeilToInt((float)vertexCount / threadGroupSizeX);
    }
    
    public void UpdateComponents(AnimationClip animationClip)
    {
        this.animator = animator;
        this.animationClip = animationClip;

        VATWriter = Resources.Load<ComputeShader>("VATWriter");
        
        mesh = skinnedMeshRenderer.sharedMesh;
        mesh.vertexBufferTarget |= GraphicsBuffer.Target.Structured;

        amountFramesToRecord = (int)(animationClip.frameRate * animationClip.length);
        amountFramesSqrt = (int)Mathf.Sqrt(amountFramesToRecord);
        frameWidth = Mathf.CeilToInt(Mathf.Sqrt(vertexCount));
        
        textureWidth = frameWidth * amountFramesSqrt;
        gpuVerticesArray = new GraphicsBuffer[amountFramesToRecord];
        
        renderTexture = new CustomRenderTexture(textureWidth, textureWidth, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        renderTexture.enableRandomWrite = true;

        kernelID = 0;
        VATWriter.GetKernelThreadGroupSizes(kernelID, out uint threadGroupSizeX, out _, out _);
        
        threadGroupSize.x = Mathf.CeilToInt((float)vertexCount / threadGroupSizeX);
    }
    
    public void UpdateComponents(Animator animator)
    {
        this.animator = animator;
        this.animationClip = animationClip;

        VATWriter = Resources.Load<ComputeShader>("VATWriter");
        
        mesh = skinnedMeshRenderer.sharedMesh;
        mesh.vertexBufferTarget |= GraphicsBuffer.Target.Structured;

        amountFramesToRecord = (int)(animationClip.frameRate * animationClip.length);
        amountFramesSqrt = (int)Mathf.Sqrt(amountFramesToRecord);
        frameWidth = Mathf.CeilToInt(Mathf.Sqrt(vertexCount));
        
        textureWidth = frameWidth * amountFramesSqrt;
        gpuVerticesArray = new GraphicsBuffer[amountFramesToRecord];
        
        renderTexture = new CustomRenderTexture(textureWidth, textureWidth, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        renderTexture.enableRandomWrite = true;

        kernelID = 0;
        VATWriter.GetKernelThreadGroupSizes(kernelID, out uint threadGroupSizeX, out _, out _);
        
        threadGroupSize.x = Mathf.CeilToInt((float)vertexCount / threadGroupSizeX);
    }
    
    public void UpdateComponents(SkinnedMeshRenderer skinnedMeshRenderer)
    {
        this.skinnedMeshRenderer = skinnedMeshRenderer;
        this.animator = animator;
        this.animationClip = animationClip;

        VATWriter = Resources.Load<ComputeShader>("VATWriter");
        
        mesh = skinnedMeshRenderer.sharedMesh;
        mesh.vertexBufferTarget |= GraphicsBuffer.Target.Structured;

        amountFramesToRecord = (int)(animationClip.frameRate * animationClip.length);
        amountFramesSqrt = (int)Mathf.Sqrt(amountFramesToRecord);
        frameWidth = Mathf.CeilToInt(Mathf.Sqrt(vertexCount));
        
        textureWidth = frameWidth * amountFramesSqrt;
        gpuVerticesArray = new GraphicsBuffer[amountFramesToRecord];
        
        renderTexture = new CustomRenderTexture(textureWidth, textureWidth, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        renderTexture.enableRandomWrite = true;

        kernelID = 0;
        VATWriter.GetKernelThreadGroupSizes(kernelID, out uint threadGroupSizeX, out _, out _);
        
        threadGroupSize.x = Mathf.CeilToInt((float)vertexCount / threadGroupSizeX);
    }
    
    public void UpdateComponents(SkinnedMeshRenderer skinnedMeshRenderer, AnimationClip animationClip)
    {
        this.skinnedMeshRenderer = skinnedMeshRenderer;
        this.animator = animator;
        this.animationClip = animationClip;

        VATWriter = Resources.Load<ComputeShader>("VATWriter");
        
        mesh = skinnedMeshRenderer.sharedMesh;
        mesh.vertexBufferTarget |= GraphicsBuffer.Target.Structured;

        amountFramesToRecord = (int)(animationClip.frameRate * animationClip.length);
        amountFramesSqrt = (int)Mathf.Sqrt(amountFramesToRecord);
        frameWidth = Mathf.CeilToInt(Mathf.Sqrt(vertexCount));
        
        textureWidth = frameWidth * amountFramesSqrt;
        gpuVerticesArray = new GraphicsBuffer[amountFramesToRecord];
        
        renderTexture = new CustomRenderTexture(textureWidth, textureWidth, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        renderTexture.enableRandomWrite = true;

        kernelID = 0;
        VATWriter.GetKernelThreadGroupSizes(kernelID, out uint threadGroupSizeX, out _, out _);
        
        threadGroupSize.x = Mathf.CeilToInt((float)vertexCount / threadGroupSizeX);
    }
    
    public void UpdateComponents(SkinnedMeshRenderer skinnedMeshRenderer, Animator animator)
    {
        this.skinnedMeshRenderer = skinnedMeshRenderer;
        this.animator = animator;
        this.animationClip = animationClip;

        VATWriter = Resources.Load<ComputeShader>("VATWriter");
        
        mesh = skinnedMeshRenderer.sharedMesh;
        mesh.vertexBufferTarget |= GraphicsBuffer.Target.Structured;

        amountFramesToRecord = (int)(animationClip.frameRate * animationClip.length);
        amountFramesSqrt = (int)Mathf.Sqrt(amountFramesToRecord);
        frameWidth = Mathf.CeilToInt(Mathf.Sqrt(vertexCount));
        
        textureWidth = frameWidth * amountFramesSqrt;
        gpuVerticesArray = new GraphicsBuffer[amountFramesToRecord];
        
        renderTexture = new CustomRenderTexture(textureWidth, textureWidth, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        renderTexture.enableRandomWrite = true;

        kernelID = 0;
        VATWriter.GetKernelThreadGroupSizes(kernelID, out uint threadGroupSizeX, out _, out _);
        
        threadGroupSize.x = Mathf.CeilToInt((float)vertexCount / threadGroupSizeX);
    }

    #endregion
}
