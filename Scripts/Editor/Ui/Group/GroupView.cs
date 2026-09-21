using UnityEngine;
using UnityEngine.UIElements;
using XNode;

namespace XNodeEditor.Ui {
    public sealed class GroupView : NodeView {
        public const int MinWidth = 200;
        public const int MinHeight = 100;
        public const float HeaderHeight = 30f;

        public GroupView(GroupNode group) : base(group) {
            AddToClassList("group-view");
            ApplyGroupSize();
        }

        public override void SetTint(Color color) {
            style.backgroundColor = color;
            Color header = color;
            header.a = Mathf.Clamp01(Mathf.Max(color.a, 0.22f) + 0.15f);
            Header.style.backgroundColor = header;
        }

        public void ApplyGroupSize() {
            var group = Node as GroupNode;
            if (group == null) return;
            style.width = group.width;
            style.minWidth = MinWidth;
            Body.style.height = group.height;
            Body.style.minHeight = group.height;
        }
    }
}
