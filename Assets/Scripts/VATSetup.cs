using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

public class VATSetup : MonoBehaviour
{
    [SerializeField] private ComputeShader VATWriter;
    [SerializeField] private SkinnedMeshRenderer skinnedMeshRenderer;
    [SerializeField] private Animator animator;
    [SerializeField] private AnimationClip animationClip;
    [SerializeField] private CustomRenderTexture renderTexture;
    [SerializeField] private Material material;
    private int amountFramesToRecord;
    private Mesh mesh;
    private GraphicsBuffer gpuVertices;
    private GraphicsBuffer[] gpuVerticesArray;
    private Vector3 threadGroupSize;
    private int kernelID;
    private int textureWidth;
    private int frameWidth;
    private int vertexCount => mesh.vertexCount;
    private void Awake()
    {
        mesh = skinnedMeshRenderer.sharedMesh;
        mesh.vertexBufferTarget |= GraphicsBuffer.Target.Structured;

        amountFramesToRecord = Mathf.ClosestPowerOfTwo((int)(animationClip.frameRate * animationClip.length));
        frameWidth = Mathf.CeilToInt(Mathf.Sqrt(vertexCount));
        textureWidth = frameWidth * (int)Mathf.Sqrt(amountFramesToRecord);
        gpuVerticesArray = new GraphicsBuffer[amountFramesToRecord];
        
        renderTexture = new CustomRenderTexture(frameWidth, frameWidth, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        renderTexture.enableRandomWrite = true;
        
        kernelID = 0;
        VATWriter.GetKernelThreadGroupSizes(kernelID, out uint threadGroupSizeX, out uint threadGroupSizeY, out _);
        
        threadGroupSize.x = Mathf.CeilToInt((float)vertexCount / threadGroupSizeX);

        //FillGPUVerticesArray();

        // for (int i = 0; i < amountFramesToRecord; i++)
        // {
        //     WriteToVAT(i);
        // }
        //
        WriteToVAT(0);
        
        material.SetFloat("_amountFrames", (int)Mathf.Sqrt(amountFramesToRecord));
        material.SetFloat("_frameWidth", frameWidth);
        material.SetTexture("_MainTex", renderTexture);

        SaveTexture();
    }
    

    private void WriteToVAT(int currentFrame)
    {
        gpuVertices ??= mesh.GetVertexBuffer(0);

        int frameX = currentFrame % (int)Mathf.Sqrt(amountFramesToRecord);
        int frameY = (int)(currentFrame / Mathf.Sqrt(amountFramesToRecord));
        frameX *= frameWidth;
        frameY *= frameWidth;
        Vector2 startPixel = new Vector2(frameX, frameY);

        //vertex[] vertices = new vertex[vertexCount];
        //gpuVerticesArray[currentFrame].GetData(vertices);
        
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

        Mesh bakedMesh = new Mesh();
        float sampleTime = 0;

        float perFrameTime = animationClip.length / amountFramesToRecord;
        for (int i = 0; i < amountFramesToRecord; i++)
        {
            animator.Play(aniStateInfo.shortNameHash, iLayer, sampleTime);
            animator.Update(0f);

            skinnedMeshRenderer.BakeMesh(bakedMesh);

            GraphicsBuffer tempGpuVertices = new GraphicsBuffer(GraphicsBuffer.Target.Structured, vertexCount,
                bakedMesh.GetVertexBufferStride(0));
            vertex[] vertices = new vertex[vertexCount];
            for (int j = 0; j < vertexCount; j++)
            {
                vertices[i].position = bakedMesh.vertices[i];
            }
            tempGpuVertices.SetData(vertices);
            gpuVerticesArray[i] = tempGpuVertices;

            sampleTime += perFrameTime;
        }
    }
    
    public void SaveTexture () {
        byte[] bytes = toTexture2D(renderTexture).EncodeToPNG();
        System.IO.File.WriteAllBytes(Application.dataPath + "/SavedScreen.png", bytes);
    }
    Texture2D toTexture2D(RenderTexture rTex)
    {
        Texture2D tex = new Texture2D(frameWidth, frameWidth, TextureFormat.RGBAFloat, false);
        RenderTexture.active = rTex;
        tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
        tex.Apply();
        return tex;
    }
}

struct vertex
{
    public float3 position;
    public float3 normal;
    public float4 tangent;
};
