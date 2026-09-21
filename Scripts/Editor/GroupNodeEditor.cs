using UnityEngine;
using XNode;
using XNodeEditor.Ui;

namespace XNodeEditor {
    [CustomNodeEditor(typeof(GroupNode))]
    public class GroupNodeEditor : NodeEditor {
        public override int GetWidth() {
            var group = target as GroupNode;
            return group != null ? group.width : 400;
        }

        public override Color GetTint() {
            var group = target as GroupNode;
            return group != null ? group.color : base.GetTint();
        }

        public override void BuildHeader(Ui.NodeView view) {
            base.BuildHeader(view);
            view.AddToClassList("group-view");
        }

        public override void BuildBody(Ui.NodeView view) {
            if (view is GroupView groupView) groupView.ApplyGroupSize();
        }
    }
}
