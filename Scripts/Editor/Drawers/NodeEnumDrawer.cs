using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using XNode;

namespace XNodeEditor {
	[CustomPropertyDrawer(typeof(NodeEnumAttribute))]
	public class NodeEnumDrawer : PropertyDrawer {
		public override VisualElement CreatePropertyGUI(SerializedProperty property) {
			if (property.propertyType != SerializedPropertyType.Enum)
				throw new ArgumentException("Parameter selected must be of type System.Enum");

			Type enumType = fieldInfo != null ? UnwrapEnumType(fieldInfo.FieldType) : null;
			if (enumType != null && Attribute.IsDefined(enumType, typeof(FlagsAttribute), false)) {
				var flags = new EnumFlagsField(preferredLabel);
				flags.BindProperty(property);
				flags.AddToClassList("xn-field");
				return flags;
			}

			var field = new EnumField(preferredLabel);
			field.BindProperty(property);
			field.AddToClassList("xn-field");
			return field;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
			EditorGUI.BeginProperty(position, label, property);
			EnumPopup(position, property, label);
			EditorGUI.EndProperty();
		}

		public static void EnumPopup(Rect position, SerializedProperty property, GUIContent label) {
			if (property.propertyType != SerializedPropertyType.Enum)
				throw new ArgumentException("Parameter selected must be of type System.Enum");

			position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

			string enumName = "";
			if (property.enumValueIndex >= 0 && property.enumValueIndex < property.enumDisplayNames.Length)
				enumName = property.enumDisplayNames[property.enumValueIndex];

			if (EditorGUI.DropdownButton(position, new GUIContent(enumName), FocusType.Passive)) {
				if (NodeEditorWindow.current != null)
					NodeEditorWindow.current.onLateGUI += () => ShowContextMenuAtMouse(property);
				else
					ShowContextMenuAtMouse(property);
			}
		}

		public static void ShowContextMenuAtMouse(SerializedProperty property) {
			GenericMenu menu = new GenericMenu();
			for (int i = 0; i < property.enumDisplayNames.Length; i++) {
				int index = i;
				menu.AddItem(new GUIContent(property.enumDisplayNames[i]), false, () => SetEnum(property, index));
			}
			Rect r = new Rect(Event.current.mousePosition, new Vector2(0, 0));
			menu.DropDown(r);
		}

		private static void SetEnum(SerializedProperty property, int index) {
			property.enumValueIndex = index;
			property.serializedObject.ApplyModifiedProperties();
			property.serializedObject.Update();
		}

		static Type UnwrapEnumType(Type type) {
			if (type == null) return null;
			if (type.IsEnum) return type;
			return Nullable.GetUnderlyingType(type) is Type underlying && underlying.IsEnum
				? underlying
				: type;
		}
	}
}
