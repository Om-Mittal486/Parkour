using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Stillworks.Editor
{
    // Explicit local requests only. Allows the same bake to run in an already-open Editor.
    [InitializeOnLoad]
    public static class StillworksEditorBridge
    {
        private const string Request = "Logs/Stillworks-editor-request.txt";
        private static double nextCheck;
        static StillworksEditorBridge() { EditorApplication.update += Update; }
        private static void Update()
        {
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 2;
            if (Application.isBatchMode || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) return; // Never discard in-progress user edits.
            string command = File.ReadAllText(Request).Trim();
            File.Delete(Request);
            try
            {
                if (command == "bake")
                {
                    StillworksBuilder.BuildAndCapture();
                    // Capture uses an unsaved temporary camera arrangement; restore the playable file.
                    EditorSceneManager.OpenScene("Assets/Scenes/Stillworks.unity", OpenSceneMode.Single);
                }
                else if (command == "playtest") StillworksPlaytest.Begin();
                else throw new InvalidOperationException("Unknown Stillworks request.");
                File.WriteAllText("Logs/Stillworks-editor-result.txt", command + " completed");
            }
            catch (Exception e) { File.WriteAllText("Logs/Stillworks-editor-result.txt", e.ToString()); Debug.LogException(e); }
        }
    }
}
