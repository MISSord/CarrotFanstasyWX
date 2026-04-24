using UnityEngine;
using UnityEditor;
using System.IO;

public class AnimationClipExtractor : EditorWindow
{
    [MenuItem("Tools/Extract Animation Clips")]
    static void ExtractClips()
    {
        Object[] selectedObjects = Selection.GetFiltered(typeof(Object), SelectionMode.DeepAssets);

        foreach (Object selectedObject in selectedObjects)
        {
            if (!(selectedObject is GameObject)) continue;

            string assetPath = AssetDatabase.GetAssetPath(selectedObject);
            ModelImporter modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;

            if (modelImporter == null) continue;

            if (modelImporter.clipAnimations.Length == 0)
            {
                Debug.LogWarning($"FBX file {assetPath} contains no animation clips.");
                continue;
            }

            string targetDirectory = Path.GetDirectoryName(assetPath) + "/Extracted_Animations_" + Path.GetFileNameWithoutExtension(assetPath);
            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            foreach (ModelImporterClipAnimation clipAnimation in modelImporter.clipAnimations)
            {
                AnimationClip originalClip = (AnimationClip)AssetDatabase.LoadAssetAtPath(assetPath, typeof(AnimationClip));
                if (originalClip == null) continue;

                AnimationClip newClip = new AnimationClip();
                EditorUtility.CopySerialized(originalClip, newClip);

                newClip.name = clipAnimation.name;

                string outputPath = AssetDatabase.GenerateUniqueAssetPath(targetDirectory + "/" + newClip.name + ".anim");
                AssetDatabase.CreateAsset(newClip, outputPath);
            }
            AssetDatabase.Refresh();
            Debug.Log($"Extracted animations from {assetPath} to {targetDirectory}");
        }
    }
}
