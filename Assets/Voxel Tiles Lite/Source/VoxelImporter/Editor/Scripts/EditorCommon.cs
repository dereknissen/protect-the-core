using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

namespace VoxelImporter
{
    public sealed class EditorCommon
    {
        public static void SaveInsideAssetsFolderDisplayDialog()
        {
            EditorUtility.DisplayDialog("Need to save in the Assets folder", "You need to save the file inside of the project's assets floder", "ok");
        }

        public static string GetHelpStrings(List<string> helpList)
        {
            if (helpList.Count > 0)
            {
                string text = "";
                if (helpList.Count >= 3)
                {
                    int i = 0;
                    var enu = helpList.GetEnumerator();
                    while (enu.MoveNext())
                    {
                        if (i == helpList.Count - 1)
                            text += ", and ";
                        else if (i != 0)
                            text += ", ";
                        text += enu.Current;
                        i++;
                    }
                }
                else if (helpList.Count == 2)
                {
                    var enu = helpList.GetEnumerator();
                    enu.MoveNext();
                    text += enu.Current;
                    text += " and ";
                    enu.MoveNext();
                    text += enu.Current;
                }
                else if (helpList.Count == 1)
                {
                    var enu = helpList.GetEnumerator();
                    enu.MoveNext();
                    text += enu.Current;
                }
                return string.Format("If it is not Prefab you need to save the file.\nPlease create a Prefab for this GameObject.\nIf you do not want to Prefab, please save {0}.", text);
            }
            return null;
        }

        public static bool IsMainAsset(UnityEngine.Object obj)
        {
            return (obj != null && AssetDatabase.Contains(obj) && AssetDatabase.IsMainAsset(obj));
        }
        public static bool IsSubAsset(UnityEngine.Object obj)
        {
            return (obj != null && AssetDatabase.Contains(obj) && AssetDatabase.IsSubAsset(obj));
        }
        public static string GetProjectRelativePath2FullPath(string assetPath)
        {
            return Application.dataPath + assetPath.Remove(0, "Assets".Length);
        }
        public static string GenerateUniqueAssetFullPath(string fullPath)
        {
            var assetPath = AssetDatabase.GenerateUniqueAssetPath(FileUtil.GetProjectRelativePath(fullPath));
            return GetProjectRelativePath2FullPath(assetPath);
        }
        public static T Instantiate<T>(T obj) where T : UnityEngine.Object
        {
            if (obj == null)
                return null;
            var inst = UnityEngine.Object.Instantiate(obj) as T;
            var index = inst.name.LastIndexOf("(Clone)");
            if (index >= 0)
                inst.name = inst.name.Remove(index);
            return inst;
        }

        public static bool IsBuiltInRenderPipeline()
        {
            return GraphicsSettings.currentRenderPipeline == null;
        }
        public static bool IsUniversalRenderPipeline()
        {
            if (GraphicsSettings.currentRenderPipeline == null)
                return false;
            var shader = GraphicsSettings.currentRenderPipeline.defaultShader;
            if (shader == Shader.Find("LightweightPipeline/Standard (Physically Based)") ||
                shader == Shader.Find("Lightweight Render Pipeline/Lit") ||
                shader == Shader.Find("Universal Render Pipeline/Lit"))
                return true;
            return false;
        }
        public static bool IsHighDefinitionRenderPipeline()
        {
            if (GraphicsSettings.currentRenderPipeline == null)
                return false;
            var shader = GraphicsSettings.currentRenderPipeline.defaultShader;
            if (shader == Shader.Find("HDRenderPipeline/Lit") ||
                shader == Shader.Find("HDRP/Lit"))
                return true;
            return false;
        }
        public static Shader GetStandardShader()
        {
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                var shader = GraphicsSettings.currentRenderPipeline.defaultShader;
                if (shader != null)
                    return shader;
            }
            return Shader.Find("Standard");
        }
        public static Material CreateStandardMaterial()
        {
            var material = new Material(GetStandardShader());
            material.color = Color.white;
            if (material.HasProperty("_BaseColor"))  //LWRP
                material.SetColor("_BaseColor", Color.white);
            return material;
        }
        public static Material ResetMaterial(Material mat)
        {
            if (IsMainAsset(mat))
            {
                return Instantiate(mat);
            }
            else
            {
                return mat;
            }
        }
        public static void SetMainTexture(Material material, Texture2D texture)
        {
            material.mainTexture = texture;
            if (material.HasProperty("_BaseColorMap"))  //HDRP
                material.SetTexture("_BaseColorMap", texture);
            else if (material.HasProperty("_BaseMap"))  //LWRP
                material.SetTexture("_BaseMap", texture);
        }

        public static bool IsComponentEditable(Component comp)
        {
            var prefabType = PrefabUtility.GetPrefabAssetType(comp);
            if (prefabType == PrefabAssetType.NotAPrefab || prefabType == PrefabAssetType.MissingAsset)
                return true;
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null)
                return false;
            var selectPrefab = PrefabUtility.GetCorrespondingObjectFromSource(comp);
            if (selectPrefab != prefabStage.prefabContentsRoot)
                return false;
            return true;
        }

        public static bool IsComponentPrefab(Component comp)
        {
            var prefabType = PrefabUtility.GetPrefabAssetType(comp);
            if (prefabType == PrefabAssetType.NotAPrefab || prefabType == PrefabAssetType.MissingAsset)
                return false;
            var prefabStatus = PrefabUtility.GetPrefabInstanceStatus(comp);
            if (prefabStatus != PrefabInstanceStatus.NotAPrefab)
                return false;
            return true;
        }
        public static void DestroyImmediateExtra(VoxelBase voxelBase)
        {
            VoxelBaseExplosionCore.DestroyImmediateVoxelExplosion(voxelBase);
        }
    }
}
