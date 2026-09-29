using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine.UIElements;

namespace XNodeEditor
{
    [CustomPropertyDrawer(typeof(XNode.BlackboardVariableReference))]
    [CustomPropertyDrawer(typeof(XNode.BlackboardVariableReference<>))]
    internal sealed class BlackboardVariableReferenceDrawer: PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var idProperty = property.FindPropertyRelative("variableId");
            var node = property.serializedObject.targetObject as XNode.Node;
            var valueType = GetExpectedValueType(node?.GetType());
            var choices = new List<string> {"<None>"};
            var ids = new List<string> {string.Empty};
            var selected = 0;

            if (node != null && node.graph != null)
            {
                var variables = node.graph.BlackboardDefinition.Variables;
                for (var i = 0; i < variables.Count; i++)
                {
                    var variable = variables[i];
                    if (variable == null)
                    {
                        continue;
                    }

                    if (valueType != null && variable.ValueType != valueType)
                    {
                        continue;
                    }

                    ids.Add(variable.Id);
                    choices.Add(variable.Name);

                    if (variable.Id == idProperty.stringValue)
                    {
                        selected = ids.Count - 1;
                    }
                }
            }

            if (selected == 0 && !string.IsNullOrEmpty(idProperty.stringValue))
            {
                choices.Add("(missing)");
                ids.Add(idProperty.stringValue);
                selected = choices.Count - 1;
            }

            var dropdown = new DropdownField(property.displayName, choices, selected)
            {
                style =
                {
                    minWidth = 0,
                    flexGrow = 1
                }
            };
            dropdown.RegisterValueChangedCallback(evt =>
            {
                var index = choices.IndexOf(evt.newValue);
                idProperty.stringValue = index >= 0 && index < ids.Count ? ids[index] : string.Empty;
                property.serializedObject.ApplyModifiedProperties();
            });
            return dropdown;
        }

        private Type GetExpectedValueType(Type nodeType)
        {
            var fieldType = fieldInfo?.FieldType;
            if (fieldType != null &&
                fieldType.IsGenericType &&
                fieldType.GetGenericTypeDefinition() ==
                typeof(XNode.BlackboardVariableReference<>))
            {
                return fieldType.GetGenericArguments()[0];
            }

            return GetExpectedValueTypeFromNode(nodeType);
        }

        private static Type GetExpectedValueTypeFromNode(Type nodeType)
        {
            var current = nodeType;
            while (current != null)
            {
                if (current.IsGenericType)
                {
                    var definition = current.GetGenericTypeDefinition();
                    if (definition == typeof(XNode.GetBlackboardVariableNode<>))
                    {
                        return current.GetGenericArguments()[0];
                    }

                    var referenceField = current.GetField(
                        "Variable",
                        BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (referenceField?.FieldType ==
                        typeof(XNode.BlackboardVariableReference))
                    {
                        return current.GetGenericArguments()[0];
                    }
                }

                current = current.BaseType;
            }

            return null;
        }
    }
}