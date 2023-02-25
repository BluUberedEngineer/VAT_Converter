using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VATSetup : MonoBehaviour
{
    [SerializeField] private SkinnedMeshRenderer skinnedMeshRenderer;
    [SerializeField] private Animator animator;
    [SerializeField] private AnimationClip animationClip;
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private float animationSpeed;
    [SerializeField] private Material displayMat;
    public CustomRenderTexture rt;
    private Mesh mesh;
    private VAT vat;

    #region ShaderProperties
    private readonly static int Vat = Shader.PropertyToID("_VAT");
    private readonly static int FrameWidth = Shader.PropertyToID("_FrameWidth");
    private readonly static int AmountFramesSqrt = Shader.PropertyToID("_AmountFramesSqrt");
    private readonly static int TextureWidth = Shader.PropertyToID("_TextureWidth");
    private readonly static int CurrentFrame = Shader.PropertyToID("_CurrentFrame");
    #endregion
    
    private void Awake()
    {
        vat = new VAT(skinnedMeshRenderer, animator, animationClip);
        rt = vat.WriteToVAT();
        SaveTexture(rt, vat.textureWidth, vat.textureWidth);

        mesh = MeshExtensions.CopyMesh(skinnedMeshRenderer.sharedMesh);
        meshFilter.sharedMesh = mesh;
        
        displayMat.SetTexture(Vat, rt);
        displayMat.SetInt(FrameWidth, vat.frameWidth);
        displayMat.SetInt(AmountFramesSqrt, vat.amountFramesSqrt);
        displayMat.SetInt(TextureWidth, vat.textureWidth);
    }

    private void OnDisable()
    {
        vat.Destroy();
    }

    private void Update()
    {
        displayMat.SetInt(CurrentFrame, (int)(Time.frameCount / animationSpeed) % vat.amountFramesToRecord);
    }

    public void SaveTexture (RenderTexture rTex, int imageWidth, int imageHeight) {
        byte[] bytes = toTexture2D(rTex, imageWidth, imageHeight).EncodeToPNG();
        System.IO.File.WriteAllBytes(Application.dataPath + "/SavedScreen.png", bytes);
    }
    Texture2D toTexture2D(RenderTexture rTex, int imageWidth, int imageHeight)
    {
        Texture2D tex = new Texture2D(imageWidth, imageHeight, TextureFormat.RGBAFloat, false);
        RenderTexture.active = rTex;
        tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
        tex.Apply();
        return tex;
    }
}
