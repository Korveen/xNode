using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace XNodeEditor {
    public enum NoodlePath { Curvy, Straight, Angled, ShaderLab }
    public enum NoodleStroke { Full, Dashed }

    public static class NodeEditorPreferences {

        /// <summary> The last editor we checked. This should be the one we modify </summary>
        private static XNodeEditor.NodeGraphEditor lastEditor;
        /// <summary> The last key we checked. This should be the one we modify </summary>
        private static string lastKey = "xNode.Settings";

        private static Dictionary<Type, Color> typeColors = new Dictionary<Type, Color>();
        private static Dictionary<string, Settings> settings = new Dictionary<string, Settings>();

        [System.Serializable]
        public class Settings : ISerializationCallbackReceiver {
            [SerializeField] private Color32 _gridLineColor = new Color(.23f, .23f, .23f);
            public Color32 gridLineColor { get { return _gridLineColor; } set { _gridLineColor = value; _gridTexture = null; _crossTexture = null; } }

            [SerializeField] private Color32 _gridBgColor = new Color(.19f, .19f, .19f);
            public Color32 gridBgColor { get { return _gridBgColor; } set { _gridBgColor = value; _gridTexture = null; } }

            [Obsolete("Use maxZoom instead")]
            public float zoomOutLimit { get { return maxZoom; } set { maxZoom = value; } }

            [UnityEngine.Serialization.FormerlySerializedAs("zoomOutLimit")]
            public float maxZoom = 5f;
            public float minZoom = 1f;
            public float compactNodeZoom = 1.5f;
            public Color32 tintColor = new Color32(90, 97, 105, 255);
            public Color32 highlightColor = new Color32(255, 255, 255, 255);
            public bool gridSnap = true;
            public bool autoSave = true;
            public bool openOnCreate = true;
            public bool dragToCreate = true;
            public bool createFilter = true;
            public bool zoomToMouse = true;
            public bool portTooltips = true;
            [SerializeField] private string typeColorsData = "";
            [NonSerialized] public Dictionary<string, Color> typeColors = new Dictionary<string, Color>();
            [FormerlySerializedAs("noodleType")] public NoodlePath noodlePath = NoodlePath.Curvy;
            public float noodleThickness = 2f;

            public NoodleStroke noodleStroke = NoodleStroke.Full;

            private Texture2D _gridTexture;
            public Texture2D gridTexture {
                get {
                    if (_gridTexture == null) _gridTexture = NodeEditorResources.GenerateGridTexture(gridLineColor, gridBgColor);
                    return _gridTexture;
                }
            }
            private Texture2D _crossTexture;
            public Texture2D crossTexture {
                get {
                    if (_crossTexture == null) _crossTexture = NodeEditorResources.GenerateCrossTexture(gridLineColor);
                    return _crossTexture;
                }
            }

            public void OnAfterDeserialize() {
                typeColors = new Dictionary<string, Color>();
                string[] data = typeColorsData.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < data.Length; i += 2) {
                    Color col;
                    if (ColorUtility.TryParseHtmlString("#" + data[i + 1], out col)) {
                        typeColors.Add(data[i], col);
                    }
                }
            }

            public void OnBeforeSerialize() {
                typeColorsData = "";
                foreach (var item in typeColors) {
                    typeColorsData += item.Key + "," + ColorUtility.ToHtmlStringRGB(item.Value) + ",";
                }
            }
        }

        /// <summary> Get settings of current active editor </summary>
        public static Settings GetSettings() {
            PreferenceGroup group = GetActivePreferenceGroup();
            VerifyLoaded(group);
            lastKey = group.key;
            return settings[group.key];
        }

        private static PreferenceGroup GetActivePreferenceGroup() {
            if (NodeEditorWindow.current != null && NodeEditorWindow.current.graphEditor != null) {
                Type editorType = NodeEditorWindow.current.graphEditor.GetType();
                object[] attribs = editorType.GetCustomAttributes(typeof(NodeGraphEditor.CustomNodeGraphEditorAttribute), true);
                if (attribs.Length == 1) {
                    NodeGraphEditor.CustomNodeGraphEditorAttribute attrib = attribs[0] as NodeGraphEditor.CustomNodeGraphEditorAttribute;
                    lastEditor = NodeEditorWindow.current.graphEditor;
                    return new PreferenceGroup(attrib.editorPrefsKey, PrefsLabel(attrib), editorType);
                }
            }

            PreferenceGroup[] groups = GetPreferenceGroups();
            return groups.Length > 0 ? groups[0] : PreferenceGroup.Default;
        }

#if UNITY_2019_1_OR_NEWER
        [SettingsProvider]
        public static SettingsProvider CreateXNodeSettingsProvider() {
            SettingsProvider provider = new SettingsProvider("Preferences/Node Editor", SettingsScope.User) {
                guiHandler = (searchContext) => { PreferencesGUI(); },
                keywords = new HashSet<string>(new [] { "xNode", "node", "editor", "graph", "connections", "noodles", "ports" })
            };
            return provider;
        }
#endif

#if !UNITY_2019_1_OR_NEWER
        [PreferenceItem("Node Editor")]
#endif
        private static void PreferencesGUI() {
            PreferenceGroup[] groups = GetPreferenceGroups();

            if (GUILayout.Button(new GUIContent("Documentation", "https://github.com/Siccity/xNode/wiki"), GUILayout.Width(100))) {
                Application.OpenURL("https://github.com/Siccity/xNode/wiki");
            }

            DrawSection("Connections", () => {
                if (groups.Length > 1) {
                    EditorGUILayout.HelpBox("Path, thickness and stroke are stored per graph type.", MessageType.None);
                }
                for (int i = 0; i < groups.Length; i++) {
                    DrawConnectionBlock(groups[i], true);
                }
            });

            Settings shared = GetSharedSettings();
            DrawSection("Appearance", () => DrawAppearanceGUI(shared, groups));
            DrawSection("Grid", () => DrawGridGUI(shared, groups));
            DrawSection("Editor", () => DrawEditorGUI(shared, groups));

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Reset All To Default", GUILayout.Width(160))) {
                ResetPrefs();
            }
        }

        private static void DrawSection(string title, Action body) {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            body();
            EditorGUILayout.EndVertical();
        }

        private static void DrawConnectionBlock(PreferenceGroup group, bool showLabel) {
            VerifyLoaded(group);
            Settings groupSettings = settings[group.key];

            if (showLabel) {
                EditorGUILayout.LabelField(group.label, EditorStyles.miniBoldLabel);
            }

            EditorGUI.BeginChangeCheck();
            groupSettings.noodlePath = (NoodlePath) EditorGUILayout.EnumPopup("Path", groupSettings.noodlePath);
            groupSettings.noodleThickness = EditorGUILayout.Slider("Thickness", groupSettings.noodleThickness, 1f, 8f);
            groupSettings.noodleStroke = (NoodleStroke) EditorGUILayout.EnumPopup("Stroke", groupSettings.noodleStroke);
            if (EditorGUI.EndChangeCheck()) {
                SavePrefs(group.key, groupSettings);
                NodeEditorWindow.RepaintAll();
            }

            if (showLabel) EditorGUILayout.Space(4);
        }

        private static void DrawAppearanceGUI(Settings shared, PreferenceGroup[] groups) {
            EditorGUI.BeginChangeCheck();
            Color32 tint = EditorGUILayout.ColorField("Tint", shared.tintColor);
            Color32 highlight = EditorGUILayout.ColorField("Selection", shared.highlightColor);
            if (EditorGUI.EndChangeCheck()) {
                ApplyShared(groups, s => {
                    s.tintColor = tint;
                    s.highlightColor = highlight;
                });
            }

            if (typeColors.Count == 0) return;
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Port Types", EditorStyles.miniBoldLabel);
            List<Type> typeColorKeys = new List<Type>(typeColors.Keys);
            for (int i = 0; i < typeColorKeys.Count; i++) {
                Type type = typeColorKeys[i];
                string typeColorKey = NodeEditorUtilities.PrettyName(type);
                Color col = typeColors[type];
                EditorGUI.BeginChangeCheck();
                col = EditorGUILayout.ColorField(typeColorKey, col);
                if (EditorGUI.EndChangeCheck()) {
                    typeColors[type] = col;
                    ApplyShared(groups, s => {
                        if (s.typeColors.ContainsKey(typeColorKey)) s.typeColors[typeColorKey] = col;
                        else s.typeColors.Add(typeColorKey, col);
                    });
                }
            }
        }

        private static void DrawGridGUI(Settings shared, PreferenceGroup[] groups) {
            EditorGUI.BeginChangeCheck();
            bool gridSnap = EditorGUILayout.Toggle(new GUIContent("Snap", "Hold CTRL in editor to invert"), shared.gridSnap);
            bool zoomToMouse = EditorGUILayout.Toggle(new GUIContent("Zoom to Mouse"), shared.zoomToMouse);
            EditorGUILayout.LabelField("Zoom", EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;
            float maxZoom = EditorGUILayout.FloatField("Max", shared.maxZoom);
            float minZoom = EditorGUILayout.FloatField("Min", shared.minZoom);
            float compactNodeZoom = EditorGUILayout.FloatField(
                new GUIContent("Hide Fields At", "When Scale is at least this value, node fields are not drawn. 0 disables."),
                shared.compactNodeZoom);
            EditorGUI.indentLevel--;
            Color32 gridLineColor = EditorGUILayout.ColorField("Line", shared.gridLineColor);
            Color32 gridBgColor = EditorGUILayout.ColorField("Background", shared.gridBgColor);
            if (EditorGUI.EndChangeCheck()) {
                ApplyShared(groups, s => {
                    s.gridSnap = gridSnap;
                    s.zoomToMouse = zoomToMouse;
                    s.maxZoom = maxZoom;
                    s.minZoom = minZoom;
                    s.compactNodeZoom = compactNodeZoom;
                    s.gridLineColor = gridLineColor;
                    s.gridBgColor = gridBgColor;
                });
            }
        }

        private static void DrawEditorGUI(Settings shared, PreferenceGroup[] groups) {
            EditorGUI.BeginChangeCheck();
            bool autoSave = EditorGUILayout.Toggle(new GUIContent("Autosave"), shared.autoSave);
            bool openOnCreate = EditorGUILayout.Toggle(new GUIContent("Open Editor on Create"), shared.openOnCreate);
            bool portTooltips = EditorGUILayout.Toggle("Port Tooltips", shared.portTooltips);
            bool dragToCreate = EditorGUILayout.Toggle(new GUIContent("Drag to Create", "Drag a port onto empty grid to create a node"), shared.dragToCreate);
            bool createFilter = EditorGUILayout.Toggle(new GUIContent("Create Filter", "Only show nodes compatible with the dragged port"), shared.createFilter);
            if (EditorGUI.EndChangeCheck()) {
                ApplyShared(groups, s => {
                    s.autoSave = autoSave;
                    s.openOnCreate = openOnCreate;
                    s.portTooltips = portTooltips;
                    s.dragToCreate = dragToCreate;
                    s.createFilter = createFilter;
                });
            }
        }

        private static Settings GetSharedSettings() {
            PreferenceGroup[] groups = GetPreferenceGroups();
            VerifyLoaded(groups[0]);
            return settings[groups[0].key];
        }

        private static void ApplyShared(PreferenceGroup[] groups, Action<Settings> mutate) {
            for (int i = 0; i < groups.Length; i++) {
                VerifyLoaded(groups[i]);
                Settings groupSettings = settings[groups[i].key];
                mutate(groupSettings);
                SavePrefs(groups[i].key, groupSettings);
            }
            NodeEditorWindow.RepaintAll();
        }

        private static Settings LoadPrefs(PreferenceGroup group) {
            if (!EditorPrefs.HasKey(group.key)) {
                if (group.key != "xNode.Settings" && EditorPrefs.HasKey("xNode.Settings")) {
                    EditorPrefs.SetString(group.key, EditorPrefs.GetString("xNode.Settings"));
                } else {
                    EditorPrefs.SetString(group.key, JsonUtility.ToJson(group.CreateDefaults()));
                }
            }
            return JsonUtility.FromJson<Settings>(EditorPrefs.GetString(group.key));
        }

        public static void ResetPrefs() {
            PreferenceGroup[] groups = GetPreferenceGroups();
            for (int i = 0; i < groups.Length; i++) ResetPrefs(groups[i]);
            typeColors = new Dictionary<Type, Color>();
            NodeEditorWindow.RepaintAll();
        }

        private static void ResetPrefs(PreferenceGroup group) {
            if (EditorPrefs.HasKey(group.key)) EditorPrefs.DeleteKey(group.key);
            if (settings.ContainsKey(group.key)) settings.Remove(group.key);
            VerifyLoaded(group);
            NodeEditorWindow.RepaintAll();
        }

        private static void SavePrefs(string key, Settings settings) {
            EditorPrefs.SetString(key, JsonUtility.ToJson(settings));
        }

        private static void VerifyLoaded() {
            VerifyLoaded(GetActivePreferenceGroup());
        }

        private static void VerifyLoaded(PreferenceGroup group) {
            lastKey = group.key;
            if (!settings.ContainsKey(group.key)) settings.Add(group.key, LoadPrefs(group));
        }

        private static string PrefsLabel(NodeGraphEditor.CustomNodeGraphEditorAttribute attrib) {
            string typeName = attrib.GetInspectedType().Name;
            if (typeName.EndsWith("Graph", StringComparison.Ordinal)) {
                typeName = typeName.Substring(0, typeName.Length - 5);
            }
            return ObjectNames.NicifyVariableName(typeName);
        }

        private static PreferenceGroup[] GetPreferenceGroups() {
            List<PreferenceGroup> groups = new List<PreferenceGroup>();
            HashSet<string> seen = new HashSet<string>();
            CollectGroups(TypeCache.GetTypesWithAttribute<NodeGraphEditor.CustomNodeGraphEditorAttribute>(), groups, seen);
            if (groups.Count < 2) {
                CollectGroups(TypeCache.GetTypesDerivedFrom<NodeGraphEditor>(), groups, seen);
            }

            if (groups.Count == 0) return new[] { PreferenceGroup.Default };
            groups.Sort((a, b) => string.CompareOrdinal(a.label, b.label));
            return groups.ToArray();
        }

        private static void CollectGroups(
            TypeCache.TypeCollection types,
            List<PreferenceGroup> groups,
            HashSet<string> seen)
        {
            foreach (Type editorType in types) {
                if (editorType.IsAbstract) continue;
                if (!typeof(NodeGraphEditor).IsAssignableFrom(editorType)) continue;
                NodeGraphEditor.CustomNodeGraphEditorAttribute attrib =
                    editorType.GetCustomAttribute<NodeGraphEditor.CustomNodeGraphEditorAttribute>(false) ??
                    editorType.GetCustomAttribute<NodeGraphEditor.CustomNodeGraphEditorAttribute>(true);
                if (attrib == null) continue;
                if (attrib.GetInspectedType() == typeof(XNode.NodeGraph)) continue;
                if (!seen.Add(attrib.editorPrefsKey)) continue;
                groups.Add(new PreferenceGroup(attrib.editorPrefsKey, PrefsLabel(attrib), editorType));
            }
        }

        private readonly struct PreferenceGroup {
            public readonly string key;
            public readonly string label;
            public readonly Type editorType;

            public static PreferenceGroup Default =>
                new PreferenceGroup("xNode.Settings", "Node Editor", null);

            public PreferenceGroup(string key, string label, Type editorType) {
                this.key = key;
                this.label = label;
                this.editorType = editorType;
            }

            public Settings CreateDefaults() {
                if (editorType == null) return new Settings();
                try {
                    NodeGraphEditor editor = Activator.CreateInstance(editorType) as NodeGraphEditor;
                    if (editor != null) return editor.GetDefaultPreferences() ?? new Settings();
                } catch {
                    // ignored
                }
                return new Settings();
            }
        }

        public static Color GetTypeColor(System.Type type) {
            VerifyLoaded();
            if (type == null) return Color.gray;
            Color col;
            if (!typeColors.TryGetValue(type, out col)) {
                string typeName = type.PrettyName();
                if (settings[lastKey].typeColors.ContainsKey(typeName)) typeColors.Add(type, settings[lastKey].typeColors[typeName]);
                else {
#if UNITY_5_4_OR_NEWER
                    UnityEngine.Random.State oldState = UnityEngine.Random.state;
                    UnityEngine.Random.InitState(typeName.GetHashCode());
#else
                    int oldSeed = UnityEngine.Random.seed;
                    UnityEngine.Random.seed = typeName.GetHashCode();
#endif
                    col = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
                    typeColors.Add(type, col);
#if UNITY_5_4_OR_NEWER
                    UnityEngine.Random.state = oldState;
#else
                    UnityEngine.Random.seed = oldSeed;
#endif
                }
            }
            return col;
        }
    }
}
