#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Formats.Fbx.Exporter;
using UnityEngine;

namespace PROTOCO.FBXNormalDisabler
{
    /// <summary>
    /// FBX内のBlendShapeを選択して、デルタ法線を0ベクトルで上書きした
    /// Meshアセット(.mesh)またはFBXファイルを生成するエディタツール。
    /// メニュー: Tools/PROTOCO/BlendShapeNormalDisabler
    /// </summary>
    public class FBXNormalDisablerWindow : EditorWindow
    {
        private enum OutputFormat { BinaryFBX, AsciiFBX, MeshAsset }

        private GameObject _fbxAsset;
        private GameObject _prevFbxAsset;
        private readonly List<MeshShapeEntry> _entries = new List<MeshShapeEntry>();
        private Vector2 _scroll;
        private OutputFormat _outputFormat = OutputFormat.BinaryFBX;
        private bool _overwrite = false;

        [MenuItem("Tools/PROTOCO/BlendShapeNormalDisabler")]
        private static void Open() =>
            GetWindow<FBXNormalDisablerWindow>("BlendShapeNormalDisabler");

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("BlendShapeNormalDisabler", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            var newFbx = (GameObject)EditorGUILayout.ObjectField(
                "FBX アセット", _fbxAsset, typeof(GameObject), false);

            if (newFbx != _prevFbxAsset)
            {
                _fbxAsset = newFbx;
                _prevFbxAsset = newFbx;
                RefreshBlendShapes();
            }

            EditorGUILayout.Space(6);

            if (_entries.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    _fbxAsset == null
                        ? "FBXアセットをフィールドにドロップしてください。"
                        : "このFBXにBlendShapeが見つかりませんでした。",
                    MessageType.Info);
                return;
            }

            // ─── 全選択 / 全解除ボタン ─────────────────────────
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("すべて選択", GUILayout.Width(110)))
                    SetAllToggles(true);
                if (GUILayout.Button("すべて解除", GUILayout.Width(110)))
                    SetAllToggles(false);
            }

            EditorGUILayout.Space(4);

            // ─── BlendShape リスト ────────────────────────────
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            string currentMesh = null;
            foreach (var entry in _entries)
            {
                if (entry.MeshName != currentMesh)
                {
                    if (currentMesh != null) EditorGUILayout.Space(4);
                    currentMesh = entry.MeshName;
                    EditorGUILayout.LabelField($"Mesh: {currentMesh}", EditorStyles.boldLabel);
                }

                entry.Enabled = EditorGUILayout.ToggleLeft(entry.ShapeName, entry.Enabled);
            }

            EditorGUILayout.EndScrollView();
            // ─── 出力形式 ───────────────────────────────────────────────
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("出力形式", EditorStyles.boldLabel);
            _outputFormat = (OutputFormat)GUILayout.Toolbar(
                (int)_outputFormat,
                new[] { "Binary FBX", "ASCII FBX", "Mesh (.mesh)" },
                GUILayout.Height(28));

            EditorGUILayout.HelpBox($"出力: {OutputFileName()}", MessageType.None);
            _overwrite = EditorGUILayout.ToggleLeft("上書き (OFF時は Modified フォルダに別名出力)", _overwrite);

            EditorGUILayout.HelpBox(
                "マテリアルスロット名はunityで使用しているマテリアル名に上書きされます",
                MessageType.Warning);

            // ─── 適用ボタン ───────────────────────────────────
            EditorGUILayout.Space(4);
            bool anyChecked = _entries.Any(e => e.Enabled);
            using (new EditorGUI.DisabledScope(!anyChecked))
            {
                if (GUILayout.Button("選択したBlendShapeの法線情報を削除", GUILayout.Height(38)))
                    Apply();
            }
        }

        // ──────────────────────────────────────────────────────────────

        private string OutputFileName()
        {
            return _outputFormat switch
            {
                OutputFormat.MeshAsset => _overwrite ? "<FBX名>_<Mesh名>.mesh" : "Modified/<FBX名>_<Mesh名>.mesh",
                _                      => _overwrite ? "<FBX名>.fbx" : "Modified/<FBX名>.fbx",
            };
        }

        private void RefreshBlendShapes()
        {
            _entries.Clear();
            if (_fbxAsset == null) return;

            string path = AssetDatabase.GetAssetPath(_fbxAsset);
            if (string.IsNullOrEmpty(path)) return;

            var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var asset in allAssets)
            {
                if (!(asset is Mesh mesh) || mesh.blendShapeCount == 0) continue;
                for (int i = 0; i < mesh.blendShapeCount; i++)
                    _entries.Add(new MeshShapeEntry(mesh, i));
            }
        }

        private void SetAllToggles(bool value)
        {
            foreach (var e in _entries) e.Enabled = value;
        }

        private void Apply()
        {
            var byMesh = _entries
                .Where(e => e.Enabled)
                .GroupBy(e => e.Mesh)
                .ToDictionary(g => g.Key, g => new HashSet<string>(g.Select(e => e.ShapeName)));

            if (byMesh.Count == 0) return;

            string fbxPath = AssetDatabase.GetAssetPath(_fbxAsset);
            string dir     = Path.GetDirectoryName(fbxPath).Replace('\\', '/');
            string fbxName = Path.GetFileNameWithoutExtension(fbxPath);

            if (_outputFormat == OutputFormat.MeshAsset)
                ApplyAsMesh(byMesh, dir, fbxName, _overwrite);
            else
                ApplyAsFbx(byMesh, dir, fbxName, _overwrite,
                    ascii: _outputFormat == OutputFormat.AsciiFBX);
        }

        // ─── .mesh 出力 ───────────────────────────────────────────────

        private void ApplyAsMesh(
            Dictionary<Mesh, HashSet<string>> byMesh,
            string dir, string fbxName, bool overwrite)
        {
            int savedCount = 0;
            var savedPaths = new List<string>();
            string outputDir = overwrite ? dir : $"{dir}/Modified";
            if (!overwrite && !AssetDatabase.IsValidFolder(outputDir))
                AssetDatabase.CreateFolder(dir, "Modified");

            foreach (var kvp in byMesh)
            {
                Mesh   srcMesh  = kvp.Key;
                Mesh   newMesh  = BuildModifiedMesh(srcMesh, kvp.Value);
                string savePath = $"{outputDir}/{fbxName}_{srcMesh.name}.mesh";

                if (AssetDatabase.LoadAssetAtPath<Mesh>(savePath) != null)
                    AssetDatabase.DeleteAsset(savePath);

                AssetDatabase.CreateAsset(newMesh, savePath);
                savedCount++;
                savedPaths.Add(savePath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string list = string.Join("\n", savedPaths.Select(p => "-" + p));
            EditorUtility.DisplayDialog("完了",
                $"{savedCount} 個のメッシュを保存しました:\n{list}", "OK");
        }

        // ─── FBX 出力 ─────────────────────────────────────────────────

        private void ApplyAsFbx(
            Dictionary<Mesh, HashSet<string>> byMesh,
            string dir, string fbxName, bool overwrite,
            bool ascii = false)
        {
            // 修正済みメッシュをメモリ上に構築（Mesh名 → 修正Mesh）
            var modifiedMeshes = new Dictionary<string, Mesh>();
            foreach (var kvp in byMesh)
                modifiedMeshes[kvp.Key.name] = BuildModifiedMesh(kvp.Key, kvp.Value);

            // 出力パス（overwrite時はTempへ出力→本体へFBXのみコピー）
            string sourceAssetPath = AssetDatabase.GetAssetPath(_fbxAsset);
            string outputDir;
            string tempOutputDir = null;
            string exportAssetPath;
            string finalImportAssetPath;

            if (overwrite)
            {
                tempOutputDir = $"{dir}/Temp_{fbxName}";
                if (AssetDatabase.IsValidFolder(tempOutputDir))
                    AssetDatabase.DeleteAsset(tempOutputDir);
                AssetDatabase.CreateFolder(dir, $"Temp_{fbxName}");

                outputDir = tempOutputDir;
                exportAssetPath = $"{outputDir}/{fbxName}.fbx";
                finalImportAssetPath = sourceAssetPath;
            }
            else
            {
                outputDir = $"{dir}/Modified";
                if (!AssetDatabase.IsValidFolder(outputDir))
                    AssetDatabase.CreateFolder(dir, "Modified");

                exportAssetPath = $"{outputDir}/{fbxName}.fbx";
                finalImportAssetPath = exportAssetPath;
            }

            string projectRoot = Application.dataPath.Substring(
                                     0, Application.dataPath.Length - "Assets".Length);
            string absExportPath = Path.GetFullPath(
                                     Path.Combine(projectRoot, exportAssetPath));
            string absFinalPath = Path.GetFullPath(
                                    Path.Combine(projectRoot, finalImportAssetPath));

            // FBXプレハブを一時的にシーンへ展開してメッシュを差し替え
            var tempRoot = (GameObject)PrefabUtility.InstantiatePrefab(_fbxAsset);
            var tempPlaceholderMaterials = new List<Material>();
            try
            {
                foreach (var smr in tempRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (smr.sharedMesh != null &&
                        modifiedMeshes.TryGetValue(smr.sharedMesh.name, out var m))
                        smr.sharedMesh = m;
                }
                foreach (var mf in tempRoot.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh != null &&
                        modifiedMeshes.TryGetValue(mf.sharedMesh.name, out var m))
                        mf.sharedMesh = m;
                }

                // FBXにテクスチャ参照を含めないため、名前保持の複製マテリアルを一時差し替え
                foreach (var r in tempRoot.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterials = BuildTexturelessMaterialsKeepingNames(r.sharedMaterials, tempPlaceholderMaterials);

                // 余計なラッパーノードを作らないため、ルート自身ではなく直下の子を出力する
                var exportObjects = tempRoot.transform
                    .Cast<Transform>()
                    .Select(t => (UnityEngine.Object)t.gameObject)
                    .ToArray();

                try   { ExportWithFormat(absExportPath, exportObjects, ascii); }
                finally { }
            }
            finally
            {
                DestroyImmediate(tempRoot);
                foreach (var mat in tempPlaceholderMaterials)
                    DestroyImmediate(mat);
                foreach (var m in modifiedMeshes.Values)
                    DestroyImmediate(m);
            }

            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(exportAssetPath, ImportAssetOptions.ForceSynchronousImport);

            if (overwrite)
            {
                File.Copy(absExportPath, absFinalPath, overwrite: true);
                AssetDatabase.ImportAsset(finalImportAssetPath, ImportAssetOptions.ForceSynchronousImport);

                if (!string.IsNullOrEmpty(tempOutputDir) && AssetDatabase.IsValidFolder(tempOutputDir))
                    AssetDatabase.DeleteAsset(tempOutputDir);
            }

            // overwrite時は既存.metaを維持するため、Importer設定の再保存を行わない
            if (!overwrite)
            {
                var srcImporter = AssetImporter.GetAtPath(sourceAssetPath) as ModelImporter;
                var dstImporter = AssetImporter.GetAtPath(finalImportAssetPath) as ModelImporter;
                if (dstImporter != null)
                {
                    ApplyPostImportOverrides(dstImporter, srcImporter);
                    dstImporter.SaveAndReimport();
                }
            }

            EditorUtility.DisplayDialog("完了",
                $"FBXを保存しました:\n{finalImportAssetPath}", "OK");
        }

        // ─── フォーマット指定付きFBXエクスポート ───────────────────────

        private static Material[] BuildTexturelessMaterialsKeepingNames(Material[] originals, List<Material> created)
        {
            var result = new Material[originals.Length];

            for (int i = 0; i < originals.Length; i++)
            {
                var src = originals[i];
                var mat = src != null
                    ? new Material(src)
                    : new Material(Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"));

                string name = src != null ? src.name : $"slot_{i}";
                mat.name = name;

                // すべてのTextureプロパティ参照を除去
                var shader = mat.shader;
                if (shader != null)
                {
                    int propCount = ShaderUtil.GetPropertyCount(shader);
                    for (int p = 0; p < propCount; p++)
                    {
                        if (ShaderUtil.GetPropertyType(shader, p) != ShaderUtil.ShaderPropertyType.TexEnv)
                            continue;

                        string propName = ShaderUtil.GetPropertyName(shader, p);
                        if (mat.HasProperty(propName))
                            mat.SetTexture(propName, null);
                    }
                }

                created.Add(mat);
                result[i] = mat;
            }

            return result;
        }

        // ExportFormat はゲッターのみ。SetExportFormat() メソッドで設定し、
        // internal な ExportObjects(string, Object[], IExportOptions, Dictionary) を呼ぶ。
        // ExportSettings.ExportFormat: ASCII=0, Binary=1
        private static void ExportWithFormat(string absPath, UnityEngine.Object[] exportObjects, bool ascii)
        {
            var asm      = typeof(ModelExporter).Assembly;
            var optsType = asm.GetType("UnityEditor.Formats.Fbx.Exporter.ExportModelSettingsSerialize");
            var fmtType  = asm.GetType("UnityEditor.Formats.Fbx.Exporter.ExportSettings+ExportFormat");
            var objPosType = asm.GetType("UnityEditor.Formats.Fbx.Exporter.ExportSettings+ObjectPosition");

            if (optsType != null && fmtType != null)
            {
                try
                {
                    // ExportModelSettingsSerialize を生成して形式を設定
                    var opts   = Activator.CreateInstance(optsType);
                    var setFmt = optsType.GetMethod("SetExportFormat",
                                     BindingFlags.Public | BindingFlags.Instance);
                    setFmt?.Invoke(opts, new[] { Enum.ToObject(fmtType, ascii ? 0 : 1) });

                    // 複数ルート書き出し時の中心寄せを防ぐ
                    var setObjPos = optsType.GetMethod("SetObjectPosition",
                                        BindingFlags.Public | BindingFlags.Instance);
                    if (setObjPos != null && objPosType != null)
                        setObjPos.Invoke(opts, new[] { Enum.ToObject(objPosType, 1) }); // WorldAbsolute

                    // Maya互換命名を無効化して、'.' やスペースを '_' に変換しない
                    var setMayaNames = optsType.GetMethod("SetUseMayaCompatibleNames",
                                          BindingFlags.Public | BindingFlags.Instance);
                    setMayaNames?.Invoke(opts, new object[] { false });

                    // internal ExportObjects(string, Object[], IExportOptions, Dictionary) を呼ぶ
                    var exportMethod = typeof(ModelExporter)
                        .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                        .FirstOrDefault(m => m.Name == "ExportObjects"
                                          && m.GetParameters().Length == 4);
                    if (exportMethod != null)
                    {
                        exportMethod.Invoke(null,
                            new object[] { absPath, exportObjects, opts, null });
                        return;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[FBXNormalDisabler] フォーマット設定に失敗、デフォルトでエクスポート: {e.Message}");
                }
            }

            // フォールバック: デフォルト設定でエクスポート
            ModelExporter.ExportObjects(absPath, exportObjects);
        }

        // ─── Import設定上書き ───────────────────────────────────────

        private static void ApplyPostImportOverrides(ModelImporter importer, ModelImporter srcImporter)
        {
            // Unity自動生成のmetaをベースに、このツールで必要な設定のみ固定する
            importer.importBlendShapeNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;

            // Avatarが失われるのを防ぐため、Avatar関連だけ選択的に引き継ぐ
            if (srcImporter != null)
            {
                importer.animationType = srcImporter.animationType;
                importer.avatarSetup = srcImporter.avatarSetup;
                importer.sourceAvatar = srcImporter.sourceAvatar;
                importer.autoGenerateAvatarMappingIfUnspecified = srcImporter.autoGenerateAvatarMappingIfUnspecified;
            }
        }

        // ─── 共通: 修正済みメッシュ構築 ──────────────────────────────

        private static Mesh BuildModifiedMesh(Mesh src, HashSet<string> shapesToZero)
        {
            Mesh dst = Instantiate(src);
            dst.ClearBlendShapes();

            int vc = src.vertexCount;
            var dv = new Vector3[vc];
            var dn = new Vector3[vc];
            var dt = new Vector3[vc];

            for (int si = 0; si < src.blendShapeCount; si++)
            {
                string name       = src.GetBlendShapeName(si);
                bool   zeroNormal = shapesToZero.Contains(name);
                int    frameCount = src.GetBlendShapeFrameCount(si);

                for (int fi = 0; fi < frameCount; fi++)
                {
                    float weight = src.GetBlendShapeFrameWeight(si, fi);
                    src.GetBlendShapeFrameVertices(si, fi, dv, dn, dt);

                    if (zeroNormal)
                    {
                        var tiny = new Vector3(1.1e-5f, 1.1e-5f, 1.1e-5f);
                        for (int i = 0; i < dn.Length; i++) dn[i] = tiny;
                    }

                    dst.AddBlendShapeFrame(name, weight, dv, dn, dt);
                }
            }

            return dst;
        }

        // ──────────────────────────────────────────────────────────────

        private class MeshShapeEntry
        {
            public Mesh   Mesh      { get; }
            public string MeshName  { get; }
            public string ShapeName { get; }
            public bool   Enabled   { get; set; }

            public MeshShapeEntry(Mesh mesh, int shapeIndex)
            {
                Mesh      = mesh;
                MeshName  = mesh.name;
                ShapeName = mesh.GetBlendShapeName(shapeIndex);
                Enabled   = false;
            }
        }
    }
}
#endif
