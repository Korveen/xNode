using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace XNodeEditor {
    /// <summary> Loads node header icons named by <see cref="XNode.Node.NodeIconAttribute"/> from any NodeIcons folder. </summary>
    public static class NodeIcons {
        const string FolderToken = "/NodeIcons/";

        static Dictionary<string, Texture2D> textures;
        static Dictionary<Type, Texture2D> byType;

        public static Texture2D Get(Type nodeType) {
            if (nodeType == null) return null;
            if (byType == null) Cache();
            byType.TryGetValue(nodeType, out Texture2D texture);
            return texture;
        }

        static void Cache() {
            byType = new Dictionary<Type, Texture2D>();
            EnsureTextures();
            Type[] types = NodeEditorReflection.nodeTypes;
            for (int i = 0; i < types.Length; i++) {
                Type type = types[i];
                if (!type.TryGetNodeIcon(out string name)) continue;
                if (textures.TryGetValue(name, out Texture2D texture))
                    byType[type] = texture;
            }
        }

        static void EnsureTextures() {
            if (textures != null) return;
            textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
            string[] guids = AssetDatabase.FindAssets("t:Texture2D");
            for (int i = 0; i < guids.Length; i++) {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.IndexOf(FolderToken, StringComparison.Ordinal) < 0) continue;
                string name = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrEmpty(name) || textures.ContainsKey(name)) continue;
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture != null) textures.Add(name, texture);
            }
        }
    }
}
