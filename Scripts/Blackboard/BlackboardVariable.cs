using System;
using System.Collections.Generic;
using UnityEngine;

namespace XNode {
    /// <summary>Serialized definition and default value of one Blackboard variable.</summary>
    [Serializable]
    public abstract class BlackboardVariable {
        [SerializeField] private string id = Guid.NewGuid().ToString("N");
        [SerializeField] private string name = "Variable";

        public string Id => id;
        public string Name => name;
        public abstract Type ValueType { get; }

        internal void EnsureIdentity() {
            if (string.IsNullOrEmpty(id)) id = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(name)) name = "Variable";
        }

        internal void SetName(string value) {
            name = value;
        }

        public abstract object GetBoxedDefault();
        public abstract void SetBoxedDefault(object value);
        public abstract BlackboardVariable CloneDefinition();

        internal abstract BlackboardRuntimeValue CreateRuntimeValue();
        internal abstract bool TryGetDefault<T>(out T value);
    }

    /// <summary>Base class for custom strongly typed Blackboard variables.</summary>
    [Serializable]
    public abstract class BlackboardVariable<T> : BlackboardVariable {
        [SerializeField] private T defaultValue;

        public sealed override Type ValueType => typeof(T);
        public T DefaultValue {
            get => defaultValue;
            set => defaultValue = value;
        }

        public sealed override object GetBoxedDefault() => defaultValue;

        public sealed override void SetBoxedDefault(object value) {
            if (value is T typedValue) {
                defaultValue = typedValue;
                return;
            }

            if (value == null && default(T) == null) {
                defaultValue = default(T);
                return;
            }

            throw new InvalidOperationException(
                $"Cannot assign {value?.GetType().FullName ?? "null"} to {typeof(T).FullName}.");
        }

        public sealed override BlackboardVariable CloneDefinition() {
            var clone = (BlackboardVariable<T>)Activator.CreateInstance(GetType());
            clone.DefaultValue = defaultValue;
            return clone;
        }

        internal sealed override BlackboardRuntimeValue CreateRuntimeValue() {
            return new BlackboardRuntimeValue<T>(defaultValue);
        }

        internal sealed override bool TryGetDefault<TValue>(out TValue value) {
            if (defaultValue is TValue typedValue) {
                value = typedValue;
                return true;
            }

            if ((object)defaultValue == null && (object)default(TValue) == null) {
                value = default(TValue);
                return true;
            }

            value = default(TValue);
            return false;
        }
    }

    internal abstract class BlackboardRuntimeValue {
        public abstract Type ValueType { get; }
        public abstract object GetBoxed();
        public abstract bool TrySetBoxed(object value, out bool changed);
        public abstract void Reset();
    }

    internal sealed class BlackboardRuntimeValue<T> : BlackboardRuntimeValue {
        private readonly T defaultValue;
        public T Value;

        public BlackboardRuntimeValue(T defaultValue) {
            this.defaultValue = defaultValue;
            Value = defaultValue;
        }

        public override Type ValueType => typeof(T);
        public override object GetBoxed() => Value;

        public override bool TrySetBoxed(object value, out bool changed) {
            if (value is T typedValue) {
                changed = !EqualityComparer<T>.Default.Equals(Value, typedValue);
                Value = typedValue;
                return true;
            }

            if (value == null && default(T) == null) {
                changed = !EqualityComparer<T>.Default.Equals(Value, default(T));
                Value = default(T);
                return true;
            }

            changed = false;
            return false;
        }

        public override void Reset() {
            Value = defaultValue;
        }
    }

    [Serializable] public sealed class BoolBlackboardVariable : BlackboardVariable<bool> { }
    [Serializable] public sealed class IntBlackboardVariable : BlackboardVariable<int> { }
    [Serializable] public sealed class FloatBlackboardVariable : BlackboardVariable<float> { }
    [Serializable] public sealed class StringBlackboardVariable : BlackboardVariable<string> { }
    [Serializable] public sealed class Vector2BlackboardVariable : BlackboardVariable<Vector2> { }
    [Serializable] public sealed class Vector3BlackboardVariable : BlackboardVariable<Vector3> { }
    [Serializable] public sealed class QuaternionBlackboardVariable : BlackboardVariable<Quaternion> { }
    [Serializable] public sealed class ColorBlackboardVariable : BlackboardVariable<Color> { }
    [Serializable] public sealed class ObjectBlackboardVariable : BlackboardVariable<UnityEngine.Object> { }
}
