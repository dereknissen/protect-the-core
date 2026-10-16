using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace VoxelImporter
{
    public abstract class VoxelBaseExplosionCore
    {
        public VoxelBaseExplosion explosionBase { get; protected set; }

        public VoxelBase voxelBase { get; protected set; }
        public VoxelBaseCore voxelBaseCore { get; protected set; }

        public VoxelBaseExplosionCore(VoxelBaseExplosion target)
        {
            explosionBase = target;
            voxelBase = target.GetComponent<VoxelBase>();
        }

        public virtual void Initialize()
        {
            if (explosionBase == null) return;

            explosionBase.EditorInitialize();
            explosionBase.EditorInitializeDone();
        }

        public void Generate()
        {
            if (explosionBase == null || voxelBaseCore.voxelData == null) return;

            voxelBaseCore.DestroyUnusedObjectInPrefabObject();

            GenerateOnly();

            SetMaterialProperties();

            voxelBaseCore.CheckPrefabAssetReImport();

            explosionBase.edit_fileRefreshLastTimeTicks = voxelBase.fileRefreshLastTimeTicks;
            explosionBase.edit_colorSpace = PlayerSettings.colorSpace;
        }
        public abstract void GenerateOnly();

        protected void CreateBasicCube(out Vector3 cubeCenter, out List<Vector3> cubeVertices, out List<Vector3> cubeNormals, out List<int> cubeTriangles)
        {
            cubeVertices = new List<Vector3>();
            cubeNormals = new List<Vector3>();
            cubeTriangles = new List<int>();
            {
                var offsetPosition = voxelBase.localOffset + voxelBase.importOffset;
                cubeCenter = Vector3.Scale(voxelBase.importScale, offsetPosition) + voxelBase.importScale / 2f;
                #region forward
                {
                    var pOffset = Vector3.Scale(voxelBase.importScale, offsetPosition);
                    var vOffset = cubeVertices.Count;
                    cubeVertices.Add(new Vector3(0, voxelBase.importScale.y, voxelBase.importScale.z) + pOffset);
                    cubeVertices.Add(new Vector3(0, 0, voxelBase.importScale.z) + pOffset);
                    cubeVertices.Add(new Vector3(voxelBase.importScale.x, 0, voxelBase.importScale.z) + pOffset);
                    cubeVertices.Add(new Vector3(voxelBase.importScale.x, voxelBase.importScale.y, voxelBase.importScale.z) + pOffset);
                    cubeTriangles.Add(vOffset + 0); cubeTriangles.Add(vOffset + 1); cubeTriangles.Add(vOffset + 2);
                    cubeTriangles.Add(vOffset + 0); cubeTriangles.Add(vOffset + 2); cubeTriangles.Add(vOffset + 3);
                    for (int j = 0; j < 4; j++)
                    {
                        cubeNormals.Add(Vector3.forward);
                    }
                }
                #endregion
                #region up
                {
                    var pOffset = Vector3.Scale(voxelBase.importScale, offsetPosition);
                    var vOffset = cubeVertices.Count;
                    cubeVertices.Add(new Vector3(0, voxelBase.importScale.y, 0) + pOffset);
                    cubeVertices.Add(new Vector3(0, voxelBase.importScale.y, voxelBase.importScale.z) + pOffset);
                    cubeVertices.Add(new Vector3(voxelBase.importScale.x, voxelBase.importScale.y, voxelBase.importScale.z) + pOffset);
                    cubeVertices.Add(new Vector3(voxelBase.importScale.x, voxelBase.importScale.y, 0) + pOffset);
                    cubeTriangles.Add(vOffset + 0); cubeTriangles.Add(vOffset + 1); cubeTriangles.Add(vOffset + 2);
                    cubeTriangles.Add(vOffset + 0); cubeTriangles.Add(vOffset + 2); cubeTriangles.Add(vOffset + 3);
                    for (int j = 0; j < 4; j++)
                    {
                        cubeNormals.Add(Vector3.up);
                    }
                }
                #endregion
                #region right
                {
                    var pOffset = Vector3.Scale(voxelBase.importScale, offsetPosition);
                    var vOffset = cubeVertices.Count;
                    cubeVertices.Add(new Vector3(voxelBase.importScale.x, 0, 0) + pOffset);
                    cubeVertices.Add(new Vector3(voxelBase.importScale.x, voxelBase.importScale.y, 0) + pOffset);
                    cubeVertices.Add(new Vector3(voxelBase.importScale.x, voxelBase.importScale.y, voxelBase.importScale.z) + pOffset);
                    cubeVertices.Add(new Vector3(voxelBase.importScale.x, 0, voxelBase.importScale.z) + pOffset);
                    cubeTriangles.Add(vOffset + 0); cubeTriangles.Add(vOffset + 1); cubeTriangles.Add(vOffset + 2);
                    cubeTriangles.Add(vOffset + 0); cubeTriangles.Add(vOffset + 2); cubeTriangles.Add(vOffset + 3);
                    for (int j = 0; j < 4; j++)
                    {
                        cubeNormals.Add(Vector3.right);
                    }
                }
                #endregion
                #region left
                {
                    var pOffset = Vector3.Scale(voxelBase.importScale, offsetPosition);
                    var vOffset = cubeVertices.Count;
                    cubeVertices.Add(new Vector3(0, 0, voxelBase.importScale.z) + pOffset);
                    cubeVertices.Add(new Vector3(0, 0, 0) + pOffset);
                    cubeVertices.Add(new Vector3(0, voxelBase.importScale.y, 0) + pOffset);
                    cubeVertices.Add(new Vector3(0, voxelBase.importScale.y, voxelBase.importScale.z) + pOffset);
                    cubeTriangles.Add(vOffset + 2); cubeTriangles.Add(vOffset + 1); cubeTriangles.Add(vOffset + 0);
                    cubeTriangles.Add(vOffset + 3); cubeTriangles.Add(vOffset + 2); cubeTriangles.Add(vOffset + 0);
                    for (int j = 0; j < 4; j++)
                    {
                        cubeNormals.Add(Vector3.left);
                    }
                }
                #endregion
                #region down
                {
                    var pOffset = Vector3.Scale(voxelBase.importScale, offsetPosition);
                    var vOffset = cubeVertices.Count;
                    cubeVertices.Add(new Vector3(voxelBase.importScale.x, 0, 0) + pOffset);
                    cubeVertices.Add(new Vector3(0, 0, 0) + pOffset);
                    cubeVertices.Add(new Vector3(0, 0, voxelBase.importScale.z) + pOffset);
                    cubeVertices.Add(new Vector3(voxelBase.importScale.x, 0, voxelBase.importScale.z) + pOffset);
                    cubeTriangles.Add(vOffset + 2); cubeTriangles.Add(vOffset + 1); cubeTriangles.Add(vOffset + 0);
                    cubeTriangles.Add(vOffset + 3); cubeTriangles.Add(vOffset + 2); cubeTriangles.Add(vOffset + 0);
                    for (int j = 0; j < 4; j++)
                    {
                        cubeNormals.Add(Vector3.down);
                    }
                }
                #endregion
                #region back
                {
                    var pOffset = Vector3.Scale(voxelBase.importScale, offsetPosition);
                    var vOffset = cubeVertices.Count;
                    cubeVertices.Add(new Vector3(0, 0, 0) + pOffset);
                    cubeVertices.Add(new Vector3(voxelBase.importScale.x, 0, 0) + pOffset);
                    cubeVertices.Add(new Vector3(voxelBase.importScale.x, voxelBase.importScale.y, 0) + pOffset);
                    cubeVertices.Add(new Vector3(0, voxelBase.importScale.y, 0) + pOffset);
                    cubeTriangles.Add(vOffset + 2); cubeTriangles.Add(vOffset + 1); cubeTriangles.Add(vOffset + 0);
                    cubeTriangles.Add(vOffset + 3); cubeTriangles.Add(vOffset + 2); cubeTriangles.Add(vOffset + 0);
                    for (int j = 0; j < 4; j++)
                    {
                        cubeNormals.Add(Vector3.back);
                    }
                }
                #endregion
            }
        }

        protected Shader GetStandardShader(bool transparent)
        {
            Shader shader = null;
            if (EditorCommon.IsBuiltInRenderPipeline() &&
                explosionBase.edit_legacyShader)
            {
                if (!transparent)
                    shader = Shader.Find("Voxel Importer/Explosion/VoxelExplosion-Opaque");
                else
                    shader = Shader.Find("Voxel Importer/Explosion/VoxelExplosion-Transparent");
            }
            else
            {
                if (!transparent)
                    shader = Shader.Find("Shader Graphs/VoxelExplosion-Opaque");
                else
                    shader = Shader.Find("Shader Graphs/VoxelExplosion-Transparent");
            }
            return shader;
        }

        public abstract void SetExplosionCenter();

        protected void CopyMaterialProperties(List<Material> dst, List<Material> src)
        {
            if (dst == null || src == null)
                return;
            for (int i = 0; i < src.Count; i++)
            {
                if (dst[i] != null && src[i] != null)
                {
                    if (src[i].HasProperty("_Color"))
                        dst[i].color = src[i].color;
                    {
                        var smoothness = 0f;
                        if (src[i].HasProperty("_Glossiness"))
                            smoothness = src[i].GetFloat("_Glossiness");
                        if (src[i].HasProperty("_Smoothness"))
                            smoothness = src[i].GetFloat("_Smoothness");
                        if (dst[i].HasProperty("_Glossiness"))
                            dst[i].SetFloat("_Glossiness", smoothness);
                        if (dst[i].HasProperty("_Smoothness"))
                            dst[i].SetFloat("_Smoothness", smoothness);
                    }
                    if (src[i].HasProperty("_Metallic"))
                        dst[i].SetFloat("_Metallic", src[i].GetFloat("_Metallic"));
                    bool isEnableEmission = false;
                    if (EditorCommon.IsHighDefinitionRenderPipeline())
                    {
                        if (src[i].HasProperty("_UseEmissiveIntensity"))
                            isEnableEmission = src[i].GetInt("_UseEmissiveIntensity") != 0;
                    }
                    else
                    {
                        isEnableEmission = src[i].IsKeywordEnabled("_EMISSION");
                    }
                    if (isEnableEmission)
                    {
                        if (src[i].HasProperty("_EmissionColor"))
                        {
                            var emissionColor = src[i].GetColor("_EmissionColor");
                            if (EditorCommon.IsHighDefinitionRenderPipeline())
                            {
                                emissionColor = src[i].GetColor("_EmissiveColorLDR");
                                if (dst[i].HasProperty("_EmissiveIntensity") && src[i].HasProperty("_EmissiveIntensity"))
                                    dst[i].SetFloat("_EmissiveIntensity", src[i].GetFloat("_EmissiveIntensity"));
                                if (dst[i].HasProperty("_EmissiveExposureWeight") && src[i].HasProperty("_EmissiveExposureWeight"))
                                    dst[i].SetFloat("_EmissiveExposureWeight", src[i].GetFloat("_EmissiveExposureWeight"));
                            }
                            if (EditorCommon.IsBuiltInRenderPipeline())
                            {
                                if (dst[i].shader.name.StartsWith("Voxel Importer/Explosion/VoxelExplosion-"))
                                {
                                    if (PlayerSettings.colorSpace == ColorSpace.Linear)
                                    {
                                        emissionColor = emissionColor.linear;
                                    }
                                }
                                else if (dst[i].shader.name.StartsWith("Shader Graphs/VoxelExplosion-"))
                                {
                                    if (PlayerSettings.colorSpace == ColorSpace.Gamma)
                                    {
                                        emissionColor = emissionColor.linear;
                                    }
                                    else if (PlayerSettings.colorSpace == ColorSpace.Linear)
                                    {
                                        emissionColor = emissionColor.linear;
                                    }
                                }
                            }
                            else
                            {
                                if (dst[i].shader.name.StartsWith("Shader Graphs/VoxelExplosion-"))
                                {
                                    if (PlayerSettings.colorSpace == ColorSpace.Gamma)
                                    {
                                        emissionColor = emissionColor.linear;
                                    }
                                }
                            }
                            dst[i].SetColor("_ExplosionEmissionColor", emissionColor);
                        }
                    }
                    else
                    {
                        dst[i].SetColor("_ExplosionEmissionColor", Color.black);
                        if (dst[i].HasProperty("_EmissiveIntensity"))
                            dst[i].SetFloat("_EmissiveIntensity", 0f);
                        if (dst[i].HasProperty("_EmissiveExposureWeight"))
                            dst[i].SetFloat("_EmissiveExposureWeight", 1f);
                    }
                }
            }
        }
        public abstract void CopyMaterialProperties();
        public void SetMaterialProperties()
        {
            CopyMaterialProperties();
            SetExplosionCenter();
            explosionBase.SetExplosionRotate(explosionBase.explosionRotate);
            explosionBase.SetExplosionRate(explosionBase.edit_explosionRate);
        }

        public abstract void ResetAllAssets();

        #region StaticForceGenerate
        public static void StaticForceGenerate(VoxelBaseExplosion voxelBase)
        {
            VoxelBaseExplosionCore voxelCore = null;
            if (voxelBase is VoxelObjectExplosion)
            {
                voxelCore = new VoxelObjectExplosionCore(voxelBase);
            }
            else if (voxelBase is VoxelChunksObjectExplosion)
            {
                voxelCore = new VoxelChunksObjectExplosionCore(voxelBase);
            }
            else if (voxelBase is VoxelFrameAnimationObjectExplosion)
            {
                voxelCore = new VoxelFrameAnimationObjectExplosionCore(voxelBase);
            }
            else if (voxelBase is VoxelSkinnedAnimationObjectExplosion)
            {
                voxelCore = new VoxelSkinnedAnimationObjectExplosionCore(voxelBase);
            }
            else
            {
                Assert.IsTrue(false);
            }
            if (voxelCore == null) return;

            voxelCore.ResetAllAssets();
            voxelCore.Generate();

            #region Extra
            if (voxelCore is VoxelSkinnedAnimationObjectExplosionCore)
            {
                var voxelCoreEx = voxelCore as VoxelSkinnedAnimationObjectExplosionCore;
                if (voxelCoreEx.explosionObject.meshes != null &&
                    voxelCoreEx.explosionObject.meshes.Count > 0 &&
                    voxelCoreEx.explosionObject.meshes[0].bakeMesh != null)
                {
                    voxelCoreEx.Bake();
                }
            }
            #endregion
        }

        public static void DestroyImmediateVoxelExplosion(VoxelBase voxelBase)
        {
            {
                var comps = voxelBase.GetComponentsInChildren<VoxelChunksObjectChunkExplosion>(true);
                foreach (var comp in comps)
                    Undo.DestroyObjectImmediate(comp);
            }
            {
                var comps = voxelBase.GetComponentsInChildren<VoxelBaseExplosion>(true);
                foreach (var comp in comps)
                    Undo.DestroyObjectImmediate(comp);
            }
        }
        #endregion
    }
}
