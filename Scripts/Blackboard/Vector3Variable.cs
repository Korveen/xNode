using System;
using UnityEngine;

namespace XNode
{
    [Serializable]
    public sealed class Vector3Variable : BlackboardVariable<Vector3> { }

    [Node.CreateNodeMenu("Blackboard/Get/Vector3")]
    public sealed class GetVector3 : GetBlackboardVariableNode<Vector3> { }
}
