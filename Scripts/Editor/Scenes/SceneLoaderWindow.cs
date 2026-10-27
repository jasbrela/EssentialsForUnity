using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SceneTools
{
    // Lists build scenes first, then every other scene under Assets, with quick open buttons.
    public class SceneLoaderWindow : EditorWindow
    {
        private readonly List<string> _buildScenes = new();
        private readonly List<string> _otherScenes = new();
        private Vector2 _scroll;

        [MenuItem("Tools/Scenes/Scene Loader", priority = 20)]
        private static void Open() => GetWindow<SceneLoaderWindow>("Scenes");

        private void OnEnable()
        {
            Reload();
            EditorBuildSettings.sceneListChanged += Reload;
            EditorApplication.projectChanged += Reload;
        }

        private void OnDisable()
        {
            EditorBuildSettings.sceneListChanged -= Reload;
            EditorApplication.projectChanged -= Reload;
        }

        private void Reload()
        {
            _buildScenes.Clear();
            _buildScenes.AddRange(EditorBuildSettings.scenes.Select(s => s.path));

            _otherScenes.Clear();
            _otherScenes.AddRange(AssetDatabase.FindAssets("t:Scene", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !_buildScenes.Contains(path))
                .OrderBy(path => path));

            Repaint();
        }

        private void OnGUI()
        {
            EditorGUI.BeginDisabledGroup(EditorApplication.isPlayingOrWillChangePlaymode);

            var playFromBoot = EditorGUILayout.ToggleLeft("Play From Boot Scene", BootScenePlayMode.Enabled);
            if (playFromBoot != BootScenePlayMode.Enabled) BootScenePlayMode.Enabled = playFromBoot;

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Build Scenes", EditorStyles.boldLabel);
            if (_buildScenes.Count == 0) EditorGUILayout.HelpBox("No scenes in Build Settings.", MessageType.Info);
            foreach (var path in _buildScenes) DrawScene(path);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Other Scenes", EditorStyles.boldLabel);
            foreach (var path in _otherScenes) DrawScene(path);

            EditorGUILayout.EndScrollView();
            EditorGUI.EndDisabledGroup();
        }

        private static void DrawScene(string path)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(System.IO.Path.GetFileNameWithoutExtension(path), EditorStyles.miniButtonLeft))
                    OpenScene(path, OpenSceneMode.Single);

                if (GUILayout.Button("+", EditorStyles.miniButtonMid, GUILayout.Width(24)))
                    OpenScene(path, OpenSceneMode.Additive);

                if (GUILayout.Button("Ping", EditorStyles.miniButtonRight, GUILayout.Width(40)))
                    EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(path));
            }
        }

        private static void OpenScene(string path, OpenSceneMode mode)
        {
            if (mode == OpenSceneMode.Single && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(path, mode);
        }
    }
}
