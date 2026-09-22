using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using XNode;
using XNodeEditor.Internal;
using Object = UnityEngine.Object;

namespace XNodeEditor.Ui {
    public sealed class GraphUiController : IDisposable {
        public readonly NodeEditorWindow Window;
        public readonly Dictionary<NodePort, PortView> Ports = new Dictionary<NodePort, PortView>();
        public readonly Dictionary<Node, NodeView> Nodes = new Dictionary<Node, NodeView>();

        readonly VisualElement _root;
        readonly VisualElement _toolbar;
        readonly VisualElement _viewport;
        readonly VisualElement _content;
        readonly VisualElement _nodesLayer;
        readonly VisualElement _groupsLayer;
        readonly VisualElement _groupEdges;
        readonly VisualElement _blackboard;
        readonly VisualElement _blackboardResize;
        readonly VisualElement _overlay;
        readonly VisualElement _selectionBox;
        readonly GridElement _grid;
        readonly NoodleLayer _noodles;
        readonly Slider _zoomSlider;
        readonly Label _zoomLabel;
        readonly TextField _searchField;
        readonly DropdownField _variableFilter;
        readonly VisualElement _graphToolbarSlot;
        readonly VisualElement _rerouteLayer;
        readonly List<string> _variableIds = new List<string>();
        RerouteReference _dragReroute;
        bool _rerouteDrag;
        Vector2 _rerouteOrigin;

        NodePort _dragPort;
        NodePort _autoConnectPort;
        Button _blackboardButton;
        Button _snapButton;
        Button _indexButton;
        Label _cursorLabel;
        Vector2 _dragPointer;
        bool _panning;
        bool _boxing;
        Vector2 _boxStart;
        bool _draggingNodes;
        bool _dragCollapseOnClick;
        Node _dragClickedNode;
        bool _resizingBlackboard;
        readonly Dictionary<Node, Vector2> _dragOrigins = new Dictionary<Node, Vector2>();
        Vector2 _dragPointerStart;
        GroupNode _resizeGroup;
        GroupEdge _resizeEdge;
        Vector2 _resizePointer;
        Vector2 _resizeOrigin;
        int _resizeWidth;
        int _resizeHeight;

        enum GroupEdge { N, S, E, W, NE, NW, SE, SW }

        sealed class GroupEdgeTag {
            public GroupNode Group;
            public GroupEdge Edge;
        }

        public GraphUiController(NodeEditorWindow window, VisualElement root) {
            Window = window;
            _root = root;
            _toolbar = root.Q("toolbar");
            _viewport = root.Q("viewport");
            _content = root.Q("content");
            _nodesLayer = root.Q("nodes");
            _groupsLayer = root.Q("groups");
            _groupEdges = root.Q("group-edges");
            if (_nodesLayer != null) _nodesLayer.pickingMode = PickingMode.Ignore;
            if (_groupEdges != null) _groupEdges.pickingMode = PickingMode.Ignore;
            _blackboard = root.Q("blackboard");
            _blackboardResize = root.Q("blackboard-resize");
            _overlay = root.Q("overlay");
            _selectionBox = root.Q("selection-box");

            var gridHost = root.Q("grid");
            _grid = new GridElement();
            _grid.style.flexGrow = 1;
            _viewport.Insert(0, _grid);
            gridHost?.RemoveFromHierarchy();

            var noodleHost = root.Q("noodles");
            _noodles = new NoodleLayer { Controller = this };
            _noodles.AddToClassList("graph-noodles");
            if (noodleHost != null) {
                int index = noodleHost.parent.IndexOf(noodleHost);
                noodleHost.parent.Insert(index, _noodles);
                noodleHost.RemoveFromHierarchy();
            } else {
                _content.Insert(0, _noodles);
            }

            _rerouteLayer = new VisualElement { name = "reroutes" };
            _rerouteLayer.AddToClassList("graph-reroutes");
            _rerouteLayer.pickingMode = PickingMode.Ignore;
            int nodeIndex = _nodesLayer != null ? _content.IndexOf(_nodesLayer) : -1;
            if (nodeIndex >= 0) _content.Insert(nodeIndex, _rerouteLayer);
            else _content.Add(_rerouteLayer);

            NodeEditorPreferences.SharedSettings shared = NodeEditorPreferences.GetShared();
            _zoomSlider = new Slider(shared.minZoom, shared.maxZoom) { value = window.zoom };
            _zoomSlider.style.width = 80;
            _zoomLabel = new Label();
            _zoomLabel.AddToClassList("toolbar-label");
            _searchField = new TextField();
            _searchField.style.width = 140;
            _variableFilter = new DropdownField();
            _variableFilter.style.width = 140;
            _graphToolbarSlot = new VisualElement();
            _graphToolbarSlot.style.flexDirection = FlexDirection.Row;
            _graphToolbarSlot.style.alignItems = Align.Center;

            BuildToolbar();
            _root.focusable = true;
            RegisterViewport();
            RegisterBlackboardResize();
            _viewport.AddManipulator(new ContextualMenuManipulator(OnViewportContext));
            _nodesLayer.AddManipulator(new ContextualMenuManipulator(OnViewportContext));
            _groupsLayer?.AddManipulator(new ContextualMenuManipulator(OnViewportContext));
            Undo.undoRedoPerformed += Rebuild;
            Selection.selectionChanged += RefreshSelection;
            NodeEditorPreferences.Changed += ApplyVisualPrefs;
            ApplyView();
            Rebuild();
            _root.schedule.Execute(RefreshRuntime).Every(120);
        }

        void RefreshRuntime() {
            if (!EditorApplication.isPlaying || Window.graph == null) return;
            foreach (var pair in Nodes) {
                NodeEditor editor = NodeEditor.GetEditor(pair.Key, Window);
                editor.BuildHeader(pair.Value);
            }
            _noodles.MarkDirtyRepaint();
        }

        public void Dispose() {
            Undo.undoRedoPerformed -= Rebuild;
            Selection.selectionChanged -= RefreshSelection;
            NodeEditorPreferences.Changed -= ApplyVisualPrefs;
        }

        public void Rebuild() {
            if (Window.graph == null) return;
            Window.ValidateGraphEditor();
            Window.graphEditor?.ApplyStyles(_root);
            RebuildToolbarExtras();
            RebuildBlackboard();
            RebuildOverlay();
            RebuildNodes();
            RebuildRerouteHandles();
            RefreshSelection();
            ApplyVisualPrefs();
        }

        public void RefreshNodes() {
            if (Window.graph == null) return;
            RebuildNodes();
            RefreshSelection();
        }

        void BuildToolbar() {
            _toolbar.Clear();
            _toolbar.Add(ToolbarLabel("Scale"));
            _toolbar.Add(_zoomSlider);
            _toolbar.Add(_zoomLabel);
            _zoomSlider.RegisterValueChangedCallback(evt => {
                Window.zoom = evt.newValue;
                ApplyView();
            });

            _toolbar.Add(new Button(FrameSelection) { text = "Frame" });
            _toolbar.Add(new Button(FrameAll) { text = "All" });
            _toolbar.Add(new Button(() => EditorGUIUtility.PingObject(Window.graph)) { text = "Ping" });
            _snapButton = new Button(ToggleGridSnap) { text = "Snap" };
            _snapButton.tooltip = "Snap nodes to the grid while dragging. Ctrl inverts.";
            _snapButton.AddToClassList("toolbar-toggle");
            _snapButton.EnableInClassList("active", NodeEditorPreferences.GetSettings().gridSnap);
            _toolbar.Add(_snapButton);
            _indexButton = new Button(ToggleNodeIndices) { text = "Idx" };
            _indexButton.tooltip = "Show compiled plan indices on node headers.";
            _indexButton.AddToClassList("toolbar-toggle");
            _indexButton.EnableInClassList("active", NodeEditorPreferences.GetSettings().showNodeIndices);
            _toolbar.Add(_indexButton);
            _toolbar.Add(ToolbarLabel("Node"));
            _searchField.value = Window.NodeSearch;
            _searchField.RegisterValueChangedCallback(evt => {
                Window.NodeSearch = evt.newValue;
                RefreshSelection();
                Window.RefreshHighlightHasMatches();
            });
            _searchField.RegisterCallback<KeyDownEvent>(evt => {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                Window.FrameNodeSearchResult(evt.shiftKey ? -1 : 1);
                ApplyView();
            });
            _toolbar.Add(_searchField);
            _toolbar.Add(ToolbarLabel("Uses"));
            _toolbar.Add(_variableFilter);
            _variableFilter.RegisterValueChangedCallback(evt => {
                int index = _variableFilter.choices != null
                    ? _variableFilter.choices.IndexOf(evt.newValue)
                    : -1;
                Window.HighlightedVariableId = index > 0 && index <= _variableIds.Count
                    ? _variableIds[index - 1]
                    : "";
                RefreshSelection();
            });
            _toolbar.Add(_graphToolbarSlot);
            var spacer = new VisualElement();
            spacer.AddToClassList("toolbar-spacer");
            _toolbar.Add(spacer);
            _blackboardButton = new Button(ToggleBlackboard) { text = "Blackboard" };
            _blackboardButton.AddToClassList("bb-toolbar-toggle");
            _blackboardButton.EnableInClassList("active", Window.ShowBlackboard);
            _toolbar.Add(_blackboardButton);
            _blackboard.EnableInClassList("hidden", !Window.ShowBlackboard);
            ApplyBlackboardVisibility();
            _cursorLabel = new Label();
            _cursorLabel.AddToClassList("graph-cursor");
            _cursorLabel.pickingMode = PickingMode.Ignore;
            _viewport.Add(_cursorLabel);
            UpdateCursorLabel(Vector2.zero);
        }

        void ToggleGridSnap() {
            bool next = !NodeEditorPreferences.GetSettings().gridSnap;
            NodeEditorPreferences.SetGridSnap(next);
            _snapButton?.EnableInClassList("active", next);
        }

        void ToggleNodeIndices() {
            bool next = !NodeEditorPreferences.GetSettings().showNodeIndices;
            NodeEditorPreferences.SetShowNodeIndices(next);
            _indexButton?.EnableInClassList("active", next);
            RefreshNodeIndices();
        }

        void RefreshNodeIndices() {
            bool show = NodeEditorPreferences.GetSettings().showNodeIndices;
            foreach (var pair in Nodes)
                ApplyNodeIndex(pair.Value, show);
        }

        void ApplyNodeIndex(NodeView view, bool show) {
            if (view == null) return;
            if (!show || Window.graphEditor == null ||
                !Window.graphEditor.TryGetNodeIndex(view.Node, out int index)) {
                view.SetIndex(null);
                return;
            }
            view.SetIndex(index);
        }

        void ToggleBlackboard() {
            Window.ShowBlackboard = !Window.ShowBlackboard;
            _blackboardButton?.EnableInClassList("active", Window.ShowBlackboard);
            ApplyBlackboardVisibility();
        }

        void ApplyBlackboardVisibility() {
            bool hidden = !Window.ShowBlackboard;
            _blackboard.EnableInClassList("hidden", hidden);
            _blackboardResize?.EnableInClassList("hidden", hidden);
        }

        void RebuildToolbarExtras() {
            _graphToolbarSlot.Clear();
            Window.graphEditor?.BuildToolbar(_graphToolbarSlot);
            RebuildVariableFilter();
        }

        void RebuildVariableFilter() {
            var choices = new List<string> { "(None)" };
            _variableIds.Clear();
            if (Window.graph != null) {
                IReadOnlyList<BlackboardVariable> variables = Window.graph.BlackboardDefinition.Variables;
                for (int i = 0; i < variables.Count; i++) {
                    if (variables[i] == null) continue;
                    choices.Add(variables[i].Name);
                    _variableIds.Add(variables[i].Id);
                }
            }
            _variableFilter.choices = choices;
            int selected = Mathf.Max(0, _variableIds.IndexOf(Window.HighlightedVariableId) + 1);
            _variableFilter.SetValueWithoutNotify(choices[Mathf.Clamp(selected, 0, choices.Count - 1)]);
        }

        void RebuildBlackboard() {
            _blackboard.Clear();
            if (Window.graph == null) return;
            ApplyBlackboardWidth();
            _blackboard.Add(BlackboardEditorPanel.Build(Window));
        }

        void ApplyBlackboardWidth() {
            if (_blackboard == null) return;
            _blackboard.style.width = Window.BlackboardWidth;
        }

        void RegisterBlackboardResize() {
            if (_blackboardResize == null) return;
            _blackboardResize.RegisterCallback<PointerDownEvent>(evt => {
                if (evt.button != 0) return;
                _resizingBlackboard = true;
                _blackboardResize.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });
            _blackboardResize.RegisterCallback<PointerMoveEvent>(evt => {
                if (!_resizingBlackboard || !_blackboardResize.HasPointerCapture(evt.pointerId)) return;
                float localX = _root.WorldToLocal(evt.position).x;
                Window.BlackboardWidth = _root.layout.width - localX;
                ApplyBlackboardWidth();
                evt.StopPropagation();
            });
            _blackboardResize.RegisterCallback<PointerUpEvent>(evt => {
                if (!_blackboardResize.HasPointerCapture(evt.pointerId)) return;
                _blackboardResize.ReleasePointer(evt.pointerId);
                _resizingBlackboard = false;
                evt.StopPropagation();
            });
        }

        void RebuildOverlay() {
            _overlay.Clear();
            Window.graphEditor?.BuildOverlay(_overlay);
        }

        void RebuildNodes() {
            _nodesLayer.Clear();
            _groupsLayer?.Clear();
            Nodes.Clear();
            Ports.Clear();
            if (Window.graph?.nodes == null) return;
            for (int i = 0; i < Window.graph.nodes.Count; i++) {
                Node node = Window.graph.nodes[i];
                if (node == null) continue;
                NodeView view = node is GroupNode group
                    ? new GroupView(group)
                    : new NodeView(node);
                NodeEditor editor = NodeEditor.GetEditor(node, Window);
                view.style.width = editor.GetWidth();
                editor.BuildHeader(view);
                editor.BuildBody(view);
                ApplyNodeIndex(view, NodeEditorPreferences.GetSettings().showNodeIndices);
                foreach (var pair in view.Ports) {
                    Ports[pair.Key] = pair.Value;
                    pair.Value.ApplyColor(Window.graphEditor.GetPortColor(pair.Key));
                    pair.Value.ApplyTooltip(PortTooltip(pair.Key));
                    BindPort(pair.Value);
                }
                BindNode(view);
                view.RegisterCallback<GeometryChangedEvent>(_ => {
                    Window.nodeSizes[node] = new Vector2(
                        Mathf.Max(view.resolvedStyle.width, editor.GetWidth()),
                        Mathf.Max(view.resolvedStyle.height, 40f));
                    _noodles.MarkDirtyRepaint();
                });
                if (node is GroupNode && _groupsLayer != null) _groupsLayer.Add(view);
                else _nodesLayer.Add(view);
                Nodes[node] = view;
                Window.nodeSizes[node] = new Vector2(editor.GetWidth(), 80f);
            }
            RebuildRerouteHandles();
            CullNodes();
            SyncGroupEdges();
        }

        void BindNode(NodeView view) {
            view.RegisterCallback<PointerDownEvent>(evt => {
                if (evt.button != 0) return;
                if (BlocksNodeDrag(evt.target)) return;
                bool additive = evt.ctrlKey || evt.commandKey || evt.shiftKey;
                bool already = Selection.Contains(view.Node);
                if (additive) {
                    if (already) Window.DeselectNode(view.Node);
                    else Window.SelectNode(view.Node, true);
                    _dragCollapseOnClick = false;
                } else if (!already) {
                    Window.SelectNode(view.Node, false);
                    _dragCollapseOnClick = false;
                } else {
                    _dragCollapseOnClick = Selection.objects.Length > 1;
                }
                _dragClickedNode = view.Node;
                BeginNodeDrag(evt.position);
                view.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });
            view.RegisterCallback<PointerMoveEvent>(evt => {
                if (!_draggingNodes || !view.HasPointerCapture(evt.pointerId)) return;
                DragNodes(evt.position, evt.ctrlKey || evt.commandKey);
            });
            view.RegisterCallback<PointerUpEvent>(evt => {
                bool moved = ((Vector2)evt.position - _dragPointerStart).sqrMagnitude > 16f;
                if (view.HasPointerCapture(evt.pointerId))
                    view.ReleasePointer(evt.pointerId);
                Node clicked = _dragClickedNode;
                bool collapse = _dragCollapseOnClick && !moved && clicked != null;
                EndNodeDrag();
                if (collapse) Window.SelectNode(clicked, false);
            });
        }

        static bool BlocksNodeDrag(IEventHandler target) {
            for (var ve = target as VisualElement; ve != null; ve = ve.parent) {
                if (ve is PortView) return true;
                if (ve is Button) return true;
                if (ve is IMGUIContainer) return true;
                if (ve.ClassListContains("unity-base-field")) return true;
                if (ve.ClassListContains("unity-button")) return true;
                if (ve.ClassListContains("unity-base-slider")) return true;
                if (ve.ClassListContains("group-resize")) return true;
            }
            return false;
        }

        void BindPort(PortView portView) {
            portView.RegisterCallback<PointerEnterEvent>(_ => {
                Window.hoveredPort = portView.Port;
                portView.AddToClassList("hovered");
                _noodles.MarkDirtyRepaint();
            });
            portView.RegisterCallback<PointerLeaveEvent>(_ => {
                if (Window.hoveredPort == portView.Port) Window.hoveredPort = null;
                portView.RemoveFromClassList("hovered");
                _noodles.MarkDirtyRepaint();
            });
            portView.RegisterCallback<PointerDownEvent>(evt => {
                if (evt.button != 0) return;
                BeginPortDrag(portView.Port, evt.position);
                _viewport.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });
        }

        void BeginPortDrag(NodePort port, Vector2 panelPosition) {
            _dragPointer = panelPosition;
            _autoConnectPort = null;
            if (port.IsOutput) {
                _dragPort = port;
                _autoConnectPort = port;
                Window.draggedOutput = port;
                Window.draggedInput = null;
                return;
            }

            if (port.IsConnected) {
                NodePort output = port.Connection;
                Undo.RecordObject(port.node, "Disconnect");
                Undo.RecordObject(output.node, "Disconnect");
                output.Disconnect(port);
                EditorUtility.SetDirty(port.node);
                EditorUtility.SetDirty(output.node);
                _dragPort = output;
                _autoConnectPort = output;
                Window.draggedOutput = output;
                Window.draggedInput = null;
                _noodles.MarkDirtyRepaint();
                return;
            }

            _dragPort = port;
            Window.draggedOutput = null;
            Window.draggedInput = port;
        }

        void FinishPortDrag(Vector2 panelPosition) {
            NodePort from = _dragPort;
            NodePort autoConnect = _autoConnectPort;
            _dragPort = null;
            _autoConnectPort = null;
            Window.draggedOutput = null;
            Window.draggedInput = null;
            if (from == null) {
                _noodles.MarkDirtyRepaint();
                return;
            }

            PortView hit = FindPortAt(panelPosition, from);
            NodePort target = hit?.Port;
            if (target == null) {
                NodeView nodeHit = FindNodeAt(panelPosition);
                if (nodeHit != null) {
                    target = from.IsOutput
                        ? Window.graphEditor.GetCompatibleInput(from, nodeHit.Node)
                        : Window.graphEditor.GetCompatibleOutput(from, nodeHit.Node);
                }
            }

            if (target != null && target != from) {
                NodePort output = from.IsOutput ? from : target;
                NodePort input = from.IsOutput ? target : from;
                if (Window.graphEditor.CanConnect(output, input) && output != input) {
                    Undo.RecordObject(output.node, "Connect");
                    Undo.RecordObject(input.node, "Connect");
                    output.Connect(input);
                    EditorUtility.SetDirty(output.node);
                    EditorUtility.SetDirty(input.node);
                    EditorUtility.SetDirty(Window.graph);
                    Rebuild();
                    return;
                }
            }

            bool idleOutput = autoConnect != null && autoConnect.IsOutput && !autoConnect.IsConnected;
            NodeView ownView = null;
            bool onOwnNode = idleOutput &&
                autoConnect.node != null &&
                Nodes.TryGetValue(autoConnect.node, out ownView) &&
                ownView.worldBound.Contains(panelPosition);
            bool empty = FindNodeAt(panelPosition) == null;
            bool showCreate = idleOutput &&
                (onOwnNode || (empty && NodeEditorPreferences.GetSettings().dragToCreate));
            Vector2 menuPos = panelPosition;
            if (onOwnNode && ownView != null) {
                Rect bound = ownView.worldBound;
                menuPos = new Vector2(bound.center.x, bound.yMax + 8f);
            }
            Rebuild();
            if (showCreate)
                ShowCreateMenu(menuPos, autoConnect);
        }

        PortView FindPortAt(Vector2 panelPosition, NodePort ignore = null) {
            PortView best = null;
            float bestDist = 22f * 22f;
            foreach (var pair in Ports) {
                if (pair.Value == null || pair.Key == ignore) continue;
                Vector2 center = pair.Value.HasValidLayout
                    ? pair.Value.worldBound.center
                    : pair.Value.NodeView.LocalToWorld(pair.Value.LocalOffsetOnNode);
                float dist = (center - panelPosition).sqrMagnitude;
                if (dist >= bestDist) continue;
                bestDist = dist;
                best = pair.Value;
            }
            return best;
        }

        NodeView FindNodeAt(Vector2 panelPosition) {
            NodeView groupHit = null;
            foreach (var pair in Nodes) {
                if (pair.Value == null || !pair.Value.worldBound.Contains(panelPosition)) continue;
                if (pair.Key is GroupNode) {
                    if (groupHit == null) groupHit = pair.Value;
                    continue;
                }
                return pair.Value;
            }
            return groupHit;
        }

        void RegisterViewport() {
            _viewport.RegisterCallback<WheelEvent>(evt => {
                Vector2 local = _viewport.WorldToLocal(evt.mousePosition);
                Vector2 grid = ScreenToGrid(evt.mousePosition);
                float factor = evt.delta.y > 0 ? 1.1f : 0.9f;
                NodeEditorPreferences.SharedSettings shared = NodeEditorPreferences.GetShared();
                Window.zoom = Mathf.Clamp(
                    Window.zoom * factor,
                    shared.minZoom,
                    shared.maxZoom);
                if (shared.zoomToMouse) {
                    float s = 1f / Window.zoom;
                    Window.panOffset = local - grid * s;
                }
                ApplyView();
                evt.StopPropagation();
            });
            _viewport.RegisterCallback<PointerDownEvent>(evt => {
                _root.Focus();
                if (evt.button == 2 || (evt.button == 0 && evt.altKey && FindNodeAt(evt.position) == null && FindPortAt(evt.position) == null)) {
                    if (evt.altKey && evt.button == 0) {
                        if (TryInsertReroute(ScreenToGrid(evt.position))) {
                            evt.StopPropagation();
                            return;
                        }
                    }
                    _panning = true;
                    _viewport.CapturePointer(evt.pointerId);
                    evt.StopPropagation();
                    return;
                }
                if (evt.button == 0 && FindNodeAt(evt.position) == null && FindPortAt(evt.position) == null) {
                    _boxing = true;
                    _boxStart = _viewport.WorldToLocal(evt.position);
                    _viewport.CapturePointer(evt.pointerId);
                    evt.StopPropagation();
                }
            });
            _viewport.RegisterCallback<PointerMoveEvent>(evt => {
                UpdateCursorLabel(evt.position);
                if (_panning && _viewport.HasPointerCapture(evt.pointerId)) {
                    Window.panOffset += (Vector2)evt.deltaPosition;
                    ApplyView();
                }
                if (_boxing && _viewport.HasPointerCapture(evt.pointerId)) {
                    Vector2 now = _viewport.WorldToLocal(evt.position);
                    Rect box = Rect.MinMaxRect(
                        Mathf.Min(_boxStart.x, now.x),
                        Mathf.Min(_boxStart.y, now.y),
                        Mathf.Max(_boxStart.x, now.x),
                        Mathf.Max(_boxStart.y, now.y));
                    _selectionBox.style.display = DisplayStyle.Flex;
                    _selectionBox.style.left = box.x;
                    _selectionBox.style.top = box.y;
                    _selectionBox.style.width = box.width;
                    _selectionBox.style.height = box.height;
                }
                if (_dragPort != null) {
                    _dragPointer = evt.position;
                    _noodles.MarkDirtyRepaint();
                }
            });
            _viewport.RegisterCallback<PointerUpEvent>(evt => {
                if (_viewport.HasPointerCapture(evt.pointerId))
                    _viewport.ReleasePointer(evt.pointerId);
                if (_dragPort != null) {
                    FinishPortDrag(evt.position);
                    _panning = false;
                    _boxing = false;
                    return;
                }
                if (_boxing) {
                    Vector2 now = _viewport.WorldToLocal(evt.position);
                    Rect box = Rect.MinMaxRect(
                        Mathf.Min(_boxStart.x, now.x),
                        Mathf.Min(_boxStart.y, now.y),
                        Mathf.Max(_boxStart.x, now.x),
                        Mathf.Max(_boxStart.y, now.y));
                    SelectNodesInBox(box);
                    _selectionBox.style.display = DisplayStyle.None;
                }
                _panning = false;
                _boxing = false;
            });
            _root.RegisterCallback<KeyDownEvent>(OnGraphKeyDown, TrickleDown.TrickleDown);
            _viewport.RegisterCallback<PointerMoveEvent>(
                evt => UpdateCursorLabel(evt.position),
                TrickleDown.TrickleDown);
            _viewport.RegisterCallback<GeometryChangedEvent>(_ => CullNodes());
        }

        void OnGraphKeyDown(KeyDownEvent evt) {
            if (IsEditingText(evt.target) ||
                IsEditingText(_root.focusController?.focusedElement)) {
                return;
            }

            if (evt.keyCode == KeyCode.F) {
                FrameSelection();
                evt.StopPropagation();
            }
            if (evt.keyCode == KeyCode.F2 ||
                (NodeEditorUtilities.IsMac() && evt.keyCode == KeyCode.Return)) {
                Window.RenameSelectedNode();
                evt.StopPropagation();
            }
            if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace) {
                Window.RemoveSelectedNodes();
                Rebuild();
                evt.StopPropagation();
            }
            if ((evt.ctrlKey || evt.commandKey) && evt.keyCode == KeyCode.C) {
                Window.CopySelectedNodes();
                evt.StopPropagation();
            }
            if ((evt.ctrlKey || evt.commandKey) && evt.keyCode == KeyCode.V) {
                Window.PasteNodes(ScreenToGrid(_viewport.worldBound.center));
                Rebuild();
                evt.StopPropagation();
            }
            if ((evt.ctrlKey || evt.commandKey) && evt.keyCode == KeyCode.D) {
                Window.DuplicateSelectedNodes();
                Rebuild();
                evt.StopPropagation();
            }
            if ((evt.ctrlKey || evt.commandKey) && evt.keyCode == KeyCode.A)  {
                if (Selection.objects.Any(x => Window.graph.nodes.Contains(x as Node))) {
                    Selection.objects = Array.Empty<Object>();
                } else {
                    Selection.objects = Window.graph.nodes.Where(n => n != null).ToArray();
                }
                RefreshSelection();
                evt.StopPropagation();
            }
        }

        static bool IsEditingText(IEventHandler target) {
            for (var ve = target as VisualElement; ve != null; ve = ve.parent) {
                if (ve.ClassListContains("unity-base-text-field")) return true;
                if (ve.ClassListContains("unity-base-text-field__input")) return true;
            }
            return false;
        }

        void SelectNodesInBox(Rect viewportBox) {
            var selected = new List<Object>();
            foreach (var pair in Nodes) {
                Rect world = pair.Value.worldBound;
                Vector2 local = _viewport.WorldToLocal(world.position);
                var nodeBox = new Rect(local, world.size);
                if (nodeBox.Overlaps(viewportBox)) selected.Add(pair.Key);
            }
            Selection.objects = selected.ToArray();
        }

        Vector2 NodeSize(Node node) {
            if (node != null &&
                Window.nodeSizes.TryGetValue(node, out Vector2 size) &&
                size.x > 1f &&
                size.y > 1f) {
                return size;
            }
            NodeEditor editor = NodeEditor.GetEditor(node, Window);
            float width = editor != null ? editor.GetWidth() : 208f;
            return new Vector2(width, 80f);
        }

        void RememberDragOrigin(Node node) {
            if (node == null || node.graph != Window.graph) return;
            if (_dragOrigins.ContainsKey(node)) return;
            Undo.RecordObject(node, "Moved Node");
            _dragOrigins[node] = node.position;
        }

        void BeginNodeDrag(Vector2 pointer) {
            _draggingNodes = true;
            _dragPointerStart = pointer;
            _dragOrigins.Clear();
            foreach (Object obj in Selection.objects) {
                if (obj is Node node && node != null && node.graph == Window.graph)
                    RememberDragOrigin(node);
            }
            if (_dragClickedNode is GroupNode) {
                foreach (Object obj in Selection.objects) {
                    if (obj is not GroupNode group) continue;
                    List<Node> inside = group.GetNodes(NodeSize);
                    for (int i = 0; i < inside.Count; i++)
                        RememberDragOrigin(inside[i]);
                }
            }
        }

        void DragNodes(Vector2 pointer, bool invertSnap) {
            Vector2 delta = (pointer - _dragPointerStart) * Window.zoom;
            bool snap = NodeEditorPreferences.GetSettings().gridSnap;
            if (invertSnap) snap = !snap;
            foreach (var pair in _dragOrigins) {
                Vector2 next = pair.Value + delta;
                if (snap) next = GridElement.Snap(next, SnapStep());
                Window.SetNodePosition(pair.Key, next);
                if (Nodes.TryGetValue(pair.Key, out NodeView view)) view.SyncPosition();
            }
            _noodles.MarkDirtyRepaint();
            UpdateCursorLabel(pointer);
            SyncGroupEdges();
        }

        void EndNodeDrag() {
            if (_draggingNodes) {
                foreach (var pair in _dragOrigins)
                    EditorUtility.SetDirty(pair.Key);
                Window.graphEditor?.OnNodesMoved();
            }
            _draggingNodes = false;
            _dragCollapseOnClick = false;
            _dragClickedNode = null;
            _dragOrigins.Clear();
        }

        void RefreshSelection() {
            Window.RefreshHighlightHasMatches();
            var selected = new HashSet<Object>(Selection.objects);
            foreach (var pair in Nodes) {
                pair.Value.SetSelected(selected.Contains(pair.Key));
                pair.Value.SetDimmed(Window.IsHighlightFilterActive && !Window.IsNodeHighlighted(pair.Key));
            }
            SyncGroupEdges();
        }

        void CullNodes() {
            bool compact = !Window.ShouldDrawNodeFields;
            foreach (var pair in Nodes)
                pair.Value.EnableInClassList("compact", compact);
        }

        const float GroupEdgeThickness = 8f;
        const float GroupCornerSize = 14f;

        void SyncGroupEdges() {
            if (_groupEdges == null) return;
            if (_resizeGroup != null) {
                PlaceGroupEdges();
                return;
            }
            _groupEdges.Clear();
            foreach (Object obj in Selection.objects) {
                if (obj is GroupNode group && group != null && group.graph == Window.graph)
                    BuildGroupEdges(group);
            }
        }

        void BuildGroupEdges(GroupNode group) {
            AddGroupEdge(group, GroupEdge.N);
            AddGroupEdge(group, GroupEdge.S);
            AddGroupEdge(group, GroupEdge.E);
            AddGroupEdge(group, GroupEdge.W);
            AddGroupEdge(group, GroupEdge.NE);
            AddGroupEdge(group, GroupEdge.NW);
            AddGroupEdge(group, GroupEdge.SE);
            AddGroupEdge(group, GroupEdge.SW);
            PlaceGroupEdges();
        }

        void AddGroupEdge(GroupNode group, GroupEdge edge) {
            var handle = new VisualElement();
            handle.AddToClassList("group-edge");
            handle.AddToClassList(EdgeClass(edge));
            handle.pickingMode = PickingMode.Position;
            handle.userData = new GroupEdgeTag { Group = group, Edge = edge };
            handle.RegisterCallback<PointerDownEvent>(evt => BeginGroupResize(handle, evt));
            handle.RegisterCallback<PointerMoveEvent>(evt => DragGroupResize(handle, evt));
            handle.RegisterCallback<PointerUpEvent>(evt => EndGroupResize(handle, evt));
            _groupEdges.Add(handle);
        }

        static string EdgeClass(GroupEdge edge) {
            switch (edge) {
                case GroupEdge.N: return "group-edge-n";
                case GroupEdge.S: return "group-edge-s";
                case GroupEdge.E: return "group-edge-e";
                case GroupEdge.W: return "group-edge-w";
                case GroupEdge.NE: return "group-edge-ne";
                case GroupEdge.NW: return "group-edge-nw";
                case GroupEdge.SE: return "group-edge-se";
                default: return "group-edge-sw";
            }
        }

        void PlaceGroupEdges() {
            foreach (VisualElement handle in _groupEdges.Children()) {
                if (handle.userData is not GroupEdgeTag tag || tag.Group == null) continue;
                float x = tag.Group.position.x;
                float y = tag.Group.position.y;
                float w = tag.Group.width;
                float h = GroupView.HeaderHeight + tag.Group.height;
                float t = GroupEdgeThickness;
                float c = GroupCornerSize;
                switch (tag.Edge) {
                    case GroupEdge.N:
                        SetEdge(handle, x + c, y, Mathf.Max(0f, w - c * 2f), t);
                        break;
                    case GroupEdge.S:
                        SetEdge(handle, x + c, y + h - t, Mathf.Max(0f, w - c * 2f), t);
                        break;
                    case GroupEdge.W:
                        SetEdge(handle, x, y + c, t, Mathf.Max(0f, h - c * 2f));
                        break;
                    case GroupEdge.E:
                        SetEdge(handle, x + w - t, y + c, t, Mathf.Max(0f, h - c * 2f));
                        break;
                    case GroupEdge.NW:
                        SetEdge(handle, x, y, c, c);
                        break;
                    case GroupEdge.NE:
                        SetEdge(handle, x + w - c, y, c, c);
                        break;
                    case GroupEdge.SW:
                        SetEdge(handle, x, y + h - c, c, c);
                        break;
                    default:
                        SetEdge(handle, x + w - c, y + h - c, c, c);
                        break;
                }
            }
        }

        static void SetEdge(VisualElement handle, float x, float y, float w, float h) {
            handle.style.left = x;
            handle.style.top = y;
            handle.style.width = w;
            handle.style.height = h;
        }

        void BeginGroupResize(VisualElement handle, PointerDownEvent evt) {
            if (evt.button != 0) return;
            if (handle.userData is not GroupEdgeTag tag || tag.Group == null) return;
            _resizeGroup = tag.Group;
            _resizeEdge = tag.Edge;
            _resizePointer = ScreenToGrid(evt.position);
            _resizeOrigin = tag.Group.position;
            _resizeWidth = tag.Group.width;
            _resizeHeight = tag.Group.height;
            Undo.RecordObject(tag.Group, "Resize Group");
            handle.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        void DragGroupResize(VisualElement handle, PointerMoveEvent evt) {
            if (_resizeGroup == null || !handle.HasPointerCapture(evt.pointerId)) return;
            Vector2 delta = ScreenToGrid(evt.position) - _resizePointer;
            bool west = _resizeEdge == GroupEdge.W || _resizeEdge == GroupEdge.NW || _resizeEdge == GroupEdge.SW;
            bool east = _resizeEdge == GroupEdge.E || _resizeEdge == GroupEdge.NE || _resizeEdge == GroupEdge.SE;
            bool north = _resizeEdge == GroupEdge.N || _resizeEdge == GroupEdge.NW || _resizeEdge == GroupEdge.NE;
            bool south = _resizeEdge == GroupEdge.S || _resizeEdge == GroupEdge.SW || _resizeEdge == GroupEdge.SE;
            float right = _resizeOrigin.x + _resizeWidth;
            float bottom = _resizeOrigin.y + _resizeHeight;
            float x = _resizeOrigin.x;
            float y = _resizeOrigin.y;
            float w = _resizeWidth;
            float h = _resizeHeight;
            if (east) w = _resizeWidth + delta.x;
            if (west) {
                x = _resizeOrigin.x + delta.x;
                w = right - x;
            }
            if (south) h = _resizeHeight + delta.y;
            if (north) {
                y = _resizeOrigin.y + delta.y;
                h = bottom - y;
            }
            if (w < GroupView.MinWidth) {
                w = GroupView.MinWidth;
                if (west) x = right - w;
            }
            if (h < GroupView.MinHeight) {
                h = GroupView.MinHeight;
                if (north) y = bottom - h;
            }
            _resizeGroup.position = new Vector2(Mathf.Round(x), Mathf.Round(y));
            _resizeGroup.width = Mathf.RoundToInt(w);
            _resizeGroup.height = Mathf.RoundToInt(h);
            if (Nodes.TryGetValue(_resizeGroup, out NodeView view)) {
                view.SyncPosition();
                if (view is GroupView groupView) groupView.ApplyGroupSize();
                Window.nodeSizes[_resizeGroup] = new Vector2(_resizeGroup.width, GroupView.HeaderHeight + _resizeGroup.height);
            }
            PlaceGroupEdges();
            evt.StopPropagation();
        }

        void EndGroupResize(VisualElement handle, PointerUpEvent evt) {
            if (handle.HasPointerCapture(evt.pointerId))
                handle.ReleasePointer(evt.pointerId);
            if (_resizeGroup != null) EditorUtility.SetDirty(_resizeGroup);
            _resizeGroup = null;
            evt.StopPropagation();
        }

        void OnViewportContext(ContextualMenuPopulateEvent evt) {
            evt.menu.ClearItems();
            Vector2 panelPos = evt.mousePosition;
            if (evt.currentTarget is VisualElement current)
                panelPos = current.LocalToWorld(evt.localMousePosition);
            PortView port = FindPortAt(panelPos);
            if (port != null) {
                FillPortMenu(evt.menu, port.Port);
                evt.StopPropagation();
                return;
            }

            NodeView node = FindNodeAt(panelPos);
            if (node != null) {
                FillNodeMenu(evt.menu, node);
                evt.StopPropagation();
                return;
            }

            FillCreateMenu(evt.menu, panelPos);
            evt.StopPropagation();
        }

        void FillCreateMenu(DropdownMenu menu, Vector2 panelPos) {
            if (Window.graphEditor == null) return;
            Vector2 grid = ScreenToGrid(panelPos);
            SplitCreateEntries(GetCreateEntries(null), out var groups, out var rest);
            foreach (var entry in groups) {
                Type captured = entry.type;
                menu.AppendAction(entry.path, _ => {
                    Window.graphEditor.CreateNode(captured, grid);
                    Rebuild();
                });
            }
            if (groups.Count > 0 && rest.Count > 0) menu.AppendSeparator();
            bool any = groups.Count > 0;
            foreach (var entry in rest) {
                Type captured = entry.type;
                menu.AppendAction(entry.path, _ => {
                    Window.graphEditor.CreateNode(captured, grid);
                    Rebuild();
                });
                any = true;
            }
            if (any) menu.AppendSeparator();
            bool canPaste = NodeEditorWindow.copyBuffer != null && NodeEditorWindow.copyBuffer.Length > 0;
            menu.AppendAction("Paste", _ => {
                Window.PasteNodes(grid);
                Rebuild();
            }, canPaste ? DropdownMenuAction.AlwaysEnabled : DropdownMenuAction.AlwaysDisabled);
            menu.AppendAction("Preferences", _ => NodeEditorReflection.OpenPreferences());
        }

        List<(string path, Type type)> GetCreateEntries(Type compatibleType) {
            var result = new List<(string path, Type type)>();
            if (Window.graphEditor == null) return result;
            IEnumerable<Type> types = NodeEditorReflection.nodeTypes;
            if (compatibleType != null && NodeEditorPreferences.GetSettings().createFilter) {
                types = NodeEditorUtilities.GetCompatibleNodesTypes(
                    NodeEditorReflection.nodeTypes,
                    compatibleType,
                    NodePort.IO.Input);
            }
            foreach (Type type in types.OrderBy(t => Window.graphEditor.GetNodeMenuOrder(t))) {
                string path = Window.graphEditor.GetNodeMenuName(type);
                if (string.IsNullOrEmpty(path)) continue;
                result.Add((path, type));
            }
            return result;
        }

        static void SplitCreateEntries(
            List<(string path, Type type)> entries,
            out List<(string path, Type type)> groups,
            out List<(string path, Type type)> rest) {
            groups = new List<(string path, Type type)>();
            rest = new List<(string path, Type type)>();
            for (int i = 0; i < entries.Count; i++) {
                if (typeof(GroupNode).IsAssignableFrom(entries[i].type)) groups.Add(entries[i]);
                else rest.Add(entries[i]);
            }
        }

        void ShowCreateMenu(Vector2 panelPos, NodePort output) {
            var menu = new GenericMenu();
            Vector2 grid = ScreenToGrid(panelPos);
            NodePort capturedOutput = output;
            SplitCreateEntries(
                GetCreateEntries(output != null ? output.ValueType : null),
                out var groups,
                out var rest);
            bool any = false;
            void AddEntry((string path, Type type) entry) {
                Type capturedType = entry.type;
                menu.AddItem(new GUIContent(entry.path), false, () => {
                    Node node = Window.graphEditor.CreateNode(capturedType, grid);
                    if (node != null && capturedOutput != null) {
                        NodePort input = Window.graphEditor.GetCompatibleInput(capturedOutput, node);
                        if (input != null) capturedOutput.Connect(input);
                    }
                    Rebuild();
                });
                any = true;
            }
            for (int i = 0; i < groups.Count; i++) AddEntry(groups[i]);
            if (groups.Count > 0 && rest.Count > 0) menu.AddSeparator("");
            for (int i = 0; i < rest.Count; i++) AddEntry(rest[i]);
            if (!any) return;
            menu.AddSeparator("");
            bool canPaste = NodeEditorWindow.copyBuffer != null && NodeEditorWindow.copyBuffer.Length > 0;
            if (canPaste) {
                menu.AddItem(new GUIContent("Paste"), false, () => {
                    Window.PasteNodes(grid);
                    Rebuild();
                });
            } else {
                menu.AddDisabledItem(new GUIContent("Paste"));
            }
            menu.AddItem(new GUIContent("Preferences"), false, () => NodeEditorReflection.OpenPreferences());
            _root.schedule.Execute(() => menu.DropDown(new Rect(panelPos.x, panelPos.y, 0, 0)));
        }

        void FillNodeMenu(DropdownMenu menu, NodeView view) {
            Window.SelectNode(view.Node, false);
            menu.AppendAction("Rename", _ => Window.RenameSelectedNode());
            menu.AppendAction("Copy", _ => Window.CopySelectedNodes());
            menu.AppendAction("Duplicate", _ => {
                Window.DuplicateSelectedNodes();
                Rebuild();
            });
            if (view.Node is GroupNode group) {
                GroupNode captured = group;
                menu.AppendAction("Select Contents", _ => {
                    var list = new List<Object> { captured };
                    List<Node> inside = captured.GetNodes(NodeSize);
                    for (int i = 0; i < inside.Count; i++)
                        if (inside[i] != null) list.Add(inside[i]);
                    Selection.objects = list.ToArray();
                });
            }
            menu.AppendAction("Remove", _ => {
                Window.RemoveSelectedNodes();
                Rebuild();
            });
        }

        void FillPortMenu(DropdownMenu menu, NodePort port) {
            if (port == null) return;
            foreach (NodePort other in port.GetConnections()) {
                NodePort captured = other;
                menu.AppendAction($"Disconnect ({other.node.name})", _ => {
                    port.Disconnect(captured);
                    EditorUtility.SetDirty(Window.graph);
                    Rebuild();
                });
            }
            menu.AppendAction("Clear Connections", _ => {
                port.ClearConnections();
                EditorUtility.SetDirty(Window.graph);
                Rebuild();
            });
        }

        void FrameSelection() {
            var nodes = Selection.objects.OfType<Node>().Where(n => n != null).ToList();
            if (nodes.Count == 0) FrameAll();
            else FrameNodes(nodes);
        }

        void FrameAll() {
            if (Window.graph == null) return;
            FrameNodes(Window.graph.nodes.Where(n => n != null).ToList());
        }

        void FrameNodes(List<Node> nodes) {
            if (nodes == null || nodes.Count == 0) {
                Window.zoom = 1f;
                Window.panOffset = Vector2.zero;
                ApplyView();
                return;
            }

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < nodes.Count; i++) {
                Node node = nodes[i];
                Vector2 size = Window.nodeSizes.TryGetValue(node, out Vector2 cached)
                    ? cached
                    : new Vector2(208f, 80f);
                min = Vector2.Min(min, node.position);
                max = Vector2.Max(max, node.position + size);
            }

            Vector2 bounds = Vector2.Max(max - min, new Vector2(80f, 80f));
            Vector2 viewSize = _viewport.contentRect.size;
            if (viewSize.x < 8f) viewSize = Window.position.size;
            float padding = 80f;
            float fitScale = Mathf.Min(
                (viewSize.x - padding) / bounds.x,
                (viewSize.y - padding) / bounds.y);
            float zoom = 1f / Mathf.Max(0.05f, fitScale);
            NodeEditorPreferences.SharedSettings settings = NodeEditorPreferences.GetShared();
            Window.zoom = Mathf.Clamp(zoom, settings.minZoom, settings.maxZoom);
            float s = 1f / Window.zoom;
            Vector2 center = (min + max) * 0.5f;
            Window.panOffset = viewSize * 0.5f - center * s;
            ApplyView();
        }

        void RebuildRerouteHandles() {
            _rerouteLayer.Clear();
            if (Window.graph?.nodes == null) return;
            foreach (Node node in Window.graph.nodes) {
                if (node == null) continue;
                foreach (NodePort output in node.Outputs) {
                    for (int i = 0; i < output.ConnectionCount; i++) {
                        List<Vector2> points = output.GetReroutePoints(i);
                        if (points == null) continue;
                        for (int r = 0; r < points.Count; r++) {
                            var handle = CreateRerouteHandle(output, i, r, points[r]);
                            _rerouteLayer.Add(handle);
                        }
                    }
                }
            }
        }

        VisualElement CreateRerouteHandle(NodePort port, int connectionIndex, int pointIndex, Vector2 grid) {
            var handle = new VisualElement();
            handle.AddToClassList("reroute-handle");
            handle.pickingMode = PickingMode.Position;
            handle.style.left = grid.x - 5f;
            handle.style.top = grid.y - 5f;
            var reference = new RerouteReference(port, connectionIndex, pointIndex);
            handle.RegisterCallback<PointerDownEvent>(evt => {
                if (evt.button != 0) return;
                _dragReroute = reference;
                _rerouteDrag = true;
                _rerouteOrigin = reference.GetPoint();
                handle.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });
            handle.RegisterCallback<PointerMoveEvent>(evt => {
                if (!_rerouteDrag || !handle.HasPointerCapture(evt.pointerId)) return;
                Vector2 next = ScreenToGrid(evt.position);
                bool gridSnap = NodeEditorPreferences.GetSettings().gridSnap;
                if (evt.ctrlKey) gridSnap = !gridSnap;
                if (gridSnap) next = GridElement.Snap(next, SnapStep());
                reference.SetPoint(next);
                handle.style.left = next.x - 5f;
                handle.style.top = next.y - 5f;
                _noodles.MarkDirtyRepaint();
            });
            handle.RegisterCallback<PointerUpEvent>(evt => {
                if (handle.HasPointerCapture(evt.pointerId))
                    handle.ReleasePointer(evt.pointerId);
                if (_rerouteDrag) EditorUtility.SetDirty(port.node);
                _rerouteDrag = false;
            });
            handle.RegisterCallback<ContextualMenuPopulateEvent>(evt => {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Remove Reroute"), false, () => {
                    reference.RemovePoint();
                    EditorUtility.SetDirty(port.node);
                    Rebuild();
                });
                menu.ShowAsContext();
                evt.StopPropagation();
            });
            return handle;
        }

        bool TryInsertReroute(Vector2 grid) {
            const float maxDist = 18f;
            NodePort bestPort = null;
            int bestConnection = -1;
            int bestInsert = -1;
            float bestDist = maxDist;
            foreach (Node node in Window.graph.nodes) {
                if (node == null) continue;
                foreach (NodePort output in node.Outputs) {
                    if (!Ports.TryGetValue(output, out PortView fromView)) continue;
                    for (int i = 0; i < output.ConnectionCount; i++) {
                        NodePort input = output.GetConnection(i);
                        if (input == null || !Ports.TryGetValue(input, out PortView toView)) continue;
                        var points = new List<Vector2> { fromView.NodeView.Node.position };
                        points[0] = ScreenToGrid(fromView.worldBound.center);
                        List<Vector2> reroute = output.GetReroutePoints(i);
                        if (reroute != null) points.AddRange(reroute);
                        points.Add(ScreenToGrid(toView.worldBound.center));
                        for (int s = 0; s < points.Count - 1; s++) {
                            float dist = DistanceToSegment(grid, points[s], points[s + 1]);
                            if (dist >= bestDist) continue;
                            bestDist = dist;
                            bestPort = output;
                            bestConnection = i;
                            bestInsert = s;
                        }
                    }
                }
            }
            if (bestPort == null) return false;
            Undo.RecordObject(bestPort.node, "Add Reroute");
            bestPort.GetReroutePoints(bestConnection).Insert(bestInsert, grid);
            EditorUtility.SetDirty(bestPort.node);
            RebuildRerouteHandles();
            _noodles.MarkDirtyRepaint();
            return true;
        }

        static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b) {
            Vector2 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            if (lengthSq < 0.0001f) return Vector2.Distance(point, a);
            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSq);
            return Vector2.Distance(point, a + ab * t);
        }

        public void ApplyView() {
            float s = 1f / Mathf.Max(0.01f, Window.zoom);
            _content.style.translate = new Translate(Window.panOffset.x, Window.panOffset.y);
            _content.style.scale = new Scale(new Vector3(s, s, 1f));
            _zoomSlider.SetValueWithoutNotify(Window.zoom);
            _zoomLabel.text = Window.zoom.ToString("0.0#x");
            NodeEditorPreferences.SharedSettings shared = NodeEditorPreferences.GetShared();
            _snapButton?.EnableInClassList("active", shared.gridSnap);
            _indexButton?.EnableInClassList("active", shared.showNodeIndices);
            ApplyGridColors(shared);
            _grid.Pan = Window.panOffset;
            _grid.Scale = s;
            _grid.MarkDirtyRepaint();
            _noodles.MarkDirtyRepaint();
            CullNodes();
        }

        void ApplyVisualPrefs() {
            if (_grid == null) return;
            NodeEditorPreferences.SharedSettings shared = NodeEditorPreferences.GetShared();
            _zoomSlider.lowValue = shared.minZoom;
            _zoomSlider.highValue = shared.maxZoom;
            Window.zoom = Mathf.Clamp(Window.zoom, shared.minZoom, shared.maxZoom);
            Window.graphEditor?.ApplyStyles(_root);
            Color selection = shared.selectionColor;
            foreach (var pair in Nodes)
                pair.Value.ApplySelectionColor(selection);
            if (Window.graphEditor != null) {
                foreach (var pair in Ports) {
                    pair.Value.ApplyColor(Window.graphEditor.GetPortColor(pair.Key));
                    pair.Value.ApplyTooltip(PortTooltip(pair.Key));
                }
            }
            RefreshNodeIndices();
            ApplyView();
        }

        void ApplyGridColors(NodeEditorPreferences.SharedSettings shared) {
            if (_grid == null || shared == null) return;
            _grid.BackgroundColor = shared.gridBgColor;
            _grid.MinorColor = shared.gridMinorColor;
            _grid.MajorColor = shared.gridMajorColor;
            _grid.OriginColor = shared.gridOriginColor;
            _grid.MinorStep = shared.gridMinorStep;
            _grid.MajorStep = shared.gridMajorStep;
            _grid.style.backgroundColor = (Color)shared.gridBgColor;
        }

        string PortTooltip(NodePort port) {
            if (port == null || !NodeEditorPreferences.GetShared().portTooltips) return "";
            return Window.graphEditor != null ? Window.graphEditor.GetPortTooltip(port) : "";
        }

        static float SnapStep() {
            return Mathf.Max(1f, NodeEditorPreferences.GetShared().gridMinorStep);
        }

        void UpdateCursorLabel(Vector2 panelPosition) {
            if (_cursorLabel == null) return;
            Vector2 grid = ScreenToGrid(panelPosition);
            _cursorLabel.text = $"{Mathf.RoundToInt(grid.x)}, {Mathf.RoundToInt(grid.y)}";
        }

        static Label ToolbarLabel(string text) {
            var label = new Label(text);
            label.AddToClassList("toolbar-label");
            return label;
        }

        public Vector2 ScreenToGrid(Vector2 panelPosition) {
            Vector2 local = _viewport.WorldToLocal(panelPosition);
            float s = 1f / Mathf.Max(0.01f, Window.zoom);
            return (local - Window.panOffset) / s;
        }

        public void PaintNoodles(MeshGenerationContext ctx) {
            if (Window.graph == null || Window.graphEditor == null) return;
            Painter2D painter = ctx.painter2D;
            foreach (Node node in Window.graph.nodes) {
                if (node == null) continue;
                foreach (NodePort output in node.Outputs) {
                    if (!Ports.TryGetValue(output, out PortView fromView)) continue;
                    for (int i = 0; i < output.ConnectionCount; i++) {
                        NodePort input = output.GetConnection(i);
                        if (input == null || !Ports.TryGetValue(input, out PortView toView)) continue;
                        Gradient gradient = Window.graphEditor.GetNoodleGradient(output, input);
                        float thickness = Window.graphEditor.GetNoodleThickness(output, input);
                        NoodlePath path = Window.graphEditor.GetNoodlePath(output, input);
                        NoodleStroke stroke = Window.graphEditor.GetNoodleStroke(output, input);
                        Vector2 start = fromView.CenterIn(_noodles);
                        Vector2 end = toView.CenterIn(_noodles);
                        List<Vector2> reroute = output.GetReroutePoints(i);
                        var points = new List<Vector2> { start };
                        if (reroute != null) {
                            for (int r = 0; r < reroute.Count; r++)
                                points.Add(GridToNoodle(reroute[r]));
                        }
                        points.Add(end);
                        DrawPath(
                            painter,
                            points,
                            gradient,
                            thickness,
                            path,
                            stroke,
                            Window.graphEditor.GetNoodleTension(output, input),
                            Window.graphEditor.GetNoodlePortDirection(output),
                            Window.graphEditor.GetNoodlePortDirection(input));
                    }
                }
            }

            if (_dragPort != null && Ports.TryGetValue(_dragPort, out PortView dragView)) {
                Vector2 start = dragView.CenterIn(_noodles);
                Vector2 end = _noodles.WorldToLocal(_dragPointer);
                Color color = Window.graphEditor.GetPortColor(_dragPort);
                color.a = 0.7f;
                var dragGrad = new Gradient();
                dragGrad.SetKeys(
                    new[] { new GradientColorKey(color, 0f) },
                    new[] { new GradientAlphaKey(color.a, 0f) });
                DrawPath(
                    painter,
                    new List<Vector2> { start, end },
                    dragGrad,
                    Window.graphEditor.GetNoodleThickness(_dragPort, null),
                    Window.graphEditor.GetNoodlePath(_dragPort, null),
                    NoodleStroke.Full,
                    Window.graphEditor.GetNoodleTension(_dragPort, null),
                    Window.graphEditor.GetNoodlePortDirection(_dragPort),
                    -Window.graphEditor.GetNoodlePortDirection(_dragPort));
            }
        }

        Vector2 GridToNoodle(Vector2 grid) {
            float s = 1f / Mathf.Max(0.01f, Window.zoom);
            Vector2 viewport = grid * s + Window.panOffset;
            return _noodles.WorldToLocal(_viewport.LocalToWorld(viewport));
        }

        static void DrawPath(
            Painter2D painter,
            List<Vector2> points,
            Gradient gradient,
            float thickness,
            NoodlePath path,
            NoodleStroke stroke,
            float tension,
            Vector2 startDir,
            Vector2 endDir) {
            if (points == null || points.Count < 2) return;
            painter.lineWidth = Mathf.Max(1f, thickness);
            painter.lineCap = LineCap.Round;
            if (stroke == NoodleStroke.Dashed) painter.SetDashPattern(new[] { 6f, 4f });
            else painter.SetDashPattern(Array.Empty<float>());
            float pull = Mathf.Clamp(tension, 0.05f, 1f);

            Color startColor = gradient != null ? gradient.Evaluate(0f) : Color.white;
            Color endColor = gradient != null ? gradient.Evaluate(1f) : startColor;
            bool solid = gradient == null || Approximately(startColor, endColor);

            if (solid) {
                painter.strokeColor = startColor;
                painter.BeginPath();
                painter.MoveTo(points[0]);
                TracePath(painter, points, path, startDir, endDir, pull);
                painter.Stroke();
                return;
            }

            var samples = new List<Vector2>(32);
            SamplePath(points, path, startDir, endDir, pull, samples);
            if (samples.Count < 2) return;
            float inv = 1f / (samples.Count - 1);
            for (int i = 0; i < samples.Count - 1; i++) {
                painter.strokeColor = gradient.Evaluate((i + 0.5f) * inv);
                painter.BeginPath();
                painter.MoveTo(samples[i]);
                painter.LineTo(samples[i + 1]);
                painter.Stroke();
            }
        }

        static void TracePath(
            Painter2D painter,
            List<Vector2> points,
            NoodlePath path,
            Vector2 startDir,
            Vector2 endDir,
            float tension) {
            if (path == NoodlePath.Straight || points.Count > 2) {
                for (int i = 1; i < points.Count; i++) painter.LineTo(points[i]);
                return;
            }
            Vector2 a = points[0];
            Vector2 b = points[1];
            if (path == NoodlePath.Angled) {
                Vector2 mid = new Vector2((a.x + b.x) * 0.5f, a.y);
                painter.LineTo(mid);
                painter.LineTo(new Vector2(mid.x, b.y));
                painter.LineTo(b);
                return;
            }
            float dist = Vector2.Distance(a, b);
            Vector2 c1 = a + startDir.normalized * dist * tension;
            Vector2 c2 = b + endDir.normalized * dist * tension;
            painter.BezierCurveTo(c1, c2, b);
        }

        static void SamplePath(
            List<Vector2> points,
            NoodlePath path,
            Vector2 startDir,
            Vector2 endDir,
            float tension,
            List<Vector2> samples) {
            samples.Clear();
            if (path == NoodlePath.Straight || points.Count > 2) {
                samples.AddRange(points);
                return;
            }
            Vector2 a = points[0];
            Vector2 b = points[1];
            if (path == NoodlePath.Angled) {
                samples.Add(a);
                samples.Add(new Vector2((a.x + b.x) * 0.5f, a.y));
                samples.Add(new Vector2((a.x + b.x) * 0.5f, b.y));
                samples.Add(b);
                return;
            }
            float dist = Vector2.Distance(a, b);
            Vector2 c1 = a + startDir.normalized * dist * tension;
            Vector2 c2 = b + endDir.normalized * dist * tension;
            int steps = Mathf.Clamp(Mathf.RoundToInt(dist / 14f), 8, 28);
            for (int i = 0; i <= steps; i++)
                samples.Add(EvaluateBezier(a, c1, c2, b, i / (float)steps));
        }

        static Vector2 EvaluateBezier(Vector2 a, Vector2 c1, Vector2 c2, Vector2 b, float t) {
            float u = 1f - t;
            return (u * u * u * a) + (3f * u * u * t * c1) + (3f * u * t * t * c2) + (t * t * t * b);
        }

        static bool Approximately(Color a, Color b) {
            return Mathf.Abs(a.r - b.r) < 0.004f
                && Mathf.Abs(a.g - b.g) < 0.004f
                && Mathf.Abs(a.b - b.b) < 0.004f
                && Mathf.Abs(a.a - b.a) < 0.004f;
        }
    }
}
