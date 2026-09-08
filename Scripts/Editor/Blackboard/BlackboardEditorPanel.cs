using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace XNodeEditor {
    internal static class BlackboardEditorPanel {
        private const float ResizeHandleWidth = 5f;
        private static Vector2 scroll;
        private static NodeEditorWindow resizingWindow;
        private static SerializedObject cachedSerializedGraph;
        private static SerializedProperty cachedVariables;
        private static ReorderableList cachedList;
        private static XNode.NodeGraph cachedGraph;
        private static bool cachedAllowStructuralEdits;

        private static readonly Type[] VariableTypes = {
            typeof(XNode.BoolBlackboardVariable),
            typeof(XNode.IntBlackboardVariable),
            typeof(XNode.FloatBlackboardVariable),
            typeof(XNode.StringBlackboardVariable),
            typeof(XNode.Vector2BlackboardVariable),
            typeof(XNode.Vector3BlackboardVariable),
            typeof(XNode.QuaternionBlackboardVariable),
            typeof(XNode.ColorBlackboardVariable),
            typeof(XNode.ObjectBlackboardVariable)
        };

        private static readonly Dictionary<Type, Type> GetterTypes = new Dictionary<Type, Type> {
            { typeof(bool), typeof(XNode.GetBoolBlackboardVariableNode) },
            { typeof(int), typeof(XNode.GetIntBlackboardVariableNode) },
            { typeof(float), typeof(XNode.GetFloatBlackboardVariableNode) },
            { typeof(string), typeof(XNode.GetStringBlackboardVariableNode) },
            { typeof(Vector2), typeof(XNode.GetVector2BlackboardVariableNode) },
            { typeof(Vector3), typeof(XNode.GetVector3BlackboardVariableNode) },
            { typeof(Quaternion), typeof(XNode.GetQuaternionBlackboardVariableNode) },
            { typeof(Color), typeof(XNode.GetColorBlackboardVariableNode) },
            { typeof(UnityEngine.Object), typeof(XNode.GetObjectBlackboardVariableNode) }
        };

        public static void Draw(NodeEditorWindow window, float originY = 0f) {
            Rect rect = window.GetBlackboardRect(originY);
            DrawResizeHandle(window, rect);
            EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.16f, 1f));
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            GUILayout.BeginArea(new Rect(rect.x + 7f, rect.y + 4f, rect.width - 11f, rect.height - 8f));
            try {
                DrawContents(window);
            } finally {
                GUILayout.EndArea();
            }
        }

        private static void DrawContents(NodeEditorWindow window) {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Blackboard", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(Application.isPlaying)) {
                if (GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(22f))) {
                    ShowAddMenu(window, new Rect(Event.current.mousePosition, Vector2.zero));
                }
            }
            GUILayout.EndHorizontal();

            if (!EnsureList(window)) {
                EditorGUILayout.HelpBox("Blackboard data could not be serialized.", MessageType.Error);
                return;
            }

            cachedSerializedGraph.Update();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            cachedList.DoLayoutList();
            EditorGUILayout.EndScrollView();

            if (cachedSerializedGraph.ApplyModifiedProperties()) {
                EditorUtility.SetDirty(window.graph);
                window.Repaint();
            }

            DrawDuplicateNameWarning(window.graph.BlackboardDefinition.Variables);
        }

        private static bool EnsureList(NodeEditorWindow window) {
            bool allowStructuralEdits = !Application.isPlaying;
            if (cachedList != null &&
                cachedGraph == window.graph &&
                cachedSerializedGraph != null &&
                cachedAllowStructuralEdits == allowStructuralEdits) {
                return cachedVariables != null;
            }

            cachedSerializedGraph?.Dispose();
            cachedGraph = window.graph;
            cachedAllowStructuralEdits = allowStructuralEdits;
            cachedSerializedGraph = new SerializedObject(window.graph);
            cachedVariables = cachedSerializedGraph.FindProperty("blackboardDefinition")
                ?.FindPropertyRelative("variables");
            if (cachedVariables == null) {
                cachedList = null;
                return false;
            }

            cachedList = new ReorderableList(
                cachedSerializedGraph,
                cachedVariables,
                allowStructuralEdits,
                false,
                false,
                allowStructuralEdits) {
                elementHeightCallback = index => {
                    if (index < 0 || index >= cachedVariables.arraySize) {
                        return EditorGUIUtility.singleLineHeight;
                    }

                    SerializedProperty variableProperty = cachedVariables.GetArrayElementAtIndex(index);
                    SerializedProperty defaultValue = variableProperty.FindPropertyRelative("defaultValue");
                    float height = EditorGUIUtility.singleLineHeight + 8f;
                    if (defaultValue != null) {
                        height += EditorGUI.GetPropertyHeight(defaultValue, true) + 2f;
                    }
                    return height;
                },
                drawElementCallback = (rect, index, _, _) => {
                    if (index < 0 || index >= cachedVariables.arraySize) return;
                    DrawVariable(window, rect, cachedVariables.GetArrayElementAtIndex(index));
                },
                onAddDropdownCallback = (rect, _) => ShowAddMenu(window, rect),
                onReorderCallback = _ => {
                    EditorUtility.SetDirty(window.graph);
                    window.Repaint();
                }
            };
            return true;
        }

        private static void DrawVariable(
            NodeEditorWindow window,
            Rect rect,
            SerializedProperty variableProperty) {
            var variable = variableProperty.managedReferenceValue as XNode.BlackboardVariable;
            if (variable == null) return;

            float line = EditorGUIUtility.singleLineHeight;
            float y = rect.y + 2f;
            bool highlighted = window.HighlightedVariableId == variable.Id;
            if (highlighted) {
                EditorGUI.DrawRect(rect, new Color(0.22f, 0.42f, 0.72f, 0.28f));
            }

            var nameRect = new Rect(rect.x, y, Mathf.Max(36f, rect.width - 114f), line);
            var typeRect = new Rect(nameRect.xMax + 4f, y, 72f, line);
            var getRect = new Rect(typeRect.xMax + 4f, y, 34f, line);

            using (new EditorGUI.DisabledScope(Application.isPlaying)) {
                EditorGUI.PropertyField(
                    nameRect,
                    variableProperty.FindPropertyRelative("name"),
                    GUIContent.none);
            }

            EditorGUI.LabelField(typeRect, variable.ValueType.PrettyName(), EditorStyles.miniLabel);
            if (GUI.Button(getRect, "Get")) CreateGetter(window, variable);

            SerializedProperty defaultValue = variableProperty.FindPropertyRelative("defaultValue");
            if (defaultValue == null) return;

            y += line + 2f;
            EditorGUI.PropertyField(
                new Rect(rect.x, y, rect.width, EditorGUI.GetPropertyHeight(defaultValue, true)),
                defaultValue,
                new GUIContent("Default"),
                true);
        }

        private static void ShowAddMenu(NodeEditorWindow window, Rect rect) {
            GenericMenu menu = new GenericMenu();
            for (int i = 0; i < VariableTypes.Length; i++) {
                Type type = VariableTypes[i];
                string label = type.BaseType?.GetGenericArguments()[0].PrettyName() ?? type.Name;
                menu.AddItem(new GUIContent(label), false, () => AddVariable(window, type, label));
            }
            menu.DropDown(rect);
        }

        private static void AddVariable(NodeEditorWindow window, Type variableType, string label) {
            if (!EnsureList(window)) return;

            Undo.RecordObject(window.graph, "Add Blackboard Variable");
            cachedSerializedGraph.Update();
            int index = cachedVariables.arraySize;
            cachedVariables.InsertArrayElementAtIndex(index);
            SerializedProperty element = cachedVariables.GetArrayElementAtIndex(index);
            element.managedReferenceValue = Activator.CreateInstance(variableType);
            cachedSerializedGraph.ApplyModifiedProperties();

            XNode.BlackboardVariable variable = window.graph.BlackboardDefinition.Variables[index];
            window.graph.BlackboardDefinition.Rename(
                variable.Id,
                window.graph.BlackboardDefinition.GetUniqueName(label));
            EditorUtility.SetDirty(window.graph);
            window.Repaint();
        }

        private static void CreateGetter(NodeEditorWindow window, XNode.BlackboardVariable variable) {
            if (!GetterTypes.TryGetValue(variable.ValueType, out Type getterType)) {
                Debug.LogWarning($"No Blackboard getter node is registered for {variable.ValueType}.");
                return;
            }

            Vector2 windowPosition = new Vector2(
                Mathf.Max(0f, window.position.width - window.BlackboardWidth) * 0.5f,
                window.position.height * 0.5f);
            XNode.Node node = window.graphEditor.CreateNode(
                getterType,
                window.WindowToGridPosition(windowPosition));

            Type genericBase = node.GetType().BaseType;
            var variableProperty = genericBase?.GetProperty("Variable");
            variableProperty?.SetValue(
                node,
                new XNode.BlackboardVariableReference(variable.Id));
            EditorUtility.SetDirty(node);
        }

        private static void DrawDuplicateNameWarning(
            IReadOnlyList<XNode.BlackboardVariable> variables) {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < variables.Count; i++) {
                XNode.BlackboardVariable variable = variables[i];
                if (variable != null && !names.Add(variable.Name)) {
                    EditorGUILayout.HelpBox(
                        $"Variable name '{variable.Name}' is duplicated.",
                        MessageType.Error);
                    return;
                }
            }
        }

        private static void DrawResizeHandle(NodeEditorWindow window, Rect panelRect) {
            Rect handle = new Rect(
                panelRect.x - ResizeHandleWidth * 0.5f,
                panelRect.y,
                ResizeHandleWidth,
                panelRect.height);
            EditorGUIUtility.AddCursorRect(handle, MouseCursor.ResizeHorizontal);

            Event current = Event.current;
            if (current.type == EventType.MouseDown && handle.Contains(current.mousePosition)) {
                resizingWindow = window;
                current.Use();
            } else if (current.type == EventType.MouseDrag && resizingWindow == window) {
                window.BlackboardWidth = window.position.width - current.mousePosition.x;
                current.Use();
            } else if (current.type == EventType.MouseUp && resizingWindow == window) {
                resizingWindow = null;
                current.Use();
            }
        }
    }
}
