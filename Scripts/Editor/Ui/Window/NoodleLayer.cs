using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using XNode;

namespace XNodeEditor.Ui {
    public sealed class NoodleLayer : VisualElement {
        public GraphUiController Controller;

        public NoodleLayer() {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        void OnGenerateVisualContent(MeshGenerationContext ctx) {
            if (Controller == null) return;
            Controller.PaintNoodles(ctx);
        }
    }
}
