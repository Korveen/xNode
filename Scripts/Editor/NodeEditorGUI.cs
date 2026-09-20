using System;
using UnityEngine;

namespace XNodeEditor {
    /// <summary> Window leftovers that are not the UITK canvas. </summary>
    public partial class NodeEditorWindow {
        public NodeGraphEditor graphEditor;
        /// <summary> Runs after window IMGUI. xNodeGroups and IMGUI drawers still enqueue here. </summary>
        public event Action onLateGUI;

        protected virtual void OnGUI() {
            if (onLateGUI == null) return;
            onLateGUI();
            onLateGUI = null;
        }
    }
}
