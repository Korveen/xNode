using UnityEngine.UIElements;

namespace XNodeEditor.Ui {
    /// <summary>UITK shell for <see cref="NodeEditorWindow"/>. Loads UXML/USS and hosts <see cref="GraphUiController"/>.</summary>
    public sealed class GraphWindow : VisualElement {
        public GraphUiController Controller { get; }

        public GraphWindow(NodeEditorWindow window) {
            name = "graph-window";
            style.flexGrow = 1;
            pickingMode = PickingMode.Position;

            VisualTreeAsset uxml = UiAssets.LoadUxml("GraphWindow");
            StyleSheet tokens = UiAssets.LoadUss("tokens");
            StyleSheet windowSheet = UiAssets.LoadUss("GraphWindow");
            if (uxml != null) uxml.CloneTree(this);
            if (tokens != null) styleSheets.Add(tokens);
            if (windowSheet != null) styleSheets.Add(windowSheet);

            VisualElement root = this.Q("root") ?? this;
            root.style.flexGrow = 1;
            Controller = new GraphUiController(window, root);
        }
    }
}
