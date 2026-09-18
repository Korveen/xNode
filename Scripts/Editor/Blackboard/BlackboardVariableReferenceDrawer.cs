using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace XNodeEditor {
    [CustomPropertyDrawer(typeof(XNode.BlackboardVariableReference))]
    internal sealed class BlackboardVariableReferenceDrawer : PropertyDrawer {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            EditorGUI.BeginProperty(position, label, property);
            try {
                XNode.Node node = property.serializedObject.targetObject as XNode.Node;
                Type valueType = GetExpectedValueType(node?.GetType());
                if (node == null || node.graph == null || valueType == null) {
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
                    if (variable == null || variable.ValueType != valueType) continue;
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
