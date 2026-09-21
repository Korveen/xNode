using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using XNode;

namespace XNodeEditor.Ui {
    public class NodeView : VisualElement {
        public Node Node { get; }
        public readonly Dictionary<NodePort, PortView> Ports = new Dictionary<NodePort, PortView>();

        readonly Label _title;
        readonly Label _index;
        readonly Label _badge;
        readonly VisualElement _header;
        readonly VisualElement _body;

        public VisualElement Header => _header;
        public VisualElement Body => _body;

        public NodeView(Node node) {
            Node = node;
            AddToClassList("node-view");
            pickingMode = PickingMode.Position;
            usageHints = UsageHints.DynamicTransform;
            style.flexDirection = FlexDirection.Column;

            _header = new VisualElement();
            _header.AddToClassList("node-header");
            _title = new Label(node != null ? node.name : "Node");
            _title.AddToClassList("node-header__title");
            _index = new Label();
            _index.AddToClassList("node-header__index");
            _index.pickingMode = PickingMode.Ignore;
            _badge = new Label();
            _badge.AddToClassList("node-header__badge");
            _header.Add(_index);
            _header.Add(_title);
            _header.Add(_badge);
            Add(_header);

            _body = new VisualElement();
            _body.AddToClassList("node-body");
            _body.style.flexDirection = FlexDirection.Column;
            Add(_body);

            style.position = Position.Absolute;
            SyncPosition();
        }

        public void SetTitle(string title) {
            _title.text = title ?? "";
        }

        public virtual void SetTint(Color color) {
            _header.style.backgroundColor = color;
        }

        public void BeginRename(System.Action<string> apply) {
            if (_title.parent == null) return;
            int index = _header.IndexOf(_title);
            var field = new TextField { value = Node != null ? Node.name : _title.text };
            field.AddToClassList("node-header__rename");
            field.AddToClassList("unity-base-text-field");
            _header.Insert(index, field);
            _title.style.display = DisplayStyle.None;
            field.schedule.Execute(() => field.Focus()).ExecuteLater(1);
            void Finish(bool commit) {
                if (field.parent == null) return;
                string next = field.value;
                field.RemoveFromHierarchy();
                _title.style.display = DisplayStyle.Flex;
                if (commit && apply != null) apply(next);
            }
            field.RegisterCallback<KeyDownEvent>(evt => {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) {
                    Finish(true);
                    evt.StopPropagation();
                } else if (evt.keyCode == KeyCode.Escape) {
                    Finish(false);
                    evt.StopPropagation();
                }
            }, TrickleDown.TrickleDown);
            field.RegisterCallback<FocusOutEvent>(_ => Finish(true));
        }

        public void SetSelected(bool selected) {
            EnableInClassList("selected", selected);
            ApplySelectionColor(NodeEditorPreferences.GetShared().selectionColor);
        }

        public void ApplySelectionColor(Color color) {
            if (ClassListContains("selected")) {
                style.borderTopColor = color;
                style.borderBottomColor = color;
                style.borderLeftColor = color;
                style.borderRightColor = color;
                style.borderTopWidth = 2;
                style.borderBottomWidth = 2;
                style.borderLeftWidth = 2;
                style.borderRightWidth = 2;
            } else {
                style.borderTopColor = StyleKeyword.Null;
                style.borderBottomColor = StyleKeyword.Null;
                style.borderLeftColor = StyleKeyword.Null;
                style.borderRightColor = StyleKeyword.Null;
                style.borderTopWidth = StyleKeyword.Null;
                style.borderBottomWidth = StyleKeyword.Null;
                style.borderLeftWidth = StyleKeyword.Null;
                style.borderRightWidth = StyleKeyword.Null;
            }
        }

        public void SetDimmed(bool dimmed) {
            EnableInClassList("dimmed", dimmed);
        }

        public void SetIndex(int? index) {
            bool visible = index.HasValue;
            _index.EnableInClassList("visible", visible);
            _index.text = visible ? index.Value.ToString() : "";
        }

        public void SetBadge(string text, Color color) {
            bool visible = !string.IsNullOrEmpty(text);
            _badge.EnableInClassList("visible", visible);
            _badge.text = text ?? "";
            _badge.style.backgroundColor = color;
        }

        public void SyncPosition() {
            if (Node == null) return;
            style.left = Node.position.x;
            style.top = Node.position.y;
        }

        public PortView CreatePort(NodePort port) {
            if (port == null) return null;
            if (Ports.TryGetValue(port, out PortView existing)) return existing;
            var view = new PortView(port, this);
            Ports[port] = view;
            return view;
        }

        public VisualElement CreatePortRow(NodePort port, string label, bool output) {
            var row = new VisualElement();
            row.AddToClassList("node-port-row");
            if (output) row.AddToClassList("output");
            var portView = CreatePort(port);
            var caption = new Label(label ?? "");
            caption.AddToClassList("port-label");
            if (output) {
                row.Add(caption);
                row.Add(portView);
            } else {
                row.Add(portView);
                row.Add(caption);
            }
            return row;
        }

        public VisualElement AddPortRow(NodePort port, string label, bool output) {
            VisualElement row = CreatePortRow(port, label, output);
            _body.Add(row);
            return row;
        }

        public VisualElement AddInOutRow(NodePort input, string inputLabel, NodePort output, string outputLabel) {
            var row = new VisualElement();
            row.AddToClassList("node-port-row");
            row.AddToClassList("inout");
            if (input != null) {
                row.Add(CreatePort(input));
                var caption = new Label(inputLabel ?? "");
                caption.AddToClassList("port-label");
                row.Add(caption);
            }
            var spacer = new VisualElement();
            spacer.AddToClassList("port-spacer");
            row.Add(spacer);
            if (output != null) {
                var caption = new Label(outputLabel ?? "");
                caption.AddToClassList("port-label");
                row.Add(caption);
                row.Add(CreatePort(output));
            }
            _body.Add(row);
            return row;
        }
    }
}
