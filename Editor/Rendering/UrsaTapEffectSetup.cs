using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Ursa.UI;
using Ursa.UI.Rendering;

namespace Ursa.Editor
{
    /// <summary>
    /// Configures every URP renderer used by Graphics or Quality settings for tap distortion.
    /// </summary>
    public static class UrsaTapEffectSetup
    {
        private const string MenuPath = "Ursa/Setup Tap Effect";
        private const string ProfilePath = "Assets/Resources/Ursa/UrsaTapRippleProfile.asset";

        [MenuItem(MenuPath, priority = 12)]
        public static void Setup()
        {
            try
            {
                var pipelineAssets = FindPipelineAssets();
                if (pipelineAssets.Count == 0)
                {
                    ReportFailure("使用中のUniversal Render Pipeline Assetが見つかりませんでした。");
                    return;
                }

                var rendererDataAssets = FindRendererDataAssets(pipelineAssets);
                if (rendererDataAssets.Count == 0)
                {
                    ReportFailure("使用中のUniversal Renderer Dataが見つかりませんでした。");
                    return;
                }

                Undo.IncrementCurrentGroup();
                var undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Setup Ursa Tap Effect");

                var addedCount = 0;
                var enabledCount = 0;
                foreach (var rendererData in rendererDataAssets)
                {
                    var result = EnsureRendererFeature(rendererData);
                    if (result == FeatureSetupResult.Added)
                        addedCount++;
                    else if (result == FeatureSetupResult.Enabled)
                        enabledCount++;
                }

                var profile = GetOrCreateProfile(out var profileCreated);
                var settings = UrsaSettings.Instance;
                Undo.RecordObject(settings, "Assign Ursa tap effect profile");
                settings.TapEffectEnabled = true;
                settings.DefaultTapEffectProfile = profile;
                EditorUtility.SetDirty(settings);

                Undo.CollapseUndoOperations(undoGroup);
                AssetDatabase.SaveAssets();

                var unchangedCount = rendererDataAssets.Count - addedCount - enabledCount;
                var rendererNames = string.Join(", ", rendererDataAssets.Select(data => data.name));
                var message =
                    $"タップエフェクトの設定が完了しました。\n\n" +
                    $"対象Renderer Data: {rendererNames}\n" +
                    $"Feature追加: {addedCount}\n" +
                    $"Feature有効化: {enabledCount}\n" +
                    $"設定済み: {unchangedCount}\n" +
                    $"Profile: {(profileCreated ? "作成" : "再利用")} ({ProfilePath})";

                Debug.Log($"<color=lime>[Ursa]</color> {message.Replace("\n", " ")}");
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog("Setup Tap Effect", message, "OK");
                    Selection.activeObject = settings;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ReportFailure($"設定中にエラーが発生しました。\n{exception.Message}");
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateSetup()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static List<UniversalRenderPipelineAsset> FindPipelineAssets()
        {
            var assets = new HashSet<UniversalRenderPipelineAsset>();

#pragma warning disable CS0618
            AddPipelineAsset(assets, GraphicsSettings.defaultRenderPipeline);
#pragma warning restore CS0618
            for (var index = 0; index < QualitySettings.names.Length; index++)
                AddPipelineAsset(assets, QualitySettings.GetRenderPipelineAssetAt(index));

            return assets.OrderBy(asset => asset.name).ToList();
        }

        private static void AddPipelineAsset(
            ISet<UniversalRenderPipelineAsset> assets,
            RenderPipelineAsset candidate)
        {
            if (candidate is UniversalRenderPipelineAsset universalAsset)
                assets.Add(universalAsset);
        }

        private static List<ScriptableRendererData> FindRendererDataAssets(
            IEnumerable<UniversalRenderPipelineAsset> pipelineAssets)
        {
            var results = new HashSet<ScriptableRendererData>();
            foreach (var pipelineAsset in pipelineAssets)
            {
                var serializedAsset = new SerializedObject(pipelineAsset);
                var rendererDataList = serializedAsset.FindProperty("m_RendererDataList");
                if (rendererDataList == null || !rendererDataList.isArray)
                    continue;

                for (var index = 0; index < rendererDataList.arraySize; index++)
                {
                    var rendererData = rendererDataList
                        .GetArrayElementAtIndex(index)
                        .objectReferenceValue as ScriptableRendererData;
                    if (rendererData != null)
                        results.Add(rendererData);
                }
            }

            return results.OrderBy(data => data.name).ToList();
        }

        private static FeatureSetupResult EnsureRendererFeature(ScriptableRendererData rendererData)
        {
            var serializedData = new SerializedObject(rendererData);
            serializedData.Update();
            var features = serializedData.FindProperty("m_RendererFeatures");
            var featureMap = serializedData.FindProperty("m_RendererFeatureMap");
            if (features == null || featureMap == null)
                throw new InvalidOperationException($"Renderer Feature listを取得できません: {rendererData.name}");

            for (var index = 0; index < features.arraySize; index++)
            {
                if (features.GetArrayElementAtIndex(index).objectReferenceValue
                    is not UrsaTapRippleRendererFeature existingFeature)
                {
                    continue;
                }

                if (existingFeature.isActive)
                    return FeatureSetupResult.Unchanged;

                Undo.RecordObject(existingFeature, "Enable Ursa tap ripple renderer feature");
                existingFeature.SetActive(true);
                EditorUtility.SetDirty(existingFeature);
                return FeatureSetupResult.Enabled;
            }

            var feature = ScriptableObject.CreateInstance<UrsaTapRippleRendererFeature>();
            feature.name = nameof(UrsaTapRippleRendererFeature);
            Undo.RegisterCreatedObjectUndo(feature, "Add Ursa tap ripple renderer feature");

            if (EditorUtility.IsPersistent(rendererData))
                AssetDatabase.AddObjectToAsset(feature, rendererData);

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            featureMap.arraySize++;
            featureMap.GetArrayElementAtIndex(featureMap.arraySize - 1).longValue = localId;

            serializedData.ApplyModifiedProperties();
            EditorUtility.SetDirty(rendererData);
            return FeatureSetupResult.Added;
        }

        private static TapEffectProfile GetOrCreateProfile(out bool created)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TapEffectProfile>(ProfilePath);
            if (existing != null)
            {
                created = false;
                return existing;
            }

            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/Ursa");

            var profile = ScriptableObject.CreateInstance<TapEffectProfile>();
            profile.name = "UrsaTapRippleProfile";
            var serializedProfile = new SerializedObject(profile);
            serializedProfile.FindProperty("_ringEnabled").boolValue = false;
            serializedProfile.FindProperty("_color").colorValue = new Color(0.2f, 0.92f, 1f, 0.95f);
            serializedProfile.FindProperty("_duration").floatValue = 1.1f;
            serializedProfile.FindProperty("_startDiameter").floatValue = 8f;
            serializedProfile.FindProperty("_endDiameter").floatValue = 180f;
            serializedProfile.FindProperty("_ringThickness").floatValue = 0.08f;
            serializedProfile.FindProperty("_distortionStrength").floatValue = 0.05f;
            serializedProfile.FindProperty("_maxConcurrentEffects").intValue = 12;
            serializedProfile.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(profile, ProfilePath);
            Undo.RegisterCreatedObjectUndo(profile, "Create Ursa tap ripple profile");
            created = true;
            return profile;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var separator = path.LastIndexOf('/');
            if (separator <= 0)
                throw new InvalidOperationException($"作成できないAsset folderです: {path}");

            var parent = path.Substring(0, separator);
            var folderName = path.Substring(separator + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static void ReportFailure(string message)
        {
            Debug.LogError($"[Ursa] Setup Tap Effect: {message}");
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("Setup Tap Effect", message, "OK");
        }

        private enum FeatureSetupResult
        {
            Unchanged,
            Added,
            Enabled
        }
    }
}
