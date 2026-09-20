using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace XNodeEditor {
    [CustomPropertyDrawer(typeof(XNode.BlackboardVariableReference))]
    internal sealed class BlackboardVariableReferenceDrawer : PropertyDrawer {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            SerializedProperty idProperty = property.FindPropertyRelative("variableId");
            XNode.Node node = property.serializedObject.targetObject as XNode.Node;
            Type valueType = GetExpectedValueType(node?.GetType());
            var choices = new List<string> { "<None>" };
            var ids = new List<string> { string.Empty };
            int selected = 0;
            if (node != null && node.graph != null) {
                IReadOnlyList<XNode.BlackboardVariable> variables = node.graph.BlackboardDefinition.Variables;
                for (int i = 0; i < variables.Count; i++) {
                    XNode.BlackboardVariable variable = variables[i];
                    if (variable == null) continue;
                    if (valueType != null && variable.ValueType != valueType) continue;
                    ids.Add(variable.Id);
                    choices.Add(variable.Name);
                    if (variable.Id == idProperty.stringValue) selected = ids.Count - 1;
                }
            }

            if (selected == 0 && !string.IsNullOrEmpty(idProperty.stringValue)) {
                choices.Add("(missing)");
                ids.Add(idProperty.stringValue);
                selected = choices.Count - 1;
            }

            var dropdown = new DropdownField(property.displayName, choices, selected);
            dropdown.style.minWidth = 0;
            dropdown.style.flexGrow = 1;
            dropdown.RegisterValueChangedCallback(evt => {
                int index = choices.IndexOf(evt.newValue);
                idProperty.stringValue = index >= 0 && index < ids.Count ? ids[index] : string.Empty;
                property.serializedObject.ApplyModifiedProperties();
            });
            return dropdown;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            EditorGUI.BeginProperty(position, label, property);
            try {
                XNode.Node node = property.serializedObject.targetObject as XNode.Node;
                Type valueType = GetExpectedValueType(node?.GetType());
                if (node == null || node.graph == null) {
                    EditorGUI.PropertyField(position, property, label, true);
                    return;
                }

                SerializedProperty idProperty = property.FindPropertyRelative("variableId");
                IReadOnlyList<XNode.BlackboardVariable> variables =
                    node.graph.BlackboardDefinition.Variables;
                var labels = new List<GUIContent> { new GUIContent("<None>") };
                var ids = new List<string> { string.Empty };
                int selected = 0;

                for (int i = 0; i < variables.Count; i++) {
                    XNode.BlackboardVariable variable = variables[i];
                    if (variable == null) continue;
                    if (valueType != null && variable.ValueType != valueType) continue;
                    ids.Add(variable.Id);
                    labels.Add(new GUIContent(variable.Name));
                    if (variable.Id == idProperty.stringValue) selected = ids.Count - 1;
                }

                int next = EditorGUI.Popup(position, label, selected, labels.ToArray());
                if (next != selected) idProperty.stringValue = ids[next];
            } finally {
                EditorGUI.EndProperty();
            }
        }

        private static Type GetExpectedValueType(Type nodeType) {
            Type current = nodeType;
            while (current != null) {
                if (current.IsGenericType) {
                    Type definition = current.GetGenericTypeDefinition();
                    if (definition == typeof(XNode.GetBlackboardVariableNode<>)) {
                        return current.GetGenericArguments()[0];
                    }
                    FieldInfo referenceField = current.GetField(
                        "Variable",
                        BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (referenceField?.FieldType ==
                        typeof(XNode.BlackboardVariableReference)) {
                        return current.GetGenericArguments()[0];
                    }
                }
                current = current.BaseType;
            }
            return null;
        }
    }
}
