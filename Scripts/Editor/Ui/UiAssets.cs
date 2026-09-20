using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace XNodeEditor.Ui {
    internal static class UiAssets {
        public static VisualTreeAsset LoadUxml(string fileName) {
            return Load<VisualTreeAsset>(fileName);
        }

        public static StyleSheet LoadUss(string fileName) {
            return Load<StyleSheet>(fileName);
        }

        static T Load<T>(string fileName) where T : Object {
            string[] guids = AssetDatabase.FindAssets($"{fileName} t:{typeof(T).Name}");
            for (int i = 0; i < guids.Length; i++) {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.IndexOf("/Editor/Ui/", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (!System.IO.Path.GetFileNameWithoutExtension(path).Equals(
                        fileName, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                return AssetDatabase.LoadAssetAtPath<T>(path);
            }

            Debug.LogError($"xNode UITK asset '{fileName}' was not found under Editor/Ui.");
            return null;
        }
    }
}
