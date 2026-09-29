using System;
using UnityEngine;

namespace XNode {
    /// <summary>Rename-safe reference limited to Blackboard variables of type <typeparamref name="T"/>.</summary>
    [Serializable]
    public struct BlackboardVariableReference<T> {
        [SerializeField] private string variableId;

        public string VariableId => variableId;
        public bool IsAssigned => !string.IsNullOrEmpty(variableId);

        public BlackboardVariableReference(string variableId) {
            this.variableId = variableId;
        }
    }
}
