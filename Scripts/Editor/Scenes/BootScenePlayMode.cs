using UnityEditor;
using UnityEditor.SceneManagement;

namespace SceneTools
{
    // Makes the Play button always start from the boot scene (first enabled scene in Build Settings).
    // When play mode stops, Unity returns to whatever scene was open in the editor.
    [InitializeOnLoad]
    public static class BootScenePlayMode
    {
        private const string MenuPath = "Tools/Scenes/Play From Boot Scene";
        private const string PrefKey = "Jasbrela.Essentials.PlayFromBootScene";

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set
            {
                EditorPrefs.SetBool(PrefKey, value);
                Refresh();
            }
        }

        static BootScenePlayMode()
        {
            EditorBuildSettings.sceneListChanged += Refresh;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += Refresh;
        }

        public static SceneAsset GetBootScene()
        {
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled) continue;
                var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path);
                if (asset != null) return asset;
            }

            return null;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) Refresh();
        }

        private static void Refresh()
        {
            EditorSceneManager.playModeStartScene = Enabled ? GetBootScene() : null;
        }

        [MenuItem(MenuPath, priority = 0)]
        private static void Toggle() => Enabled = !Enabled;

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }
    }
}
