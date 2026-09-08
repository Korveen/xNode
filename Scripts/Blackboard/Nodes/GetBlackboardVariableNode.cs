using UnityEngine;

namespace XNode
{
    public abstract class GetBlackboardVariableNode<T> : Node, IAuxiliaryGraphNode
    {
        [SerializeField] private BlackboardVariableReference variable;
        [Output(ShowBackingValue.Never)] public T value;

        public BlackboardVariableReference Variable
        {
            get => variable;
            set => variable = value;
        }

        public sealed override object GetValue(NodePort port)
        {
            if (port == null || port.fieldName != nameof(value)) return null;
            return graph != null &&
                   graph.BlackboardDefinition.TryGetDefault(variable.VariableId, out T defaultValue)
                ? defaultValue
                : default;
        }

        public sealed override object GetValue(NodePort port, GraphExecutionContext context)
        {
            if (port == null || port.fieldName != nameof(value)) return null;
            if (context == null) return GetValue(port);
            return context.Blackboard.TryGet(variable.VariableId, out T runtimeValue)
                ? runtimeValue
                : default;
        }
    }
}
