using System;
using UnityEngine;

namespace XNode
{
    [Serializable]
    public sealed class QuaternionVariable : BlackboardVariable<Quaternion> { }

    [Node.CreateNodeMenu("Blackboard/Get/Quaternion")]
    public sealed class GetQuaternion : GetBlackboardVariableNode<Quaternion> { }
}
