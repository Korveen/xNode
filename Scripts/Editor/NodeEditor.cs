using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace XNodeEditor {
    /// <summary> Base class to derive custom Node editors from. Use this to create your own custom inspectors and editors for your nodes. </summary>
    [CustomNodeEditor(typeof(XNode.Node))]
    public class NodeEditor : XNodeEditor.Internal.NodeEditorBase<NodeEditor, NodeEditor.CustomNodeEditorAttribute, XNode.Node> {

        /// <summary> Fires every whenever a node was modified through the editor </summary>
        public static Action<XNode.Node> onUpdateNode;
        public readonly static Dictionary<XNode.NodePort, Vector2> portPositions = new Dictionary<XNode.NodePort, Vector2>();

        public virtual void BuildHeader(Ui.NodeView view) {
            if (view == null) return;
            view.SetTitle(target != null ? target.name : "");
            view.SetTint(GetTint());
            string tip = GetHeaderTooltip();
            if (!string.IsNullOrEmpty(tip)) view.tooltip = tip;
        }

        public virtual void BuildBody(Ui.NodeView view) {
            Ui.NodeUiUtility.BindNodeFields(view, serializedObject);
        }

        public virtual int GetWidth() {
            Type type = target.GetType();
            int width;
            if (type.TryGetAttributeWidth(out width)) return width;
            else return 280;
        }

        /// <summary> Returns color for target node </summary>
        public virtual Color GetTint() {
            Type type = target.GetType();
            Color color;
            if (type.TryGetAttributeTint(out color)) return color;
            else return NodeEditorPreferences.GetSettings().tintColor;
        }

        /// <summary> Height of the header used for node dragging. </summary>
        public virtual float GetHeaderHeight() {
            return 30;
        }

        /// <summary> Override to display custom node header tooltips </summary>
        public virtual string GetHeaderTooltip() {
            return null;
        }

        /// <summary> Rename the node asset. This will trigger a reimport of the node. </summary>
        public void Rename(string newName) {
            if (newName == null || newName.Trim() == "") newName = NodeEditorUtilities.NodeDefaultName(target.GetType());
            target.name = newName;
            OnRename();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(target));
        }

        /// <summary> Called after this node's name has changed. </summary>
        public virtual void OnRename() { }

        [AttributeUsage(AttributeTargets.Class)]
        public class CustomNodeEditorAttribute : Attribute,
        XNodeEditor.Internal.NodeEditorBase<NodeEditor, NodeEditor.CustomNodeEditorAttribute, XNode.Node>.INodeEditorAttrib {
            private Type inspectedType;
            /// <summary> Tells a NodeEditor which Node type it is an editor for </summary>
            /// <param name="inspectedType">Type that this editor can edit</param>
            public CustomNodeEditorAttribute(Type inspectedType) {
                this.inspectedType = inspectedType;
            }

            public Type GetInspectedType() {
                return inspectedType;
            }
        }
    }
}
