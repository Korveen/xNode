using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace XNodeEditor {
    internal static class BlackboardEditorPanel {
        static Type[] _variableTypes;
        static Dictionary<Type, Type> _getterTypes;

        static readonly Type[] TypeOrder = {
            typeof(bool),
            typeof(int),
            typeof(float),
            typeof(string),
            typeof(Vector2),
            typeof(Vector3),
            typeof(Quaternion),
            typeof(Color),
            typeof(UnityEngine.Object)
        };

        static readonly Dictionary<Type, string> TypeLabels = new Dictionary<Type, string> {
            { typeof(bool), "bool" },
            { typeof(int), "int" },
            { typeof(float), "float" },
            { typeof(string), "string" },
            { typeof(Vector2), "Vector2" },
            { typeof(Vector3), "Vector3" },
            { typeof(Quaternion), "Quaternion" },
            { typeof(Color), "Color" },
            { typeof(UnityEngine.Object), "Object" }
        };

        static Type[] VariableTypes {
            get {
                if (_variableTypes == null) CacheTypes();
                return _variableTypes;
            }
        }

        static Dictionary<Type, Type> GetterTypes {
            get {
                if (_getterTypes == null) CacheTypes();
                return _getterTypes;
            }
        }

        static void CacheTypes() {
            var variables = new List<Type>();
            foreach (Type type in typeof(XNode.BlackboardVariable).GetDerivedTypes()) {
                if (type.GetConstructor(Type.EmptyTypes) == null) continue;
                if (ValueTypeOf(type) == null) continue;
                variables.Add(type);
            }
            variables.Sort(CompareVariableTypes);
            _variableTypes = variables.ToArray();

            _getterTypes = new Dictionary<Type, Type>();
            Type[] nodes = NodeEditorReflection.nodeTypes;
            for (int i = 0; i < nodes.Length; i++) {
                Type valueType = GetterValueType(nodes[i]);
                if (valueType != null) _getterTypes[valueType] = nodes[i];
            }
        }

        static int CompareVariableTypes(Type a, Type b) {
            int orderA = TypeOrderIndex(ValueTypeOf(a));
            int orderB = TypeOrderIndex(ValueTypeOf(b));
            if (orderA != orderB) return orderA.CompareTo(orderB);
            return string.Compare(TypeLabel(ValueTypeOf(a)), TypeLabel(ValueTypeOf(b)), StringComparison.Ordinal);
        }

        static int TypeOrderIndex(Type valueType) {
            if (valueType == null) return TypeOrder.Length + 100;
            for (int i = 0; i < TypeOrder.Length; i++) {
                if (TypeOrder[i] == valueType) return i;
                if (TypeOrder[i] == typeof(UnityEngine.Object) &&
                    typeof(UnityEngine.Object).IsAssignableFrom(valueType)) {
                    return i;
                }
            }
            return TypeOrder.Length + 1;
        }

        static Type ValueTypeOf(Type variableType) {
            Type current = variableType;
            while (current != null) {
                if (current.IsGenericType &&
                    current.GetGenericTypeDefinition() == typeof(XNode.BlackboardVariable<>)) {
                    return current.GetGenericArguments()[0];
                }
                current = current.BaseType;
            }
            return null;
        }

        static Type GetterValueType(Type nodeType) {
            Type current = nodeType;
            while (current != null) {
                if (current.IsGenericType &&
                    current.GetGenericTypeDefinition() == typeof(XNode.GetBlackboardVariableNode<>)) {
                    return current.GetGenericArguments()[0];
                }
                current = current.BaseType;
            }
            return null;
        }

        internal static VisualElement Build(NodeEditorWindow window) {
            return new Panel(window).Root;
        }

        internal static string TypeLabel(Type type) {
            if (type == null) return "?";
            if (TypeLabels.TryGetValue(type, out string label)) return label;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return "Object";
            return type.Name;
        }

        internal static void CreateGetter(NodeEditorWindow window, XNode.BlackboardVariable variable) {
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

        sealed class Panel {
            public readonly VisualElement Root;
            readonly NodeEditorWindow _window;
            readonly SerializedObject _serializedGraph;
            readonly SerializedProperty _variables;
            readonly ListView _list;
            readonly HelpBox _duplicate;
            readonly List<int> _indices = new List<int>();
            readonly bool _allowEdits;

            public Panel(NodeEditorWindow window) {
                _window = window;
                _allowEdits = !Application.isPlaying;
                _serializedGraph = new SerializedObject(window.graph);
                _variables = _serializedGraph.FindProperty("blackboardDefinition")
                    ?.FindPropertyRelative("variables");

                Root = new VisualElement();
                Root.AddToClassList("bb-panel");

                var header = new VisualElement();
                header.AddToClassList("bb-header");
                var title = new Label("Blackboard");
                title.AddToClassList("bb-title");
                title.tooltip = _allowEdits ? "Drag the handle to reorder." : "";
                header.Add(title);
                var addButton = new Button { text = "+" };
                addButton.tooltip = "Add variable";
                addButton.clicked += () => ShowAddMenu(addButton);
                addButton.SetEnabled(_allowEdits);
                header.Add(addButton);
                Root.Add(header);

                if (_variables == null) {
                    Root.Add(new HelpBox("Blackboard data could not be serialized.", HelpBoxMessageType.Error));
                    return;
                }

                _list = new ListView {
                    reorderable = _allowEdits,
                    reorderMode = ListViewReorderMode.Animated,
                    virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                    selectionType = SelectionType.None,
                    showBoundCollectionSize = false,
                    showAddRemoveFooter = false,
                    showFoldoutHeader = false,
                    horizontalScrollingEnabled = false,
                    fixedItemHeight = 72
                };
                _list.AddToClassList("bb-list");
                _list.makeItem = MakeRow;
                _list.bindItem = BindRow;
                _list.unbindItem = UnbindRow;
                _list.itemIndexChanged += OnReordered;
                RefreshSource();
                Root.Add(_list);
                _list.schedule.Execute(() => {
                    ScrollView scroll = _list.Q<ScrollView>();
                    if (scroll != null)
                        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                });

                _duplicate = new HelpBox("", HelpBoxMessageType.Error);
                Root.Add(_duplicate);
                RefreshDuplicate();
                Root.TrackSerializedObjectValue(_serializedGraph, _ => RefreshDuplicate());
            }

            VisualElement MakeRow() {
                var card = new VisualElement();
                card.AddToClassList("bb-card");

                var top = new VisualElement();
                top.AddToClassList("bb-card-header");

                var nameField = new TextField { name = "name" };
                nameField.AddToClassList("bb-name");
                top.Add(nameField);

                var typeLabel = new Label { name = "type" };
                typeLabel.AddToClassList("bb-type");
                top.Add(typeLabel);

                var getButton = new Button { name = "get", text = "Get" };
                getButton.AddToClassList("bb-get");
                top.Add(getButton);

                var remove = new Button { name = "remove", text = "×" };
                remove.AddToClassList("bb-remove");
                remove.SetEnabled(_allowEdits);
                top.Add(remove);

                card.Add(top);

                var valueHost = new VisualElement { name = "value" };
                valueHost.AddToClassList("bb-value-host");
                card.Add(valueHost);
                return card;
            }

            void BindRow(VisualElement card, int index) {
                if (index < 0 || index >= _variables.arraySize) return;
                _serializedGraph.Update();
                SerializedProperty element = _variables.GetArrayElementAtIndex(index);
                var variable = element.managedReferenceValue as XNode.BlackboardVariable;
                card.EnableInClassList("highlighted",
                    variable != null && _window.HighlightedVariableId == variable.Id);
                card.userData = index;

                var nameField = card.Q<TextField>("name");
                SerializedProperty nameProperty = element.FindPropertyRelative("name");
                nameField.UnregisterValueChangedCallback(OnNameChanged);
                nameField.Unbind();
                if (nameProperty != null) nameField.BindProperty(nameProperty);
                nameField.SetEnabled(_allowEdits);
                nameField.RegisterValueChangedCallback(OnNameChanged);

                var typeLabel = card.Q<Label>("type");
                typeLabel.text = TypeLabel(variable?.ValueType);
                typeLabel.tooltip = variable != null
                    ? NodeEditorUtilities.PrettyName(variable.ValueType)
                    : "";

                var getButton = card.Q<Button>("get");
                getButton.userData = variable;
                getButton.UnregisterCallback<ClickEvent>(OnGetClicked);
                getButton.RegisterCallback<ClickEvent>(OnGetClicked);

                var remove = card.Q<Button>("remove");
                remove.userData = index;
                remove.UnregisterCallback<ClickEvent>(OnRemoveClicked);
                remove.RegisterCallback<ClickEvent>(OnRemoveClicked);

                BindValue(card.Q("value"), element.FindPropertyRelative("defaultValue"), variable?.ValueType);
            }

            void UnbindRow(VisualElement card, int _) {
                var nameField = card.Q<TextField>("name");
                if (nameField != null) {
                    nameField.UnregisterValueChangedCallback(OnNameChanged);
                    nameField.Unbind();
                }
                card.Q("value")?.Clear();
                card.Q<Button>("get")?.UnregisterCallback<ClickEvent>(OnGetClicked);
                card.Q<Button>("remove")?.UnregisterCallback<ClickEvent>(OnRemoveClicked);
            }

            void BindValue(VisualElement host, SerializedProperty defaultValue, Type type) {
                if (host == null) return;
                host.Clear();
                if (defaultValue == null) return;

                VisualElement field;
                if (type == typeof(Quaternion)) {
                    var euler = new Vector3Field();
                    euler.SetValueWithoutNotify(defaultValue.quaternionValue.eulerAngles);
                    euler.RegisterValueChangedCallback(evt => {
                        defaultValue.quaternionValue = Quaternion.Euler(evt.newValue);
                        defaultValue.serializedObject.ApplyModifiedProperties();
                    });
                    field = euler;
                } else {
                    var property = new PropertyField(defaultValue, "");
                    property.BindProperty(defaultValue);
                    field = property;
                }

                field.AddToClassList("bb-value");
                host.Add(field);
            }

            void OnNameChanged(ChangeEvent<string> _) {
                _serializedGraph.ApplyModifiedProperties();
                RefreshDuplicate();
            }

            void OnGetClicked(ClickEvent evt) {
                if (evt.currentTarget is not Button button) return;
                if (button.userData is not XNode.BlackboardVariable variable) return;
                CreateGetter(_window, variable);
                _window.RebuildUi();
            }

            void OnRemoveClicked(ClickEvent evt) {
                if (evt.currentTarget is not Button button) return;
                if (button.userData is not int index) return;
                RemoveVariable(index);
            }

            void OnReordered(int from, int to) {
                if (from == to) return;
                Undo.RecordObject(_window.graph, "Reorder Blackboard Variable");
                _serializedGraph.Update();
                _variables.MoveArrayElement(from, to);
                _serializedGraph.ApplyModifiedProperties();
                EditorUtility.SetDirty(_window.graph);
                RefreshSource();
                RefreshDuplicate();
            }

            void ShowAddMenu(VisualElement anchor) {
                var menu = new GenericDropdownMenu();
                Type[] types = VariableTypes;
                for (int i = 0; i < types.Length; i++) {
                    Type type = types[i];
                    string label = TypeLabel(ValueTypeOf(type)) ?? type.Name;
                    menu.AddItem(label, false, () => AddVariable(type, label));
                }
                menu.DropDown(anchor.worldBound, anchor, false);
            }

            void AddVariable(Type variableType, string label) {
                Undo.RecordObject(_window.graph, "Add Blackboard Variable");
                _serializedGraph.Update();
                int index = _variables.arraySize;
                _variables.InsertArrayElementAtIndex(index);
                _variables.GetArrayElementAtIndex(index).managedReferenceValue =
                    Activator.CreateInstance(variableType);
                _serializedGraph.ApplyModifiedProperties();

                XNode.BlackboardVariable variable = _window.graph.BlackboardDefinition.Variables[index];
                _window.graph.BlackboardDefinition.Rename(
                    variable.Id,
                    _window.graph.BlackboardDefinition.GetUniqueName(label));
                EditorUtility.SetDirty(_window.graph);
                RefreshSource();
                RefreshDuplicate();
            }

            void RemoveVariable(int index) {
                if (index < 0 || index >= _variables.arraySize) return;
                Undo.RecordObject(_window.graph, "Remove Blackboard Variable");
                _serializedGraph.Update();
                _variables.DeleteArrayElementAtIndex(index);
                _serializedGraph.ApplyModifiedProperties();
                EditorUtility.SetDirty(_window.graph);
                RefreshSource();
                RefreshDuplicate();
            }

            void RefreshSource() {
                _serializedGraph.Update();
                _indices.Clear();
                for (int i = 0; i < _variables.arraySize; i++)
                    _indices.Add(i);
                _list.itemsSource = _indices;
                _list.RefreshItems();
            }

            void RefreshDuplicate() {
                _serializedGraph.Update();
                string duplicate = FindDuplicateName(_window.graph.BlackboardDefinition.Variables);
                bool show = !string.IsNullOrEmpty(duplicate);
                _duplicate.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (show) _duplicate.text = $"Variable name '{duplicate}' is duplicated.";
            }
        }

        static string FindDuplicateName(IReadOnlyList<XNode.BlackboardVariable> variables) {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < variables.Count; i++) {
                XNode.BlackboardVariable variable = variables[i];
                if (variable != null && !names.Add(variable.Name)) return variable.Name;
            }
            return null;
        }
    }
}
