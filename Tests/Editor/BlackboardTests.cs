using NUnit.Framework;
using UnityEngine;

namespace XNode.Tests {
    public sealed class BlackboardTests {
        private sealed class TestGraph : NodeGraph { }

        private TestGraph graph;

        [SetUp]
        public void SetUp() {
            graph = ScriptableObject.CreateInstance<TestGraph>();
        }

        [TearDown]
        public void TearDown() {
            for (int i = graph.nodes.Count - 1; i >= 0; i--) {
                if (graph.nodes[i] != null) Object.DestroyImmediate(graph.nodes[i]);
            }
            Object.DestroyImmediate(graph);
        }

        [Test]
        public void RuntimeInstancesAreIsolated() {
            FloatBlackboardVariable variable =
                graph.BlackboardDefinition.Add<FloatBlackboardVariable>("Speed");
            variable.DefaultValue = 3f;

            BlackboardInstance first = graph.BlackboardDefinition.CreateInstance();
            BlackboardInstance second = graph.BlackboardDefinition.CreateInstance();
            first.Set(variable.Id, 9f);

            Assert.That(first.Get<float>(variable.Id), Is.EqualTo(9f));
            Assert.That(second.Get<float>(variable.Id), Is.EqualTo(3f));
        }

        [Test]
        public void RenameKeepsStableReference() {
            IntBlackboardVariable variable =
                graph.BlackboardDefinition.Add<IntBlackboardVariable>("Count");
            string id = variable.Id;

            Assert.That(graph.BlackboardDefinition.Rename(id, "Total"), Is.True);
            Assert.That(variable.Id, Is.EqualTo(id));
            Assert.That(variable.Name, Is.EqualTo("Total"));
        }

        [Test]
        public void DuplicateNamesAreRejectedCaseInsensitively() {
            IntBlackboardVariable first =
                graph.BlackboardDefinition.Add<IntBlackboardVariable>("Count");
            IntBlackboardVariable second =
                graph.BlackboardDefinition.Add<IntBlackboardVariable>("Other");

            Assert.That(
                graph.BlackboardDefinition.Rename(second.Id, first.Name.ToLowerInvariant()),
                Is.False);
        }

        [Test]
        public void GetterReadsExecutionContext() {
            FloatBlackboardVariable variable =
                graph.BlackboardDefinition.Add<FloatBlackboardVariable>("Speed");
            variable.DefaultValue = 2f;
            GetFloatBlackboardVariableNode getter =
                graph.AddNode<GetFloatBlackboardVariableNode>();
            getter.Variable = new BlackboardVariableReference(variable.Id);

            GraphExecutionContext context = new GraphExecutionContext(graph);
            context.Blackboard.Set(variable.Id, 7f);

            Assert.That(
                getter.GetValue(getter.GetOutputPort("value"), context),
                Is.EqualTo(7f));
        }

        [Test]
        public void OverrideSetAppliesEnabledValuesOnly() {
            IntBlackboardVariable count =
                graph.BlackboardDefinition.Add<IntBlackboardVariable>("Count");
            count.DefaultValue = 1;
            FloatBlackboardVariable speed =
                graph.BlackboardDefinition.Add<FloatBlackboardVariable>("Speed");
            speed.DefaultValue = 2f;

            var overrides = new BlackboardOverrideSet();
            overrides.Set(count, 9);
            overrides.Enable(speed);
            overrides.Disable(speed.Id);

            BlackboardInstance instance = graph.BlackboardDefinition.CreateInstance();
            overrides.Apply(instance);

            Assert.That(instance.Get<int>(count.Id), Is.EqualTo(9));
            Assert.That(instance.Get<float>(speed.Id), Is.EqualTo(2f));
        }

        [Test]
        public void TrySetByNameRejectsWrongType() {
            graph.BlackboardDefinition.Add<IntBlackboardVariable>("Count");
            BlackboardInstance instance = graph.BlackboardDefinition.CreateInstance();

            Assert.That(instance.TrySetByName("Count", 4), Is.True);
            Assert.That(instance.TrySetByName("Count", 1.5f), Is.False);
            Assert.That(instance.GetByName<int>("Count"), Is.EqualTo(4));
        }

        [Test]
        public void CopiedNodeReceivesNewStableId() {
            GetFloatBlackboardVariableNode original =
                graph.AddNode<GetFloatBlackboardVariableNode>();
            GetFloatBlackboardVariableNode copy =
                (GetFloatBlackboardVariableNode)graph.CopyNode(original);

            Assert.That(copy.NodeId, Is.Not.EqualTo(original.NodeId));
        }
    }
}
