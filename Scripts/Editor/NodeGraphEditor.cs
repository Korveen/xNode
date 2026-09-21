using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace XNodeEditor {
    /// <summary> Base class to derive custom Node Graph editors from. Use this to override how graphs are drawn in the editor. </summary>
    [CustomNodeGraphEditor(typeof(XNode.NodeGraph))]
    public class NodeGraphEditor : XNodeEditor.Internal.NodeEditorBase<NodeGraphEditor, NodeGraphEditor.CustomNodeGraphEditorAttribute, XNode.NodeGraph> {
        [Obsolete("Use window.position instead")]
        public Rect position { get { return window.position; } set { window.position = value; } }
        /// <summary> Are we currently renaming a node? </summary>
        protected bool isRenaming;

        public virtual void BuildToolbar(VisualElement slot) { }

        public virtual void BuildOverlay(VisualElement overlay) { }

        public virtual void ApplyStyles(VisualElement root) { }

        public virtual void OnNodesMoved() { }

        public virtual bool TryGetNodeIndex(XNode.Node node, out int index) {
            index = -1;
            return false;
        }

        /// <summary> Called when opened by NodeEditorWindow </summary>
        public virtual void OnOpen() { }

        /// <summary> Called when NodeEditorWindow gains focus </summary>
        public virtual void OnWindowFocus() { }

        /// <summary> Called when NodeEditorWindow loses focus </summary>
        public virtual void OnWindowFocusLost() { }

        public virtual Texture2D GetGridTexture() {
            return NodeEditorPreferences.GetGridTexture();
        }

        public virtual Texture2D GetSecondaryGridTexture() {
            return NodeEditorPreferences.GetCrossTexture();
        }

        /// <summary> Return default settings for this graph type. This is the settings the user will load if no previous settings have been saved. </summary>
        public virtual NodeEditorPreferences.GraphTypeSettings GetDefaultPreferences() {
            return new NodeEditorPreferences.GraphTypeSettings();
        }

        /// <summary> Returns context node menu path. Null or empty strings for hidden nodes. </summary>
        public virtual string GetNodeMenuName(Type type) {
            //Check if type has the CreateNodeMenuAttribute
            XNode.Node.CreateNodeMenuAttribute attrib;
            if (NodeEditorUtilities.GetAttrib(type, out attrib)) // Return custom path
                return attrib.menuName;
            else // Return generated path
                return NodeEditorUtilities.NodeDefaultPath(type);
        }

        /// <summary> The order by which the menu items are displayed. </summary>
        public virtual int GetNodeMenuOrder(Type type) {
            //Check if type has the CreateNodeMenuAttribute
            XNode.Node.CreateNodeMenuAttribute attrib;
            if (NodeEditorUtilities.GetAttrib(type, out attrib)) // Return custom path
                return attrib.order;
            else
                return 0;
        }

        /// <summary>
        /// Called before connecting two ports in the graph view to see if the output port is compatible with the input port
        /// </summary>
        public virtual bool CanConnect(XNode.NodePort output, XNode.NodePort input) {
            return output.CanConnectTo(input);
        }

        /// <summary>
        /// Prefer this input when a connection is dropped on the node body instead of a specific port.
        /// </summary>
        public virtual XNode.NodePort GetCompatibleInput(XNode.NodePort output, XNode.Node node) {
            if (output == null || node == null || output.node == node) return null;
            foreach (XNode.NodePort input in node.Inputs) {
                if (CanConnect(output, input) && !output.IsConnectedTo(input)) return input;
            }
            return null;
        }

        /// <summary>
        /// Prefer this output when a reverse-dragged input is dropped on the node body instead of a specific port.
        /// </summary>
        public virtual XNode.NodePort GetCompatibleOutput(XNode.NodePort input, XNode.Node node) {
            if (input == null || node == null || input.node == node) return null;
            foreach (XNode.NodePort output in node.Outputs) {
                if (CanConnect(output, input) && !output.IsConnectedTo(input)) return output;
            }
            return null;
        }

        /// <summary> Returned gradient is used to color noodles </summary>
        /// <param name="output"> The output this noodle comes from. Never null. </param>
        /// <param name="input"> The output this noodle comes from. Can be null if we are dragging the noodle. </param>
        public virtual Gradient GetNoodleGradient(XNode.NodePort output, XNode.NodePort input) {
            Gradient grad = new Gradient();

            // If dragging the noodle, draw solid, slightly transparent
            if (input == null) {
                Color a = GetPortColor(output);
                grad.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(a, 0f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(0.6f, 0f) }
                );
            }
            // If normal, draw gradient fading from one input color to the other
            else {
                Color a = GetPortColor(output);
                Color b = GetPortColor(input);
                // If any port is hovered, tint white
                if (window.hoveredPort == output || window.hoveredPort == input) {
                    a = Color.Lerp(a, Color.white, 0.8f);
                    b = Color.Lerp(b, Color.white, 0.8f);
                }
                grad.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
                );
            }
            return grad;
        }

        /// <summary> Returned float is used for noodle thickness </summary>
        /// <param name="output"> The output this noodle comes from. Never null. </param>
        /// <param name="input"> The output this noodle comes from. Can be null if we are dragging the noodle. </param>
        public virtual float GetNoodleThickness(XNode.NodePort output, XNode.NodePort input) {
            return NodeEditorPreferences.GetFor(this).noodleThickness;
        }

        /// <summary>
        /// Direction the noodle leaves a port, in GUI space (y+ is down).
        /// Default is left-to-right. Override for vertical graphs.
        /// </summary>
        public virtual Vector2 GetNoodlePortDirection(XNode.NodePort port) {
            if (port == null || port.IsOutput) return Vector2.right;
            return Vector2.left;
        }

        public virtual NoodlePath GetNoodlePath(XNode.NodePort output, XNode.NodePort input) {
            return NodeEditorPreferences.GetFor(this).noodlePath;
        }

        public virtual NoodleStroke GetNoodleStroke(XNode.NodePort output, XNode.NodePort input) {
            return NodeEditorPreferences.GetFor(this).noodleStroke;
        }

        public virtual float GetNoodleTension(XNode.NodePort output, XNode.NodePort input) {
            float tension = NodeEditorPreferences.GetFor(this).noodleTension;
            return tension > 0.01f ? tension : 0.45f;
        }

        /// <summary> Returned color is used to color ports </summary>
        public virtual Color GetPortColor(XNode.NodePort port) {
            return NodeEditorPreferences.ResolvePortColor(port, NodeEditorPreferences.GetFor(this), false);
        }

        /// <summary>
        /// The returned Style is used to configure the paddings and icon texture of the ports.
        /// Use these properties to customize your port style.
        ///
        /// The properties used is:
        /// <see cref="GUIStyle.padding"/>[Left and Right], <see cref="GUIStyle.normal"/> [Background] = border texture,
        /// and <seealso cref="GUIStyle.active"/> [Background] = dot texture;
        /// </summary>
        /// <param name="port">the owner of the style</param>
        /// <returns></returns>
        public virtual GUIStyle GetPortStyle(XNode.NodePort port) {
            if (port.direction == XNode.NodePort.IO.Input)
                return NodeEditorResources.styles.inputPort;

            return NodeEditorResources.styles.outputPort;
        }

        /// <summary> The returned color is used to color the background of the door.
        /// Usually used for outer edge effect </summary>
        public virtual Color GetPortBackgroundColor(XNode.NodePort port) {
            return Color.gray;
        }

        /// <summary> Returns generated color for a type. This color is editable in preferences </summary>
        public virtual Color GetTypeColor(Type type) {
            return NodeEditorPreferences.GetTypeColor(type);
        }

        /// <summary> Override to display custom tooltips </summary>
        public virtual string GetPortTooltip(XNode.NodePort port) {
            Type portType = port.ValueType;
            string tooltip = "";
            tooltip = portType.PrettyName();
            if (port.IsOutput) {
                object obj = port.node.GetValue(port);
                tooltip += " = " + (obj != null ? obj.ToString() : "null");
            }
            return tooltip;
        }

        /// <summary> Deal with objects dropped into the graph through DragAndDrop </summary>
        public virtual void OnDropObjects(UnityEngine.Object[] objects) {
            if (GetType() != typeof(NodeGraphEditor)) Debug.Log("No OnDropObjects override defined for " + GetType());
        }

        /// <summary> Create a node and save it in the graph asset </summary>
        public virtual XNode.Node CreateNode(Type type, Vector2 position) {
            Undo.RecordObject(target, "Create Node");
            XNode.Node node = target.AddNode(type);
            if (node == null) return null; // handle null nodes to avoid nullref exceptions
            Undo.RegisterCreatedObjectUndo(node, "Create Node");
            node.position = position;
            if (node.name == null || node.name.Trim() == "") node.name = NodeEditorUtilities.NodeDefaultName(type);
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(target))) AssetDatabase.AddObjectToAsset(node, target);
            if (NodeEditorPreferences.GetSettings().autoSave) AssetDatabase.SaveAssets();
            NodeEditorWindow.RepaintAll();
            return node;
        }

        /// <summary> Creates a copy of the original node in the graph </summary>
        public virtual XNode.Node CopyNode(XNode.Node original) {
            Undo.RecordObject(target, "Duplicate Node");
            XNode.Node node = target.CopyNode(original);
            Undo.RegisterCreatedObjectUndo(node, "Duplicate Node");
            node.name = original.name;
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(target))) AssetDatabase.AddObjectToAsset(node, target);
            if (NodeEditorPreferences.GetSettings().autoSave) AssetDatabase.SaveAssets();
            return node;
        }

        /// <summary> Return false for nodes that can't be removed </summary>
        public virtual bool CanRemove(XNode.Node node) {
            // Check graph attributes to see if this node is required
            Type graphType = target.GetType();
            XNode.NodeGraph.RequireNodeAttribute[] attribs = Array.ConvertAll(
                graphType.GetCustomAttributes(typeof(XNode.NodeGraph.RequireNodeAttribute), true), x => x as XNode.NodeGraph.RequireNodeAttribute);
            if (attribs.Any(x => x.Requires(node.GetType()))) {
                if (target.nodes.Count(x => x.GetType() == node.GetType()) <= 1) {
                    return false;
                }
            }
            return true;
        }

        /// <summary> Safely remove a node and all its connections. </summary>
        public virtual void RemoveNode(XNode.Node node) {
            if (!CanRemove(node)) return;

            // Remove the node
            Undo.RecordObject(node, "Delete Node");
            Undo.RecordObject(target, "Delete Node");
            foreach (var port in node.Ports)
                foreach (var conn in port.GetConnections())
                    Undo.RecordObject(conn.node, "Delete Node");
            target.RemoveNode(node);
            Undo.DestroyObjectImmediate(node);
            if (NodeEditorPreferences.GetSettings().autoSave) AssetDatabase.SaveAssets();
        }

        [AttributeUsage(AttributeTargets.Class, Inherited = false)]
        public class CustomNodeGraphEditorAttribute : Attribute,
        XNodeEditor.Internal.NodeEditorBase<NodeGraphEditor, CustomNodeGraphEditorAttribute, XNode.NodeGraph>.INodeEditorAttrib {
            private Type inspectedType;
            public string editorPrefsKey;
            /// <summary> Tells a NodeGraphEditor which Graph type it is an editor for </summary>
            /// <param name="inspectedType">Type that this editor can edit</param>
            /// <param name="editorPrefsKey">Define unique key for unique layout settings instance</param>
            public CustomNodeGraphEditorAttribute(Type inspectedType, string editorPrefsKey = "xNode.Settings") {
                this.inspectedType = inspectedType;
                this.editorPrefsKey = editorPrefsKey;
            }

            public Type GetInspectedType() {
                return inspectedType;
            }
        }
    }
}