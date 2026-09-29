using System;

namespace XNode
{
    [Serializable]
    public sealed class ObjectVariable : BlackboardVariable<UnityEngine.Object> { }

    [Node.CreateNodeMenu("Blackboard/Get/Object")]
    public sealed class GetObject : GetBlackboardVariableNode<UnityEngine.Object> { }
}
