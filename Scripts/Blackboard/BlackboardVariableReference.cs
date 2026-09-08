using System;
using UnityEngine;

namespace XNode {
    /// <summary>Rename-safe reference to a Blackboard variable definition.</summary>
    [Serializable]
    public struct BlackboardVariableReference {
        [SerializeField] private string variableId;

        public string VariableId => variableId;
        public bool IsAssigned => !string.IsNullOrEmpty(variableId);

        public BlackboardVariableReference(string variableId) {
            this.variableId = variableId;
        }
    }
}
