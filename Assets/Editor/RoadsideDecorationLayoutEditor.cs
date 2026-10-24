using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using TheRacer;

[CustomEditor(typeof(RoadsideDecorationLayout))]
public sealed class RoadsideDecorationLayoutEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Aligns buildings and cube props to the ground using their visible mesh bounds. X/Z placement and rotation stay unchanged.",
            MessageType.Info);
        RoadsideDecorationLayout layout = (RoadsideDecorationLayout)target;
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Align Left Side"))
        {
            int count = layout.AlignSideToGround(layout.transform.Find("Left Side"));
            Debug.Log("Aligned " + count + " props on the left side.", target);
        }
        if (GUILayout.Button("Align Right Side"))
        {
            int count = layout.AlignSideToGround(layout.transform.Find("Right Side"));
            Debug.Log("Aligned " + count + " props on the right side.", target);
        }
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("Align Placed Models to Ground"))
        {
            int count = layout.AlignPlacedModelsToGround();
            Debug.Log("Aligned " + count + " roadside models to the ground.", target);
        }
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "This replaces Left Side contents with mirrored copies of Right Side. The change is undoable.",
            MessageType.Warning);
        if (GUILayout.Button("Replace Left Side with Mirror of Right"))
        {
            int count = layout.MirrorRightSideToLeft();
            Debug.Log("Replaced the left side with " + count + " mirrored props from the right side.", target);
        }
        if (GUILayout.Button("Move Side Buildings 1.5 m Away from Track"))
        {
            int count = layout.MoveSidePropsOutward(1.5f);
            Debug.Log("Moved " + count + " side props outward by 1.5 m.", target);
        }
        if (GUILayout.Button("Create Editable Building Ground Objects"))
        {
            int count = layout.CreateBuildingGroundObjects();
            Debug.Log("Created or retained " + count + " editable building ground objects.", target);
        }
        if (GUILayout.Button("Add Random Streetlights on Both Sides"))
        {
            int count = layout.AddRandomStreetlights();
            Debug.Log("Added " + count + " streetlight model instances across both sides.", target);
        }
    }
}

[InitializeOnLoad]
internal sealed class MusicFolderCatalogImporter : AssetPostprocessor
{
    private const string MusicFolder = "Assets/music";
    private const string ResourcesFolder = "Assets/Resources";
    private const string CatalogPath = ResourcesFolder + "/MusicPlaylist.asset";

    static MusicFolderCatalogImporter() { EditorApplication.delayCall += RefreshCatalog; }

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (imported.Concat(deleted).Concat(moved).Concat(movedFrom)
            .Any(path => path.StartsWith(MusicFolder + "/", StringComparison.OrdinalIgnoreCase)))
            EditorApplication.delayCall += RefreshCatalog;
    }

    private static void RefreshCatalog()
    {
        if (!Directory.Exists(MusicFolder)) return;
        if (!AssetDatabase.IsValidFolder(ResourcesFolder)) AssetDatabase.CreateFolder("Assets", "Resources");
        MusicPlaylistCatalog catalog = AssetDatabase.LoadAssetAtPath<MusicPlaylistCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<MusicPlaylistCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        AudioClip[] clips = AssetDatabase.FindAssets("t:AudioClip", new[] { MusicFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !IsSoundEffect(Path.GetFileNameWithoutExtension(path)))
            .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
            .Where(clip => clip != null)
            .ToArray();
        catalog.SetClips(clips);
        AudioClip engineStartClip = AssetDatabase.FindAssets("t:AudioClip", new[] { MusicFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => Path.GetFileNameWithoutExtension(path).IndexOf("engine_start", StringComparison.OrdinalIgnoreCase) >= 0)
            .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
            .FirstOrDefault(clip => clip != null);
        catalog.SetEngineStartClip(engineStartClip);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }

    private static bool IsSoundEffect(string name)
    {
        string lower = name.ToLowerInvariant();
        return lower.Contains("engine") || lower.Contains("sfx") || lower.Contains("horn")
            || lower.Contains("drift") || lower.Contains("impact");
    }
}
