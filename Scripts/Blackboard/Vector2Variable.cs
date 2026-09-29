using System;
using UnityEngine;

namespace XNode
{
    [Serializable]
    public sealed class Vector2Variable : BlackboardVariable<Vector2> { }

    [Node.CreateNodeMenu("Blackboard/Get/Vector2")]
    public sealed class GetVector2 : GetBlackboardVariableNode<Vector2> { }
}
