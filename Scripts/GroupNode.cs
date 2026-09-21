using System.Collections.Generic;
using UnityEngine;

namespace XNode {
    [CreateNodeMenu("Group")]
    public class GroupNode : Node, IAuxiliaryGraphNode {
        public int width = 400;
        public int height = 400;
        public Color color = new Color(1f, 1f, 1f, 0.1f);

        public override object GetValue(NodePort port) {
            return null;
        }

        public List<Node> GetNodes(System.Func<Node, Vector2> sizeOf) {
            var result = new List<Node>();
            if (graph == null || graph.nodes == null || sizeOf == null) return result;
            float maxX = position.x + width;
            float maxY = position.y + height + 30f;
            for (int i = 0; i < graph.nodes.Count; i++) {
                Node node = graph.nodes[i];
                if (node == null || node == this) continue;
                Vector2 size = sizeOf(node);
                float x1 = node.position.x;
                float y1 = node.position.y;
                float x2 = x1 + Mathf.Max(1f, size.x);
                float y2 = y1 + Mathf.Max(1f, size.y);
                if (x1 < position.x || y1 < position.y) continue;
                if (x2 > maxX || y2 > maxY) continue;
                result.Add(node);
            }
            return result;
        }
    }
}
