using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace XNodeEditor {
    public enum NoodlePath { Curvy, Straight, Angled, ShaderLab }
    public enum NoodleStroke { Full, Dashed }

    public static class NodeEditorPreferences {
        public const string SharedKey = "xNode.Settings";
        public const string BehaviourTreeKey = "xNode.Settings.BehaviourTree";
        public const string FlowKey = "xNode.Settings.Flow";

        public static class Slots {
            public const string Exec = "exec";
            public const string Bool = "bool";
            public const string Float = "float";
            public const string Int = "int";
            public const string String = "string";
            public const string Data = "data";
            public const string HeaderStart = "header.start";
            public const string HeaderComposite = "header.composite";
            public const string HeaderDecorator = "header.decorator";
            public const string HeaderAction = "header.action";
            public const string HeaderCondition = "header.condition";
            public const string HeaderEntry = "header.entry";
            public const string HeaderControl = "header.control";
            public const string HeaderDialog = "header.dialog";
            public const string HeaderCommand = "header.command";
            public const string HeaderQuery = "header.query";
            public const string HeaderTerminal = "header.terminal";
        }

        public static event Action Changed;

        static SharedSettings _shared;
        static Texture2D _gridTexture;
        static Texture2D _crossTexture;
        static readonly Dictionary<string, GraphTypeSettings> GraphSettings = new Dictionary<string, GraphTypeSettings>();
        static readonly Dictionary<Type, GraphTypeSettings> DefaultCache = new Dictionary<Type, GraphTypeSettings>();

        [Serializable]
        public class SharedSettings {
            public bool gridSnap = true;
            public bool showNodeIndices = false;
            public bool autoSave = true;
            public bool openOnCreate = true;
            public bool dragToCreate = true;
            public bool createFilter = true;
            public bool zoomToMouse = true;
            public bool portTooltips = true;
            public float maxZoom = 5f;
            public float minZoom = 1f;
            public float compactNodeZoom = 1.5f;
            public Color32 tintColor = new Color32(90, 97, 105, 255);
            public Color32 selectionColor = new Color32(240, 192, 64, 255);
            public Color32 gridBgColor = new Color32(48, 48, 48, 255);
            public Color32 gridMinorColor = new Color32(255, 255, 255, 5);
            public Color32 gridMajorColor = new Color32(255, 255, 255, 13);
            public Color32 gridOriginColor = new Color32(255, 255, 255, 26);
            public float gridMinorStep = 20f;
            public float gridMajorStep = 100f;
        }

        [Serializable]
        public class GraphTypeSettings : ISerializationCallbackReceiver {
            public NoodlePath noodlePath = NoodlePath.Curvy;
            public float noodleThickness = 2f;
            public float noodleTension = 0.45f;
            public NoodleStroke noodleStroke = NoodleStroke.Full;
            [SerializeField] string slotColorsData = "";
            [NonSerialized] public Dictionary<string, Color> slots = new Dictionary<string, Color>();

            public Color GetSlot(string key, Color fallback) {
                if (slots != null && slots.TryGetValue(key, out Color color)) return color;
                return fallback;
            }

            public void SetSlot(string key, Color color) {
                if (slots == null) slots = new Dictionary<string, Color>();
                slots[key] = color;
            }

            public void MergeMissingFrom(GraphTypeSettings defaults) {
                if (defaults?.slots == null) return;
                if (slots == null) slots = new Dictionary<string, Color>();
                foreach (var pair in defaults.slots) {
                    if (!slots.ContainsKey(pair.Key)) slots.Add(pair.Key, pair.Value);
                }
            }

            public void OnAfterDeserialize() {
                slots = new Dictionary<string, Color>();
                if (string.IsNullOrEmpty(slotColorsData)) return;
                string[] data = slotColorsData.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i + 1 < data.Length; i += 2) {
                    if (ColorUtility.TryParseHtmlString("#" + data[i + 1], out Color col))
                        slots[data[i]] = col;
                }
            }

            public void OnBeforeSerialize() {
                slotColorsData = "";
                if (slots == null) return;
                foreach (var pair in slots)
                    slotColorsData += pair.Key + "," + ColorUtility.ToHtmlStringRGBA(pair.Value) + ",";
            }
        }

        [Serializable]
        class LegacySettings {
            public bool gridSnap = true;
            public bool showNodeIndices;
            public bool autoSave = true;
            public bool openOnCreate = true;
            public bool dragToCreate = true;
            public bool createFilter = true;
            public bool zoomToMouse = true;
            public bool portTooltips = true;
            public float maxZoom = 5f;
            public float minZoom = 1f;
            public float compactNodeZoom = 1.5f;
            public Color32 tintColor = new Color32(90, 97, 105, 255);
        }

        public static SharedSettings GetShared() {
            if (_shared == null) _shared = LoadShared();
            SanitizeShared(_shared);
            return _shared;
        }

        public static GraphTypeSettings GetFor(NodeGraphEditor editor) {
            return GetForKey(PrefsKey(editor), editor != null ? editor.GetType() : null);
        }

        public static GraphTypeSettings GetForKey(string key, Type editorType = null) {
            if (string.IsNullOrEmpty(key)) key = SharedKey;
            if (editorType == null) editorType = FindEditorType(key);
            if (GraphSettings.TryGetValue(key, out GraphTypeSettings cached)) {
                if (editorType != null)
                    cached.MergeMissingFrom(CreateDefaults(editorType));
                return cached;
            }
            GraphTypeSettings loaded = LoadGraph(key, editorType);
            GraphSettings[key] = loaded;
            return loaded;
        }

        public static SharedSettings GetSettings() => GetShared();

        public static Color GetTypeColor(Type type) {
            return GetSlotColor(GetFor(NodeEditorWindow.current?.graphEditor), type);
        }

        public static string DataSlotFor(Type type) {
            if (type == typeof(bool)) return Slots.Bool;
            if (type == typeof(float) || type == typeof(double)) return Slots.Float;
            if (type == typeof(int) || type == typeof(long)) return Slots.Int;
            if (type == typeof(string)) return Slots.String;
            return Slots.Data;
        }

        public static Color ExecFallback => new Color(0.92f, 0.78f, 0.28f, 1f);
        public static Color BoolFallback => new Color(0.40f, 0.82f, 0.45f, 1f);
        public static Color FloatFallback => new Color(0.30f, 0.75f, 0.85f, 1f);
        public static Color IntFallback => new Color(0.95f, 0.55f, 0.25f, 1f);
        public static Color StringFallback => new Color(0.85f, 0.45f, 0.75f, 1f);
        public static Color DataFallback => new Color(0.30f, 0.55f, 0.90f, 1f);

        public static void ApplyDefaultDataSlots(GraphTypeSettings settings) {
            if (settings == null) return;
            settings.SetSlot(Slots.Exec, ExecFallback);
            settings.SetSlot(Slots.Bool, BoolFallback);
            settings.SetSlot(Slots.Float, FloatFallback);
            settings.SetSlot(Slots.Int, IntFallback);
            settings.SetSlot(Slots.String, StringFallback);
            settings.SetSlot(Slots.Data, DataFallback);
        }

        public static Color GetSlotColor(GraphTypeSettings settings, Type type) {
            string slot = DataSlotFor(type);
            Color fallback = slot == Slots.Bool ? BoolFallback
                : slot == Slots.Float ? FloatFallback
                : slot == Slots.Int ? IntFallback
                : slot == Slots.String ? StringFallback
                : DataFallback;
            return settings != null ? settings.GetSlot(slot, fallback) : fallback;
        }

        public static Color ResolvePortColor(XNode.NodePort port, GraphTypeSettings settings, bool execution) {
            if (NodeEditorUtilities.TryGetGraphPortColor(port, out Color attributed)) return attributed;
            if (execution)
                return settings != null ? settings.GetSlot(Slots.Exec, ExecFallback) : ExecFallback;
            return GetSlotColor(settings, port != null ? port.ValueType : null);
        }

        public static Texture2D GetGridTexture() {
            if (_gridTexture == null) {
                SharedSettings shared = GetShared();
                _gridTexture = NodeEditorResources.GenerateGridTexture(shared.gridMinorColor, shared.gridBgColor);
            }
            return _gridTexture;
        }

        public static Texture2D GetCrossTexture() {
            if (_crossTexture == null)
                _crossTexture = NodeEditorResources.GenerateCrossTexture(GetShared().gridMajorColor);
            return _crossTexture;
        }

        public static void SetGridSnap(bool enabled) {
            GetShared().gridSnap = enabled;
            SaveShared();
            NotifyChanged();
        }

        public static void SetShowNodeIndices(bool enabled) {
            GetShared().showNodeIndices = enabled;
            SaveShared();
            NotifyChanged();
        }

        public static void SaveShared() {
            EditorPrefs.SetString(SharedKey + ".shared", JsonUtility.ToJson(GetShared()));
        }

        public static void SaveGraph(string key, GraphTypeSettings settings) {
            if (settings == null || string.IsNullOrEmpty(key)) return;
            settings.OnBeforeSerialize();
            GraphSettings[key] = settings;
            EditorPrefs.SetString(key, JsonUtility.ToJson(settings));
        }

        public static void ResetShared() {
            EditorPrefs.DeleteKey(SharedKey + ".shared");
            _shared = new SharedSettings();
            NotifyChanged();
        }

        public static void ResetGraph(string key, Type editorType) {
            if (!string.IsNullOrEmpty(key) && EditorPrefs.HasKey(key)) EditorPrefs.DeleteKey(key);
            GraphSettings.Remove(key);
            GetForKey(key, editorType);
            NotifyChanged();
        }

        static void NotifyChanged() {
            DestroyTexture(ref _gridTexture);
            DestroyTexture(ref _crossTexture);
            NodeEditorWindow.RepaintAll();
            Changed?.Invoke();
        }

        static void DestroyTexture(ref Texture2D texture) {
            if (texture == null) return;
            UnityEngine.Object.DestroyImmediate(texture);
            texture = null;
        }

        static void SanitizeShared(SharedSettings shared) {
            if (shared == null) return;
            if (shared.gridMinorStep < 1f) shared.gridMinorStep = 20f;
            if (shared.gridMajorStep < 1f) shared.gridMajorStep = 100f;
            shared.gridMajorStep = Mathf.Max(shared.gridMinorStep, shared.gridMajorStep);
        }

        static SharedSettings LoadShared() {
            string jsonKey = SharedKey + ".shared";
            if (EditorPrefs.HasKey(jsonKey))
                return JsonUtility.FromJson<SharedSettings>(EditorPrefs.GetString(jsonKey)) ?? new SharedSettings();
            if (EditorPrefs.HasKey(SharedKey)) {
                var legacy = JsonUtility.FromJson<LegacySettings>(EditorPrefs.GetString(SharedKey));
                if (legacy != null) {
                    return new SharedSettings {
                        gridSnap = legacy.gridSnap,
                        showNodeIndices = legacy.showNodeIndices,
                        autoSave = legacy.autoSave,
                        openOnCreate = legacy.openOnCreate,
                        dragToCreate = legacy.dragToCreate,
                        createFilter = legacy.createFilter,
                        zoomToMouse = legacy.zoomToMouse,
                        portTooltips = legacy.portTooltips,
                        maxZoom = legacy.maxZoom,
                        minZoom = legacy.minZoom,
                        compactNodeZoom = legacy.compactNodeZoom,
                        tintColor = legacy.tintColor
                    };
                }
            }
            return new SharedSettings();
        }

        static GraphTypeSettings LoadGraph(string key, Type editorType) {
            GraphTypeSettings defaults = CreateDefaults(editorType);
            if (string.IsNullOrEmpty(key) || !EditorPrefs.HasKey(key)) return defaults;
            GraphTypeSettings loaded = JsonUtility.FromJson<GraphTypeSettings>(EditorPrefs.GetString(key));
            if (loaded == null) return defaults;
            loaded.OnAfterDeserialize();
            loaded.MergeMissingFrom(defaults);
            if (loaded.noodleThickness <= 0f) loaded.noodleThickness = defaults.noodleThickness;
            if (loaded.noodleTension <= 0.01f) loaded.noodleTension = defaults.noodleTension > 0.01f ? defaults.noodleTension : 0.45f;
            return loaded;
        }

        static GraphTypeSettings CreateDefaults(Type editorType) {
            if (editorType == null) return new GraphTypeSettings();
            if (DefaultCache.TryGetValue(editorType, out GraphTypeSettings cached)) return cached;
            GraphTypeSettings created = new GraphTypeSettings();
            try {
                var editor = Activator.CreateInstance(editorType) as NodeGraphEditor;
                if (editor != null)
                    created = editor.GetDefaultPreferences() ?? new GraphTypeSettings();
            } catch {
                // ignored
            }
            DefaultCache[editorType] = created;
            return created;
        }

        public static string PrefsKey(NodeGraphEditor editor) {
            if (editor == null) return SharedKey;
            var attrib = editor.GetType().GetCustomAttribute<NodeGraphEditor.CustomNodeGraphEditorAttribute>(true);
            return attrib != null ? attrib.editorPrefsKey : SharedKey;
        }

        static Type FindEditorType(string key) {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (Type editorType in TypeCache.GetTypesWithAttribute<NodeGraphEditor.CustomNodeGraphEditorAttribute>()) {
                if (editorType.IsAbstract || !typeof(NodeGraphEditor).IsAssignableFrom(editorType)) continue;
                var attrib = editorType.GetCustomAttribute<NodeGraphEditor.CustomNodeGraphEditorAttribute>(true);
                if (attrib != null && attrib.editorPrefsKey == key) return editorType;
            }
            return null;
        }

        [SettingsProvider]
        static SettingsProvider CreateXNodeProvider() {
            return new SettingsProvider("Preferences/XNode", SettingsScope.User) {
                label = "XNode",
                keywords = new HashSet<string> { "xnode", "node", "graph", "grid", "snap" },
                guiHandler = _ => DrawSharedGUI()
            };
        }

        [SettingsProvider]
        static SettingsProvider CreateBehaviourTreeProvider() {
            return new SettingsProvider("Preferences/XNode/Behaviour Tree", SettingsScope.User) {
                label = "Behaviour Tree",
                keywords = new HashSet<string> { "xnode", "behaviour", "tree", "noodle", "port" },
                guiHandler = _ => DrawGraphTypeGUI(BehaviourTreeKey, "Behaviour Tree")
            };
        }

        [SettingsProvider]
        static SettingsProvider CreateFlowGraphProvider() {
            return new SettingsProvider("Preferences/XNode/Flow Graph", SettingsScope.User) {
                label = "Flow Graph",
                keywords = new HashSet<string> { "xnode", "flow", "noodle", "port" },
                guiHandler = _ => DrawGraphTypeGUI(FlowKey, "Flow Graph")
            };
        }

        static void DrawSharedGUI() {
            SharedSettings shared = GetShared();
            EditorGUI.BeginChangeCheck();
            DrawSection("Grid", () => {
                shared.gridSnap = EditorGUILayout.Toggle(new GUIContent("Snap", "Hold Ctrl while dragging to invert"), shared.gridSnap);
                shared.showNodeIndices = EditorGUILayout.Toggle(
                    new GUIContent("Node Indices", "Show compiled plan indices on node headers"),
                    shared.showNodeIndices);
                shared.gridMinorStep = EditorGUILayout.FloatField(
                    new GUIContent("Snap / Minor", "Node snap and minor grid lines"),
                    shared.gridMinorStep);
                shared.gridMajorStep = EditorGUILayout.FloatField("Major lines", shared.gridMajorStep);
                shared.gridBgColor = EditorGUILayout.ColorField("Background", shared.gridBgColor);
                shared.gridMinorColor = EditorGUILayout.ColorField("Minor lines", shared.gridMinorColor);
                shared.gridMajorColor = EditorGUILayout.ColorField("Major lines", shared.gridMajorColor);
                shared.gridOriginColor = EditorGUILayout.ColorField("Origin", shared.gridOriginColor);
            });
            DrawSection("View", () => {
                shared.selectionColor = EditorGUILayout.ColorField("Selection", shared.selectionColor);
                shared.zoomToMouse = EditorGUILayout.Toggle(
                    new GUIContent("Zoom to Mouse", "Keep the point under the cursor fixed while scrolling"),
                    shared.zoomToMouse);
                EditorGUILayout.LabelField("Zoom", EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                shared.minZoom = EditorGUILayout.FloatField("Min", shared.minZoom);
                shared.maxZoom = EditorGUILayout.FloatField("Max", shared.maxZoom);
                shared.compactNodeZoom = EditorGUILayout.FloatField(
                    new GUIContent("Hide Fields At", "When Scale is at least this value, node fields are not drawn. 0 disables."),
                    shared.compactNodeZoom);
                EditorGUI.indentLevel--;
            });
            DrawSection("Editor", () => {
                shared.autoSave = EditorGUILayout.Toggle("Autosave", shared.autoSave);
                shared.openOnCreate = EditorGUILayout.Toggle("Open Editor on Create", shared.openOnCreate);
                shared.dragToCreate = EditorGUILayout.Toggle(
                    new GUIContent("Drag to Create", "Drop an unconnected output on empty grid to create a node"),
                    shared.dragToCreate);
                shared.createFilter = EditorGUILayout.Toggle(
                    new GUIContent("Create Filter", "Only show nodes compatible with the dragged port"),
                    shared.createFilter);
                shared.portTooltips = EditorGUILayout.Toggle("Port Tooltips", shared.portTooltips);
            });
            if (EditorGUI.EndChangeCheck()) {
                shared.minZoom = Mathf.Max(0.1f, shared.minZoom);
                shared.maxZoom = Mathf.Max(shared.minZoom + 0.1f, shared.maxZoom);
                SanitizeShared(shared);
                SaveShared();
                NotifyChanged();
            }
            EditorGUILayout.Space(8);
            if (GUILayout.Button("Reset XNode Defaults", GUILayout.Width(180)))
                ResetShared();
        }

        static void DrawGraphTypeGUI(string key, string label) {
            Type editorType = FindEditorType(key);
            GraphTypeSettings settings = GetForKey(key, editorType);
            EditorGUILayout.HelpBox("Stored on this machine, not in the project.", MessageType.None);
            EditorGUI.BeginChangeCheck();
            DrawSection("Connections", () => {
                int path = settings.noodlePath == NoodlePath.Straight ? 1
                    : settings.noodlePath == NoodlePath.Angled ? 2 : 0;
                path = EditorGUILayout.Popup("Path", path, new[] { "Curvy", "Straight", "Angled" });
                settings.noodlePath = path == 1 ? NoodlePath.Straight
                    : path == 2 ? NoodlePath.Angled : NoodlePath.Curvy;
                settings.noodleThickness = EditorGUILayout.Slider("Thickness", settings.noodleThickness, 1f, 8f);
                settings.noodleTension = EditorGUILayout.Slider(
                    new GUIContent("Bezier", "How far Curvy wires pull away from a straight line"),
                    settings.noodleTension, 0.05f, 1f);
                settings.noodleStroke = (NoodleStroke)EditorGUILayout.EnumPopup("Stroke", settings.noodleStroke);
            });
            DrawSection("Ports", () => {
                DrawSlot(settings, Slots.Exec, "Execution");
                DrawSlotIfPresent(settings, Slots.Bool, "Bool");
                DrawSlotIfPresent(settings, Slots.Float, "Float");
                DrawSlotIfPresent(settings, Slots.Int, "Int");
                DrawSlotIfPresent(settings, Slots.String, "String");
                DrawSlotIfPresent(settings, Slots.Data, "Other data");
                EditorGUILayout.HelpBox(
                    "Custom types use Other data unless marked [Node.GraphPortColor]. Field attribute overrides the type.",
                    MessageType.None);
            });
            DrawSection("Headers", () => {
                DrawSlotIfPresent(settings, Slots.HeaderStart, "Start");
                DrawSlotIfPresent(settings, Slots.HeaderComposite, "Composite");
                DrawSlotIfPresent(settings, Slots.HeaderDecorator, "Decorator");
                DrawSlotIfPresent(settings, Slots.HeaderAction, "Action");
                DrawSlotIfPresent(settings, Slots.HeaderCondition, "Condition");
                DrawSlotIfPresent(settings, Slots.HeaderEntry, "Entry");
                DrawSlotIfPresent(settings, Slots.HeaderControl, "Control");
                DrawSlotIfPresent(settings, Slots.HeaderDialog, "Dialog");
                DrawSlotIfPresent(settings, Slots.HeaderCommand, "Command");
                DrawSlotIfPresent(settings, Slots.HeaderQuery, "Query");
                DrawSlotIfPresent(settings, Slots.HeaderTerminal, "Terminal");
            });
            if (EditorGUI.EndChangeCheck()) {
                SaveGraph(key, settings);
                NotifyChanged();
            }
            EditorGUILayout.Space(8);
            if (GUILayout.Button("Reset " + label + " Defaults", GUILayout.Width(220)))
                ResetGraph(key, editorType);
        }

        static void DrawSlotIfPresent(GraphTypeSettings settings, string key, string label) {
            if (settings.slots != null && settings.slots.ContainsKey(key))
                DrawSlot(settings, key, label);
        }

        static void DrawSlot(GraphTypeSettings settings, string key, string label) {
            Color current = settings.GetSlot(key, Color.gray);
            Color next = EditorGUILayout.ColorField(label, current);
            if (next != current) settings.SetSlot(key, next);
        }

        static void DrawSection(string title, Action body) {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            body();
            EditorGUILayout.EndVertical();
        }
    }
}
