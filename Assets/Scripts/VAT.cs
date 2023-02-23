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
    public int currentDisplayFrame;
    
    private ComputeShader VATWriter;
    private int amountFramesToRecord;
    private int amountFramesWidth;
    private Mesh mesh;
    private GraphicsBuffer gpuVertices;
    private GraphicsBuffer[] gpuVerticesArray;
    private Vector3 threadGroupSize;
    private int kernelID;
    private int textureWidth;
    private int frameWidth;
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
        amountFramesWidth = (int)Mathf.Sqrt(amountFramesToRecord);
        frameWidth = Mathf.CeilToInt(Mathf.Sqrt(vertexCount));
        
        textureWidth = frameWidth * amountFramesWidth;
        gpuVerticesArray = new GraphicsBuffer[amountFramesToRecord];
        
        renderTexture = new CustomRenderTexture(textureWidth, textureWidth, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        renderTexture.enableRandomWrite = true;

        kernelID = 0;
        VATWriter.GetKernelThreadGroupSizes(kernelID, out uint threadGroupSizeX, out _, out _);
        
        threadGroupSize.x = Mathf.CeilToInt((float)vertexCount / threadGroupSizeX);

        VertexAttributeDescriptor uv2 = new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 2, 3);
        MeshExtensions.AddVertexAttribute(mesh, uv2);
    }

    ~VAT()
    {
        Debug.Log($"destroyed");
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
        
            int frameX = i % amountFramesWidth;
            int frameY = i / amountFramesWidth;
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

    public void ReadFromVAT(int currentFrame)
    {
        kernelID = 1;

        gpuVertices ??= mesh.GetVertexBuffer(0);
        
        int frameX = currentFrame % amountFramesWidth;
        int frameY = currentFrame / amountFramesWidth;
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
    
    // public void SaveTexture () {
    //     byte[] bytes = toTexture2D(renderTexture).EncodeToPNG();
    //     System.IO.File.WriteAllBytes(Application.dataPath + "/SavedScreen.png", bytes);
    // }
    // Texture2D toTexture2D(RenderTexture rTex)
    // {
    //     Texture2D tex = new Texture2D(textureWidth, textureWidth, TextureFormat.RGBAFloat, false);
    //     RenderTexture.active = rTex;
    //     tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
    //     tex.Apply();
    //     return tex;
    // }
}

struct vertex
{
    public float3 position;
    public float3 normal;
    public float4 tangent;
};
