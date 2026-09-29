using System;

namespace XNode
{
    [Serializable]
    public sealed class BoolVariable : BlackboardVariable<bool> { }

    [Node.CreateNodeMenu("Blackboard/Get/Bool")]
    public sealed class GetBool : GetBlackboardVariableNode<bool> { }
}
