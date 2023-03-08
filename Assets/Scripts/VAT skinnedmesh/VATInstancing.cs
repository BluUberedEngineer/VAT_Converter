using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class VATInstancing : MonoBehaviour
{
    public int range;
    public Mesh mesh;
    public Material material;
  
    private int population => range * range;
    private Matrix4x4[] matrices;
    private MaterialPropertyBlock block;

    
    private void Start() {
        Setup();
    }

    private void Setup() {
        matrices = new Matrix4x4[population];
        float[] instanceIDArray = new float[population];

        block = new MaterialPropertyBlock();

        for (int i = 0; i < population; i++) {
            Vector3 position = new Vector3(i % range, 0, i / range);
            Quaternion rotation = Quaternion.identity;
            Vector3 scale = Vector3.one;

            Matrix4x4 mat = Matrix4x4.TRS(position, rotation, scale);

            matrices[i] = mat;
            instanceIDArray[i] = i;
        }

        block.SetFloatArray("_InstanceID", instanceIDArray);
    }

    private void Update() {
        // Draw a bunch of meshes each frame.
        Graphics.DrawMeshInstanced(mesh, 0, material, matrices, population, block);
    }
}
