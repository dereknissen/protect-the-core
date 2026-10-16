using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace VoxelImporter
{
    public abstract class VoxelBaseExplosionEditor : Editor
    {
        public VoxelBaseExplosion explosionBase { get; protected set; }
        public VoxelBaseExplosionCore explosionCore { get; protected set; }

        #region Textures
        private Texture2D playIcon;
        #endregion

        #region GUIStyle
        protected GUIStyle guiStyleSkinBox;
        protected GUIStyle guiStyleMagentaBold;
        protected GUIStyle guiStyleRedBold;
        protected GUIStyle guiStyleFoldoutBold;
        protected GUIStyle guiStylePlayButton;
        #endregion

        #region Prefab
        protected PrefabAssetType prefabType { get { return PrefabUtility.GetPrefabAssetType(explosionBase.gameObject); } }
        protected bool prefabEnable { get { return (prefabType == PrefabAssetType.Regular || prefabType == PrefabAssetType.Variant) || isPrefabEditMode; } }
        protected bool isPrefab { get { return false; } }
        protected bool isPrefabEditMode { get { return PrefabStageUtility.GetCurrentPrefabStage() != null && PrefabStageUtility.GetCurrentPrefabStage().prefabContentsRoot != null; } }
        protected bool isPrefabEditable { get { return EditorCommon.IsComponentEditable(explosionBase); } }
        #endregion

        protected virtual void OnEnable()
        {
            explosionBase = target as VoxelBaseExplosion;
            if (explosionBase == null) return;

            #region Textures
            playIcon = EditorGUIUtility.Load("icons/animation.play.png") as Texture2D;
            #endregion

            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            SceneView.duringSceneGui -= OnSceneCustomGUI;
            SceneView.duringSceneGui += OnSceneCustomGUI;
        }
        protected virtual void OnDisable()
        {
            if (explosionBase == null) return;

            explosionBase.edit_explosionDraw = false;

            SceneView.duringSceneGui -= OnSceneCustomGUI;
            EditorApplication.update -= Update;
        }

        protected virtual void OnEnableInitializeSet()
        {
            #region Auto Generate
            if (explosionBase.edit_autoGenerate && explosionCore.voxelBaseCore.IsVoxelFileExists())
            {
                if (explosionBase.edit_fileRefreshLastTimeTicks != explosionCore.voxelBase.fileRefreshLastTimeTicks)
                {
                    explosionCore.Generate();
                }
            }
            #endregion
        }

        protected virtual void GUIStyleReady()
        {
            if (guiStyleSkinBox == null)
            {
                guiStyleSkinBox = new GUIStyle(GUI.skin.box);
                var olBox = new GUIStyle("OL box");
                guiStyleSkinBox.normal = olBox.normal;
                guiStyleSkinBox.hover = olBox.hover;
                guiStyleSkinBox.focused = olBox.focused;
                guiStyleSkinBox.active = olBox.active;
            }
            if (guiStyleMagentaBold == null)
                guiStyleMagentaBold = new GUIStyle(EditorStyles.boldLabel);
            guiStyleMagentaBold.normal.textColor = Color.magenta;
            if (guiStyleRedBold == null)
                guiStyleRedBold = new GUIStyle(EditorStyles.boldLabel);
            guiStyleRedBold.normal.textColor = Color.red;
            if (guiStyleFoldoutBold == null)
                guiStyleFoldoutBold = new GUIStyle(EditorStyles.foldout);
            if (guiStyleFoldoutBold == null)
                guiStyleFoldoutBold = new GUIStyle(EditorStyles.foldout);
            guiStyleFoldoutBold.fontStyle = FontStyle.Bold;
            if (guiStylePlayButton == null)
                guiStylePlayButton = new GUIStyle(GUI.skin.button);
            guiStylePlayButton.padding = new RectOffset();
        }

        public override void OnInspectorGUI()
        {
            if (explosionBase == null || explosionCore == null)
            {
                DrawDefaultInspector();
                return;
            }

            {
                if (!isPrefabEditable)
                {
                    EditorGUILayout.HelpBox("Prefab can only be edited in Prefab mode.", MessageType.Info);
                    EditorGUI.BeginDisabledGroup(true);
                }
            }

            GUIStyleReady();

            serializedObject.Update();

            #region HelpBox
            {
                if (explosionBase.edit_fileRefreshLastTimeTicks != 0 &&
                    explosionBase.edit_colorSpace != PlayerSettings.colorSpace)
                {
                    EditorGUILayout.HelpBox("The color space of the generated data is different from the current color space.\nIf left as is, there will be a problem with the colors of the explosion voxels being slightly different.\nIf you update the asset or change the color space, please generate again.", MessageType.Warning);
                }
                if (EditorCommon.IsHighDefinitionRenderPipeline())
                {
                    EditorGUILayout.HelpBox("To apply Emission correctly in HDRP, you need to open the Shader Graph and switch nodes.", MessageType.Info);
                }
            }
            #endregion

            #region Generate
            {
                explosionBase.edit_generateFoldout = EditorGUILayout.Foldout(explosionBase.edit_generateFoldout, "Generate", guiStyleFoldoutBold);
                if (explosionBase.edit_generateFoldout)
                {
                    EditorGUILayout.BeginHorizontal(guiStyleSkinBox);
                    EditorGUILayout.BeginVertical();
                    {
                        #region Settings
                        {
                            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
                            {
                                EditorGUI.indentLevel++;
                                #region BirthRate
                                {
                                    EditorGUI.BeginChangeCheck();
                                    var edit_birthRate = EditorGUILayout.Slider("Birth Rate", explosionBase.edit_birthRate, 0f, 1f);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        Undo.RecordObject(explosionBase, "Inspector");
                                        explosionBase.edit_birthRate = edit_birthRate;
                                        if (explosionBase.edit_autoGenerate)
                                        {
                                            explosionCore.Generate();
                                            ForceRepaint();
                                        }
                                    }
                                }
                                #endregion
                                #region VisibleOnly
                                {
                                    EditorGUI.BeginChangeCheck();
                                    var edit_visibleOnly = EditorGUILayout.Toggle("Visible Only", explosionBase.edit_visibleOnly);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        Undo.RecordObject(explosionBase, "Inspector");
                                        explosionBase.edit_visibleOnly = edit_visibleOnly;
                                        if (explosionBase.edit_autoGenerate)
                                        {
                                            explosionCore.Generate();
                                            ForceRepaint();
                                        }
                                    }
                                }
                                #endregion
                                #region Velocity
                                {
                                    {
                                        var min = explosionBase.edit_velocityMin;
                                        var max = explosionBase.edit_velocityMax;
                                        EditorGUI.BeginChangeCheck();
                                        EditorGUILayout.MinMaxSlider(new GUIContent("Velocity"), ref min, ref max, 0f, 300f);
                                        if (EditorGUI.EndChangeCheck())
                                        {
                                            Undo.RecordObject(explosionBase, "Inspector");
                                            explosionBase.edit_velocityMin = min;
                                            explosionBase.edit_velocityMax = max;
                                            if (explosionBase.edit_autoGenerate)
                                            {
                                                explosionCore.Generate();
                                                ForceRepaint();
                                            }
                                        }
                                    }
                                    EditorGUI.indentLevel++;
                                    {
                                        EditorGUI.BeginChangeCheck();
                                        var edit_velocityMin = EditorGUILayout.FloatField("Min", explosionBase.edit_velocityMin);
                                        if (EditorGUI.EndChangeCheck())
                                        {
                                            Undo.RecordObject(explosionBase, "Inspector");
                                            edit_velocityMin = Mathf.Clamp(edit_velocityMin, 0f, 300f);
                                            edit_velocityMin = Mathf.Min(edit_velocityMin, explosionBase.edit_velocityMax);
                                            explosionBase.edit_velocityMin = edit_velocityMin;
                                            if (explosionBase.edit_autoGenerate)
                                            {
                                                explosionCore.Generate();
                                                ForceRepaint();
                                            }
                                        }
                                    }
                                    {
                                        EditorGUI.BeginChangeCheck();
                                        var edit_velocityMax = EditorGUILayout.FloatField("Max", explosionBase.edit_velocityMax);
                                        if (EditorGUI.EndChangeCheck())
                                        {
                                            Undo.RecordObject(explosionBase, "Inspector");
                                            edit_velocityMax = Mathf.Clamp(edit_velocityMax, 0f, 300f);
                                            edit_velocityMax = Mathf.Max(edit_velocityMax, explosionBase.edit_velocityMin);
                                            explosionBase.edit_velocityMax = edit_velocityMax;
                                            if (explosionBase.edit_autoGenerate)
                                            {
                                                explosionCore.Generate();
                                                ForceRepaint();
                                            }
                                        }
                                    }
                                    EditorGUI.indentLevel--;
                                }
                                #endregion
                                #region Legacy
                                if (EditorCommon.IsBuiltInRenderPipeline())
                                {
                                    EditorGUI.BeginChangeCheck();
                                    var edit_legacyShader = EditorGUILayout.Toggle(new GUIContent("Legacy Shader", "On:Surface Shader, Off:Shader Graph Shader"), explosionBase.edit_legacyShader);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        Undo.RecordObject(explosionBase, "Inspector");
                                        explosionBase.edit_legacyShader = edit_legacyShader;
                                        if (explosionBase.edit_autoGenerate)
                                        {
                                            explosionCore.Generate();
                                            ForceRepaint();
                                        }
                                    }
#if !VOXELIMPORTER_SHADERGRAPH
                                    EditorGUI.indentLevel++;
                                    if (!explosionBase.edit_legacyShader)
                                    {
                                        EditorGUILayout.HelpBox("You need Shader Graph for the new explosion shader to work.\nInstall Shader Graph from the Package Manager.\nOr enable \"Legacy Shader\" to use the surface shader.", MessageType.Error);
                                    }
                                    EditorGUI.indentLevel--;
#endif
                                }
                                #endregion
                                EditorGUI.indentLevel--;
                            }
                        }
                        #endregion

                        Inspector_MeshMaterial();

                        EditorGUILayout.Space();

                        #region Generate
                        {
                            EditorGUILayout.BeginHorizontal();
                            if (GUILayout.Button("Generate"))
                            {
                                Undo.RecordObject(explosionBase, "Generate Voxel Explosion");
                                explosionCore.Generate();
                                ForceRepaint();
                            }
                            {
                                EditorGUI.BeginChangeCheck();
                                var edit_autoGenerate = EditorGUILayout.ToggleLeft("Auto", explosionBase.edit_autoGenerate, GUILayout.Width(48f));
                                if (EditorGUI.EndChangeCheck())
                                {
                                    Undo.RecordObject(explosionBase, "Inspector");
                                    explosionBase.edit_autoGenerate = edit_autoGenerate;
                                    if (explosionBase.edit_autoGenerate)
                                    {
                                        explosionCore.Generate();
                                        ForceRepaint();
                                    }
                                }
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                        #endregion
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                }
            }
            #endregion
            #region Bake
            Inspector_Bake();
            #endregion
            #region Settings
            {
                explosionBase.edit_settingsFoldout = EditorGUILayout.Foldout(explosionBase.edit_settingsFoldout, "Settings", guiStyleFoldoutBold);
                if (explosionBase.edit_settingsFoldout)
                {
                    EditorGUILayout.BeginHorizontal(guiStyleSkinBox);
                    EditorGUILayout.BeginVertical();
                    {
                        #region Auto Set Explosion Center
                        {
                            EditorGUI.BeginChangeCheck();
                            var edit_autoSetExplosionCenter = EditorGUILayout.Toggle("Auto Set Explosion Center", explosionBase.edit_autoSetExplosionCenter);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(explosionBase, "Inspector");
                                explosionBase.edit_autoSetExplosionCenter = edit_autoSetExplosionCenter;
                                explosionCore.SetExplosionCenter();
                                ForceRepaint();
                            }
                        }
                        #endregion
                        #region Explosion Center
                        if (!explosionBase.edit_autoSetExplosionCenter)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUI.BeginChangeCheck();
                            var explosionCenter = EditorGUILayout.Vector3Field("Explosion Center", explosionBase.explosionCenter);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(explosionBase, "Inspector");
                                explosionBase.explosionCenter = explosionCenter;
                                explosionCore.SetExplosionCenter();
                                ForceRepaint();
                            }
                            EditorGUI.indentLevel--;
                        }
                        #endregion
                        #region Explosion Rotate
                        {
                            EditorGUI.BeginChangeCheck();
                            var explosionRotate = EditorGUILayout.FloatField("Explosion Rotate", explosionBase.explosionRotate);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(explosionBase, "Inspector");
                                explosionBase.explosionRotate = explosionRotate;
                                explosionBase.SetExplosionRotate(explosionBase.explosionRotate);
                                ForceRepaint();
                            }
                        }
                        #endregion
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                }
            }
            #endregion

            {
                if (!isPrefabEditable)
                {
                    EditorGUI.EndDisabledGroup();
                }
            }

            #region Preview
            {
                explosionBase.edit_previewFoldout = EditorGUILayout.Foldout(explosionBase.edit_previewFoldout, "Preview", guiStyleFoldoutBold);
                if (explosionBase.edit_previewFoldout)
                {
                    EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
                    EditorGUILayout.BeginHorizontal(guiStyleSkinBox);
                    EditorGUILayout.BeginVertical();
                    {
                        EditorGUI.BeginChangeCheck();
                        var edit_explosionLifeTime = EditorGUILayout.Slider("Life Time", explosionBase.edit_explosionLifeTime, 1f, 100f);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(explosionBase, "Inspector");
                            explosionBase.edit_explosionLifeTime = edit_explosionLifeTime;
                            explosionBase.edit_explosionPlay = false;
                            explosionBase.SetExplosionRate(explosionBase.edit_explosionRate);
                            ForceRepaint();
                        }
                    }
                    {
                        EditorGUILayout.BeginHorizontal();
                        {
                            EditorGUI.BeginChangeCheck();
                            var edit_explosionTime = EditorGUILayout.Slider("Time Line", explosionBase.edit_explosionTime, 0f, explosionBase.edit_explosionLifeTime);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(explosionBase, "Inspector");
                                explosionBase.edit_explosionTime = edit_explosionTime;
                                explosionBase.edit_explosionPlay = false;
                                ForceRepaint();
                            }
                        }
                        {
                            EditorGUI.BeginChangeCheck();
                            var flag = GUILayout.Toggle(explosionBase.edit_explosionPlay, playIcon, guiStylePlayButton);
                            if (EditorGUI.EndChangeCheck())
                            {
                                explosionBase.edit_explosionPlay = flag;
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                    EditorGUI.EndDisabledGroup();
                }

                if (!EditorApplication.isPlaying)
                {
                    explosionBase.SetExplosionRate(explosionBase.edit_explosionRate);
                }
            }
            #endregion

            serializedObject.ApplyModifiedProperties();
        }

        protected virtual void Inspector_MeshMaterial() { }

        protected virtual void Inspector_Bake() { }

        private void OnSceneCustomGUI(SceneView sceneView)
        {
            if (sceneView != SceneView.currentDrawingSceneView) return;
            if (explosionBase == null || explosionCore == null) return;

            #region Draw
            if (explosionBase.edit_explosionPlay || explosionBase.edit_explosionDraw)
            {
                explosionCore.SetExplosionCenter();
                explosionBase.SetExplosionRate(explosionBase.edit_explosionRate);
                explosionBase.DrawMesh();
            }
            #endregion
        }

        private void Update()
        {
            if (explosionBase == null || explosionCore == null) return;

            #region Auto Generate
            if (explosionBase.edit_autoGenerate && explosionCore.voxelBaseCore.IsVoxelFileExists())
            {
                if (explosionBase.edit_fileRefreshLastTimeTicks != explosionCore.voxelBase.fileRefreshLastTimeTicks)
                {
                    explosionCore.Generate();
                }
            }
            #endregion

            #region Play
            if (explosionBase.edit_explosionPlay)
            {
                if (explosionBase.edit_explosionTime < explosionBase.edit_explosionLifeTime)
                {
                    explosionBase.edit_explosionTime += Time.deltaTime;
                    explosionBase.edit_explosionTime = Mathf.Min(explosionBase.edit_explosionTime, explosionBase.edit_explosionLifeTime);
                }
                else
                {
                    explosionBase.edit_explosionTime = 0f;
                    explosionBase.edit_explosionPlay = false;
                    explosionBase.edit_explosionDraw = false;
                }
            }
            #endregion

            #region Draw
            if (explosionBase.edit_explosionPlay)
            {
                Repaint();
            }
            if (explosionBase.edit_explosionPlay || explosionBase.edit_explosionDraw)
            {
                ForceRepaint();
            }
            #endregion
        }

        protected void ForceRepaint()
        {
            if (explosionBase == null) return;

            explosionBase.enabled = !explosionBase.enabled;
            explosionBase.enabled = !explosionBase.enabled;

            explosionBase.edit_explosionDraw = true;
        }
    }
}
