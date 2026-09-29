using System;
using UnityEngine;

namespace XNode
{
    [Serializable]
    public sealed class ColorVariable : BlackboardVariable<Color> { }

    [Node.CreateNodeMenu("Blackboard/Get/Color")]
    public sealed class GetColor : GetBlackboardVariableNode<Color> { }
}
