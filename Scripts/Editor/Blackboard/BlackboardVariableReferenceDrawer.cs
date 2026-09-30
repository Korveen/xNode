using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace XNodeEditor
{
    [CustomPropertyDrawer(typeof(XNode.BlackboardVariableReference<>))]
    internal sealed class BlackboardVariableReferenceDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var idProperty = property.FindPropertyRelative("variableId");
            var choices = new List<string>();
            var ids = new List<string>();
            var selected = CollectChoices(property, choices, ids);
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

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var idProperty = property.FindPropertyRelative("variableId");
            var choices = new List<string>();
            var ids = new List<string>();
            var selected = CollectChoices(property, choices, ids);
            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            var next = EditorGUI.Popup(position, label, selected, Contents(choices));
            if (EditorGUI.EndChangeCheck() && next >= 0 && next < ids.Count)
            {
                idProperty.stringValue = ids[next];
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        int CollectChoices(SerializedProperty property, List<string> choices, List<string> ids)
        {
            var idProperty = property.FindPropertyRelative("variableId");
            var node = property.serializedObject.targetObject as XNode.Node;
            var valueType = GetExpectedValueType(node?.GetType(), property);
            choices.Add("<None>");
            ids.Add(string.Empty);
            var selected = 0;

            if (node != null && node.graph != null && valueType != null)
            {
                var variables = node.graph.BlackboardDefinition.Variables;
                for (var i = 0; i < variables.Count; i++)
                {
                    var variable = variables[i];
                    if (variable == null || variable.ValueType != valueType)
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

            return selected;
        }

        static GUIContent[] Contents(List<string> choices)
        {
            var contents = new GUIContent[choices.Count];
            for (var i = 0; i < choices.Count; i++)
            {
                contents[i] = new GUIContent(choices[i]);
            }

            return contents;
        }

        Type GetExpectedValueType(Type nodeType, SerializedProperty property)
        {
            var fromField = GenericArgument(fieldInfo?.FieldType);
            if (fromField != null)
            {
                return fromField;
            }

            var fromParent = GenericArgument(ParentFieldType(property));
            if (fromParent != null)
            {
                return fromParent;
            }

            var current = nodeType;
            while (current != null)
            {
                if (current.IsGenericType)
                {
                    var argument = current.GetGenericArguments()[0];
                    if (!argument.IsGenericParameter)
                    {
                        return argument;
                    }
                }

                current = current.BaseType;
            }

            return null;
        }

        static Type ParentFieldType(SerializedProperty property)
        {
            var path = property.propertyPath;
            var separator = path.LastIndexOf('.');
            if (separator <= 0)
            {
                return null;
            }

            var parent = property.serializedObject.FindProperty(path.Substring(0, separator));
            if (parent == null || parent.propertyType != SerializedPropertyType.ManagedReference)
            {
                return null;
            }

            var parentType = ResolveManagedType(parent.managedReferenceFullTypename);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            while (parentType != null)
            {
                var field = parentType.GetField(property.name, flags);
                if (field != null)
                {
                    return field.FieldType;
                }

                parentType = parentType.BaseType;
            }

            return null;
        }

        static Type GenericArgument(Type fieldType)
        {
            if (fieldType == null ||
                !fieldType.IsGenericType ||
                fieldType.GetGenericTypeDefinition() != typeof(XNode.BlackboardVariableReference<>))
            {
                return null;
            }

            var argument = fieldType.GetGenericArguments()[0];
            return argument.IsGenericParameter ? null : argument;
        }

        static Type ResolveManagedType(string managedTypename)
        {
            if (string.IsNullOrEmpty(managedTypename))
            {
                return null;
            }

            var space = managedTypename.IndexOf(' ');
            if (space <= 0 || space >= managedTypename.Length - 1)
            {
                return null;
            }

            var assemblyName = managedTypename.Substring(0, space);
            var typeName = managedTypename.Substring(space + 1).Replace('/', '+');
            var resolved = Type.GetType(typeName + ", " + assemblyName);
            if (resolved != null)
            {
                return resolved;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                if (!string.Equals(assemblies[i].GetName().Name, assemblyName, StringComparison.Ordinal))
                {
                    continue;
                }

                resolved = assemblies[i].GetType(typeName);
                if (resolved != null)
                {
                    return resolved;
                }
            }

            return null;
        }
    }
}
