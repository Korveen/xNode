using System;
using System.Collections.Generic;

namespace XNode {
    /// <summary>Mutable per-execution values created from a Blackboard definition.</summary>
    public sealed class BlackboardInstance {
        private readonly BlackboardRuntimeValue[] values;
        private readonly string[] ids;
        private readonly Dictionary<string, int> indicesById;
        private readonly Dictionary<string, int> indicesByName;

        public event Action<string> ValueChanged;

        internal BlackboardInstance(BlackboardDefinition definition) {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            IReadOnlyList<BlackboardVariable> definitions = definition.Variables;
            values = new BlackboardRuntimeValue[definitions.Count];
            ids = new string[definitions.Count];
            indicesById = new Dictionary<string, int>(definitions.Count, StringComparer.Ordinal);
            indicesByName = new Dictionary<string, int>(
                definitions.Count, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < definitions.Count; i++) {
                BlackboardVariable variable = definitions[i];
                if (variable == null) {
                    throw new InvalidOperationException($"Blackboard variable at index {i} is null.");
                }

                if (!indicesById.TryAdd(variable.Id, i)) {
                    throw new InvalidOperationException(
                        $"Blackboard contains duplicate variable id '{variable.Id}'.");
                }

                if (!indicesByName.TryAdd(variable.Name, i)) {
                    throw new InvalidOperationException(
                        $"Blackboard contains duplicate variable name '{variable.Name}'.");
                }

                values[i] = variable.CreateRuntimeValue();
                ids[i] = variable.Id;
            }
        }

        public T Get<T>(string id) {
            if (!TryGet(id, out T value)) {
                throw CreateLookupException<T>(id);
            }

            return value;
        }

        public T GetByName<T>(string name) {
            if (!TryGetByName(name, out T value)) {
                throw new KeyNotFoundException(
                    $"Blackboard variable named '{name}' with type {typeof(T).FullName} was not found.");
            }

            return value;
        }

        public bool TryGet<T>(string id, out T value) {
            return TryGet(indicesById, id, out value);
        }

        public bool TryGetByName<T>(string name, out T value) {
            return TryGet(indicesByName, name, out value);
        }

        public void Set<T>(string id, T value) {
            if (!indicesById.TryGetValue(id, out int index)) {
                throw new KeyNotFoundException($"Blackboard variable id '{id}' was not found.");
            }

            if (!(values[index] is BlackboardRuntimeValue<T> typedValue)) {
                throw new InvalidOperationException(
                    $"Blackboard variable '{id}' stores {values[index].ValueType.FullName}, " +
                    $"not {typeof(T).FullName}.");
            }

            if (EqualityComparer<T>.Default.Equals(typedValue.Value, value)) return;

            typedValue.Value = value;
            ValueChanged?.Invoke(id);
        }

        public bool TrySet<T>(string id, T value) {
            if (!indicesById.TryGetValue(id, out int index)) return false;
            return TrySetValueAt(index, id, value);
        }

        public void SetByName<T>(string name, T value) {
            if (!TrySetByName(name, value)) {
                throw CreateNameLookupException<T>(name);
            }
        }

        public bool TrySetByName<T>(string name, T value) {
            if (name == null || !indicesByName.TryGetValue(name, out int index)) return false;
            return TrySetValueAt(index, name, value);
        }

        public bool TryGetBoxed(string id, out object value) {
            if (id != null && indicesById.TryGetValue(id, out int index)) {
                value = values[index].GetBoxed();
                return true;
            }

            value = null;
            return false;
        }

        public bool TrySetBoxed(string id, object value) {
            if (id == null || !indicesById.TryGetValue(id, out int index)) return false;
            if (!values[index].TrySetBoxed(value, out bool changed)) return false;
            if (changed) ValueChanged?.Invoke(id);
            return true;
        }

        public void Reset() {
            for (int i = 0; i < values.Length; i++) {
                values[i].Reset();
                ValueChanged?.Invoke(ids[i]);
            }
        }

        private bool TryGet<T>(
            Dictionary<string, int> indices,
            string key,
            out T value) {
            if (key != null &&
                indices.TryGetValue(key, out int index) &&
                values[index] is BlackboardRuntimeValue<T> typedValue) {
                value = typedValue.Value;
                return true;
            }

            value = default(T);
            return false;
        }

        private bool TrySetValueAt<T>(int index, string key, T value) {
            if (!(values[index] is BlackboardRuntimeValue<T> typedValue)) return false;
            if (EqualityComparer<T>.Default.Equals(typedValue.Value, value)) return true;

            typedValue.Value = value;
            ValueChanged?.Invoke(key);
            return true;
        }

        private void SetValueAt<T>(int index, string key, T value) {
            if (!TrySetValueAt(index, key, value)) {
                throw new InvalidOperationException(
                    $"Blackboard variable '{key}' stores {values[index].ValueType.FullName}, " +
                    $"not {typeof(T).FullName}.");
            }
        }

        private Exception CreateLookupException<T>(string id) {
            if (!indicesById.TryGetValue(id, out int index)) {
                return new KeyNotFoundException($"Blackboard variable id '{id}' was not found.");
            }

            return new InvalidOperationException(
                $"Blackboard variable '{id}' stores {values[index].ValueType.FullName}, " +
                $"not {typeof(T).FullName}.");
        }

        private Exception CreateNameLookupException<T>(string name) {
            if (name == null || !indicesByName.TryGetValue(name, out int index)) {
                return new KeyNotFoundException(
                    $"Blackboard variable named '{name}' with type {typeof(T).FullName} was not found.");
            }

            return new InvalidOperationException(
                $"Blackboard variable '{name}' stores {values[index].ValueType.FullName}, " +
                $"not {typeof(T).FullName}.");
        }
    }
}
