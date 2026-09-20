using UnityEngine;
using UnityEngine.UIElements;
using XNode;

namespace XNodeEditor.Ui {
    public sealed class PortView : VisualElement {
        public NodePort Port { get; }
        public NodeView NodeView { get; }

        public Vector2 LocalOffsetOnNode;

        public PortView(NodePort port, NodeView nodeView) {
            Port = port;
            NodeView = nodeView;
            AddToClassList("port-view");
            AddToClassList(port != null && port.IsOutput ? "output" : "input");
            pickingMode = PickingMode.Position;
            tooltip = "";
            RegisterCallback<GeometryChangedEvent>(_ => CacheOffset());
        }

        public void ApplyColor(Color color) {
            if (ClassListContains("port-slot")) return;
            style.backgroundColor = color;
        }

        public void ApplyTooltip(string text) {
            tooltip = text ?? "";
        }

        public bool HasValidLayout {
            get {
                Rect world = worldBound;
                return world.width >= 2f && world.height >= 2f;
            }
        }

        public void CacheOffset() {
            if (NodeView == null || !HasValidLayout) return;
            LocalOffsetOnNode = NodeView.WorldToLocal(worldBound.center);
        }

        public Vector2 CenterIn(VisualElement space) {
            if (space == null) return Vector2.zero;
            if (HasValidLayout) return space.WorldToLocal(worldBound.center);
            if (NodeView == null) return Vector2.zero;
            Vector2 localOnNode = LocalOffsetOnNode;
            if (localOnNode.sqrMagnitude < 0.01f) {
                float width = NodeView.resolvedStyle.width > 1f ? NodeView.resolvedStyle.width : 280f;
                localOnNode = Port != null && Port.IsOutput
                    ? new Vector2(width, 22f)
                    : new Vector2(0f, 22f);
            }
            return space.WorldToLocal(NodeView.LocalToWorld(localOnNode));
        }
    }
}
