using System;
using System.Collections.Generic;
using UnityEngine;

namespace XNode {
    /// <summary>Per-runner override of one Blackboard variable default.</summary>
    [Serializable]
    public sealed class BlackboardOverride {
        [SerializeField] private string variableId;
        [SerializeField] private bool enabled = true;
        [SerializeReference] private BlackboardVariable value;

        public string VariableId => variableId;
        public bool Enabled => enabled;
        public BlackboardVariable Value => value;

        public BlackboardOverride() { }

        public BlackboardOverride(BlackboardVariable definition) {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            variableId = definition.Id;
            enabled = true;
            value = definition.CloneDefinition();
        }

        internal void SetEnabled(bool isEnabled) => enabled = isEnabled;

        internal void ReplaceValue(BlackboardVariable definition) {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            variableId = definition.Id;
            value = definition.CloneDefinition();
        }
    }

    /// <summary>Serialized instance defaults applied on context create and Blackboard reset.</summary>
    [Serializable]
    public sealed class BlackboardOverrideSet {
        [SerializeField] private List<BlackboardOverride> overrides = new List<BlackboardOverride>();

        public IReadOnlyList<BlackboardOverride> Overrides => overrides;

        public bool TryGet(string variableId, out BlackboardOverride overrideValue) {
            int index = IndexOf(variableId);
            if (index >= 0) {
                overrideValue = overrides[index];
                return true;
            }

            overrideValue = null;
            return false;
        }

        public BlackboardOverride Enable(BlackboardVariable definition) {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            int index = IndexOf(definition.Id);
            if (index >= 0) {
                BlackboardOverride existing = overrides[index];
                if (existing.Value == null || existing.Value.ValueType != definition.ValueType) {
                    existing.ReplaceValue(definition);
                }
                existing.SetEnabled(true);
                return existing;
            }

            var created = new BlackboardOverride(definition);
            overrides.Add(created);
            return created;
        }

        public void Disable(string variableId) {
            int index = IndexOf(variableId);
            if (index >= 0) overrides[index].SetEnabled(false);
        }

        public bool Remove(string variableId) {
            int index = IndexOf(variableId);
            if (index < 0) return false;
            overrides.RemoveAt(index);
            return true;
        }

        public void Set<T>(BlackboardVariable definition, T value) {
            BlackboardOverride overrideValue = Enable(definition);
            overrideValue.Value.SetBoxedDefault(value);
        }

        public void Apply(BlackboardInstance instance) {
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            for (int i = 0; i < overrides.Count; i++) {
                BlackboardOverride overrideValue = overrides[i];
                if (overrideValue == null ||
                    !overrideValue.Enabled ||
                    overrideValue.Value == null) {
                    continue;
                }

                instance.TrySetBoxed(overrideValue.VariableId, overrideValue.Value.GetBoxedDefault());
            }
        }

        private int IndexOf(string variableId) {
            if (string.IsNullOrEmpty(variableId)) return -1;
            for (int i = 0; i < overrides.Count; i++) {
                if (overrides[i] != null && overrides[i].VariableId == variableId) return i;
            }
            return -1;
        }
    }
}
