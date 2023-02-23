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

    public GameObject meshObject;
    
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
    
    private void Awake()
    {
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

        FillGPUVerticesArray();

        for (int i = 0; i < amountFramesToRecord; i++)
        {
            WriteToVAT(i);
        }
        
        material.SetFloat("_amountFrames", amountFramesToRecord);
        material.SetFloat("_frameWidth", frameWidth);
        material.SetTexture("_MainTex", renderTexture);

        SaveTexture();
    }

    private void OnDisable()
    {
        gpuVertices?.Dispose();
        gpuVertices = null;
        
        for (int i = 0; i < gpuVerticesArray.Length; i++)
        {
            gpuVerticesArray[i]?.Release();
            gpuVerticesArray[i] = null;
        }
    }


    private void WriteToVAT(int currentFrame)
    {
        gpuVertices ??= mesh.GetVertexBuffer(0);
        
        int frameX = currentFrame % amountFramesWidth;
        int frameY = currentFrame / amountFramesWidth;
        frameX *= frameWidth;
        frameY *= frameWidth;
        Vector2 startPixel = new Vector2(frameX, frameY);
        
        // vertex[] vertices = new vertex[vertexCount];
        // gpuVerticesArray[currentFrame].GetData(vertices);
        
        VATWriter.SetTexture(kernelID, "result", renderTexture);
        VATWriter.SetBuffer(kernelID, "gpuVertices", gpuVerticesArray[currentFrame]);
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

            //GameObject tempObject = Instantiate(meshObject, new Vector3(i, 0, 0), Quaternion.identity);
            
            // bakedMesh.name = i.ToString();
            // tempObject.GetComponent<MeshFilter>().mesh = bakedMesh;
            //
            // vertex[] vertices = new vertex[vertexCount];
            // bakedMesh.GetVertexBuffer(0).GetData(vertices);

            gpuVerticesArray[i] = bakedMesh.GetVertexBuffer(0);

            sampleTime += perFrameTime;
        }
    }
    
    public void SaveTexture () {
        byte[] bytes = toTexture2D(renderTexture).EncodeToPNG();
        System.IO.File.WriteAllBytes(Application.dataPath + "/SavedScreen.png", bytes);
    }
    Texture2D toTexture2D(RenderTexture rTex)
    {
        Texture2D tex = new Texture2D(textureWidth, textureWidth, TextureFormat.RGBAFloat, false);
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
