using System;

namespace XNode
{
    [Serializable]
    public sealed class StringVariable : BlackboardVariable<string> { }

    [Node.CreateNodeMenu("Blackboard/Get/String")]
    public sealed class GetString : GetBlackboardVariableNode<string> { }
}
