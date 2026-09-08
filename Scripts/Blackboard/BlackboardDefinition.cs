using System;
using System.Collections.Generic;
using UnityEngine;

namespace XNode {
    /// <summary>Serializable Blackboard schema stored on a graph asset.</summary>
    [Serializable]
    public sealed class BlackboardDefinition : ISerializationCallbackReceiver {
        [SerializeReference] private List<BlackboardVariable> variables =
            new List<BlackboardVariable>();

        public IReadOnlyList<BlackboardVariable> Variables => variables;

        public BlackboardInstance CreateInstance() {
            EnsureValidIdentities();
            return new BlackboardInstance(this);
        }

        public bool TryGetVariable(string id, out BlackboardVariable variable) {
            if (!string.IsNullOrEmpty(id)) {
                for (int i = 0; i < variables.Count; i++) {
                    BlackboardVariable candidate = variables[i];
                    if (candidate != null && candidate.Id == id) {
                        variable = candidate;
                        return true;
                    }
                }
            }

            variable = null;
            return false;
        }

        public bool TryGetDefault<T>(string id, out T value) {
            if (TryGetVariable(id, out BlackboardVariable variable)) {
                return variable.TryGetDefault(out value);
            }

            value = default(T);
            return false;
        }

        public T Add<T>(string variableName = null) where T : BlackboardVariable, new() {
            T variable = new T();
            variable.EnsureIdentity();
            variable.SetName(GetUniqueName(
                string.IsNullOrWhiteSpace(variableName) ? variable.ValueType.Name : variableName));
            variables.Add(variable);
            return variable;
        }

        public bool Remove(string id) {
            for (int i = 0; i < variables.Count; i++) {
                if (variables[i] != null && variables[i].Id == id) {
                    variables.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public bool Rename(string id, string newName) {
            if (!TryGetVariable(id, out BlackboardVariable variable)) return false;
            if (string.IsNullOrWhiteSpace(newName)) return false;

            string trimmedName = newName.Trim();
            for (int i = 0; i < variables.Count; i++) {
                BlackboardVariable other = variables[i];
                if (other == null || other == variable) continue;
                if (string.Equals(other.Name, trimmedName, StringComparison.OrdinalIgnoreCase)) {
                    return false;
                }
            }

            variable.SetName(trimmedName);
            return true;
        }

        public string GetUniqueName(string requestedName) {
            string baseName = string.IsNullOrWhiteSpace(requestedName)
                ? "Variable"
                : requestedName.Trim();
            string candidate = baseName;
            int suffix = 1;

            while (ContainsName(candidate)) {
                candidate = $"{baseName} {suffix++}";
            }

            return candidate;
        }

        public void OnBeforeSerialize() {
            EnsureValidIdentities();
        }

        public void OnAfterDeserialize() {
            if (variables == null) variables = new List<BlackboardVariable>();
            EnsureValidIdentities();
        }

        private bool ContainsName(string variableName) {
            for (int i = 0; i < variables.Count; i++) {
                BlackboardVariable variable = variables[i];
                if (variable != null &&
                    string.Equals(variable.Name, variableName, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            return false;
        }

        private void EnsureValidIdentities() {
            if (variables == null) variables = new List<BlackboardVariable>();
            for (int i = 0; i < variables.Count; i++) {
                variables[i]?.EnsureIdentity();
            }
        }
    }
}
