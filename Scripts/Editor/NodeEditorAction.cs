using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using XNodeEditor.Internal;

namespace XNodeEditor {
    public partial class NodeEditorWindow {
        public enum NodeActivity { Idle, HoldNode, DragNode, HoldGrid, DragGrid }
        public static NodeActivity currentActivity = NodeActivity.Idle;
        public static bool isPanning { get; private set; }
        public static Vector2[] dragOffset;
        public static XNode.Node[] copyBuffer = null;

        public bool IsDraggingPort { get { return draggedOutput != null || draggedInput != null; } }
        public bool IsHoveringPort { get { return hoveredPort != null; } }
        public bool IsHoveringNode { get { return hoveredNode != null; } }
        public bool IsHoveringReroute { get { return hoveredReroute.port != null; } }

        public XNode.NodePort DraggedOutputPort { get { return draggedOutput; } }
        public XNode.NodePort HoveredPort { get { return hoveredPort; } }
        public XNode.Node HoveredNode { get { return hoveredNode; } }

        private XNode.Node hoveredNode = null;
        [NonSerialized] public XNode.NodePort hoveredPort = null;
        [NonSerialized] internal XNode.NodePort draggedOutput = null;
        [NonSerialized] internal XNode.NodePort draggedInput = null;
        [NonSerialized] private XNode.NodePort autoConnectOutput = null;

        private RerouteReference hoveredReroute = new RerouteReference();
        public List<RerouteReference> selectedReroutes = new List<RerouteReference>();

        public void Home() {
            FrameSelection();
        }

        public void FrameSelection() {
            var nodes = Selection.objects.OfType<XNode.Node>().Where(node => node != null).ToList();
            if (nodes.Count == 0) FrameAll();
            else FrameNodes(nodes);
        }

        public void FrameAll() {
            if (graph == null) return;
            var nodes = graph.nodes.Where(node => node != null).ToList();
            FrameNodes(nodes);
        }

        private void FrameNodes(List<XNode.Node> nodes) {
            if (nodes == null || nodes.Count == 0) {
                zoom = 1f;
                panOffset = Vector2.zero;
                return;
            }

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < nodes.Count; i++) {
                XNode.Node node = nodes[i];
                Vector2 size = nodeSizes.TryGetValue(node, out Vector2 cachedSize)
                    ? cachedSize
                    : new Vector2(208f, 80f);
                min = Vector2.Min(min, node.position);
                max = Vector2.Max(max, node.position + size);
            }

            Vector2 center = (min + max) * 0.5f;
            Vector2 bounds = max - min;
            float padding = 80f;
            float viewWidth = Mathf.Max(120f, position.width - (ShowBlackboard ? BlackboardWidth : 0f) - padding);
            float viewHeight = Mathf.Max(120f, position.height - GetToolbarRect().height - padding);
            float fitZoom = Mathf.Max(bounds.x / viewWidth, bounds.y / viewHeight, 0.01f);
            NodeEditorPreferences.SharedSettings settings = NodeEditorPreferences.GetShared();
            zoom = Mathf.Clamp(fitZoom, settings.minZoom, settings.maxZoom);

            float blackboard = ShowBlackboard ? BlackboardWidth : 0f;
            float toolbar = GetToolbarRect().height;
            panOffset = new Vector2(
                -center.x - blackboard * 0.5f * zoom,
                -center.y + toolbar * 0.5f * zoom);
        }

        public void RemoveSelectedNodes() {
            selectedReroutes = selectedReroutes.OrderByDescending(x => x.pointIndex).ToList();
            for (int i = 0; i < selectedReroutes.Count; i++) {
                selectedReroutes[i].RemovePoint();
            }
            selectedReroutes.Clear();
            foreach (UnityEngine.Object item in Selection.objects) {
                if (item is XNode.Node) {
                    XNode.Node node = item as XNode.Node;
                    graphEditor.RemoveNode(node);
                }
            }
        }

        public void RenameSelectedNode() {
            if (Selection.objects.Length == 1 && Selection.activeObject is XNode.Node) {
                XNode.Node node = Selection.activeObject as XNode.Node;
                Vector2 size;
                if (nodeSizes.TryGetValue(node, out size)) {
                    RenamePopup.Show(Selection.activeObject, size.x);
                } else {
                    RenamePopup.Show(Selection.activeObject);
                }
            }
        }

        public void MoveNodeToTop(XNode.Node node) {
            int index;
            while ((index = graph.nodes.IndexOf(node)) != graph.nodes.Count - 1) {
                graph.nodes[index] = graph.nodes[index + 1];
                graph.nodes[index + 1] = node;
            }
        }

        public void DuplicateSelectedNodes() {
            XNode.Node[] selectedNodes = Selection.objects.Select(x => x as XNode.Node).Where(x => x != null && x.graph == graph).ToArray();
            if (selectedNodes == null || selectedNodes.Length == 0) return;
            Vector2 topLeftNode = selectedNodes.Select(x => x.position).Aggregate((x, y) => new Vector2(Mathf.Min(x.x, y.x), Mathf.Min(x.y, y.y)));
            InsertDuplicateNodes(selectedNodes, topLeftNode + new Vector2(30, 30));
        }

        public void CopySelectedNodes() {
            copyBuffer = Selection.objects.Select(x => x as XNode.Node).Where(x => x != null && x.graph == graph).ToArray();
        }

        public void PasteNodes(Vector2 pos) {
            InsertDuplicateNodes(copyBuffer, pos);
        }

        private void InsertDuplicateNodes(XNode.Node[] nodes, Vector2 topLeft) {
            if (nodes == null || nodes.Length == 0) return;

            Vector2 topLeftNode = nodes.Select(x => x.position).Aggregate((x, y) => new Vector2(Mathf.Min(x.x, y.x), Mathf.Min(x.y, y.y)));
            Vector2 offset = topLeft - topLeftNode;

            UnityEngine.Object[] newNodes = new UnityEngine.Object[nodes.Length];
            Dictionary<XNode.Node, XNode.Node> substitutes = new Dictionary<XNode.Node, XNode.Node>();
            for (int i = 0; i < nodes.Length; i++) {
                XNode.Node srcNode = nodes[i];
                if (srcNode == null) continue;

                XNode.Node.DisallowMultipleNodesAttribute disallowAttrib;
                Type nodeType = srcNode.GetType();
                if (NodeEditorUtilities.GetAttrib(nodeType, out disallowAttrib)) {
                    int typeCount = graph.nodes.Count(x => x.GetType() == nodeType);
                    if (typeCount >= disallowAttrib.max) continue;
                }

                XNode.Node newNode = graphEditor.CopyNode(srcNode);
                substitutes.Add(srcNode, newNode);
                newNode.position = srcNode.position + offset;
                newNodes[i] = newNode;
            }

            for (int i = 0; i < nodes.Length; i++) {
                XNode.Node srcNode = nodes[i];
                if (srcNode == null) continue;
                foreach (XNode.NodePort port in srcNode.Ports) {
                    for (int c = 0; c < port.ConnectionCount; c++) {
                        XNode.NodePort inputPort = port.direction == XNode.NodePort.IO.Input ? port : port.GetConnection(c);
                        XNode.NodePort outputPort = port.direction == XNode.NodePort.IO.Output ? port : port.GetConnection(c);

                        XNode.Node newNodeIn, newNodeOut;
                        if (substitutes.TryGetValue(inputPort.node, out newNodeIn) && substitutes.TryGetValue(outputPort.node, out newNodeOut)) {
                            newNodeIn.UpdatePorts();
                            newNodeOut.UpdatePorts();
                            inputPort = newNodeIn.GetInputPort(inputPort.fieldName);
                            outputPort = newNodeOut.GetOutputPort(outputPort.fieldName);
                        }
                        if (!inputPort.IsConnectedTo(outputPort)) inputPort.Connect(outputPort);
                    }
                }
            }
            EditorUtility.SetDirty(graph);
            Selection.objects = newNodes;
        }

        public void AutoConnect(XNode.Node node) {
            if (autoConnectOutput == null) return;

            XNode.NodePort inputPort = node.Ports.FirstOrDefault(x => x.IsInput && graphEditor.CanConnect(autoConnectOutput, x));
            if (inputPort != null) autoConnectOutput.Connect(inputPort);

            EditorUtility.SetDirty(graph);
            if (NodeEditorPreferences.GetSettings().autoSave) AssetDatabase.SaveAssets();
            autoConnectOutput = null;
        }
    }
}
