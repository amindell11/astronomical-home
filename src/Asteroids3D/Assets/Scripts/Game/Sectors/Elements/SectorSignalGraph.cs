using System.Collections.Generic;
using UnityEngine;

namespace Game.Sectors
{
    /// <summary>Derived signal-wiring graph + validation over a sector's baked manifest: nodes are the manifest publishers' code-declared outputs, edges the consumers' serialized refs. Pure C# (no UnityEditor) so it is unit-testable in EditMode.</summary>
    public static class SectorSignalGraph
    {
        public enum Severity
        {
            Error,
            Info,
        }

        public readonly struct Finding
        {
            public readonly Severity Severity;
            public readonly string Message;
            public readonly Component Subject;

            public Finding(Severity severity, string message, Component subject)
            {
                Severity = severity;
                Message = message;
                Subject = subject;
            }
        }

        public class OutputNode
        {
            public Component Publisher;
            public SignalOutput Output;
            public readonly List<Component> Consumers = new();
        }

        public class Model
        {
            public readonly List<OutputNode> Outputs = new();
            public readonly List<Finding> Findings = new();

            public bool HasErrors
            {
                get
                {
                    foreach (var finding in Findings)
                        if (finding.Severity == Severity.Error) return true;
                    return false;
                }
            }

            public OutputNode NodeFor(SignalRef signal)
            {
                foreach (var node in Outputs)
                    if (node.Publisher == signal.source && node.Output.Id == signal.output)
                        return node;
                return null;
            }
        }

        public static Model Build(Sector sector)
        {
            var builder = new Builder(sector);

            foreach (var module in sector.Modules)
                builder.Declare(module);
            foreach (var spawner in sector.Spawners)
                builder.Declare(spawner);

            foreach (var module in sector.Modules)
            {
                switch (module)
                {
                    case ActivationRule rule:
                        foreach (var term in rule.Terms)
                            if (term.kind == ActivationTerm.TermKind.Signal)
                                builder.Consume(rule, "term", term.signal);
                        break;
                    case ActivateOnSignal activate:
                        builder.Consume(activate, "activation", activate.Signal);
                        break;
                }
            }

            foreach (var spawner in sector.Spawners)
                if (spawner && spawner.Mode == SectorSpawner.ActivationMode.Gated)
                    builder.Consume(spawner, "activation", spawner.ActivationSignal);

            builder.FinishFindings();
            return builder.Model;
        }

        private class Builder
        {
            public readonly Model Model = new();

            private readonly Sector sector;

            public Builder(Sector sector) => this.sector = sector;

            public void Declare(Component publisher)
            {
                if (publisher is not ISignalSource source) return;
                foreach (var output in source.Outputs)
                    Model.Outputs.Add(new OutputNode { Publisher = publisher, Output = output });
            }

            public void Consume(Component consumer, string role, SignalRef signal)
            {
                if (!signal.IsAssigned)
                {
                    Error($"{Describe(consumer)} has an unassigned {role} signal.", consumer);
                    return;
                }
                if (!signal.source.transform.IsChildOf(sector.transform))
                {
                    Error($"{Describe(consumer)} references {role} signal '{signal.output}' outside the sector.", consumer);
                    return;
                }
                var node = Model.NodeFor(signal);
                if (node == null)
                {
                    Error($"{Describe(consumer)} references {role} output '{signal.output}' that no sector publisher declares — check the source's declared outputs and the manifest.", consumer);
                    return;
                }
                node.Consumers.Add(consumer);
            }

            public void FinishFindings()
            {
                foreach (var node in Model.Outputs)
                    if (node.Consumers.Count == 0)
                        Info($"Output '{node.Output.Id}' of {Describe(node.Publisher)} is published but unconsumed.", node.Publisher);
                FindCycles();
            }

            private void FindCycles()
            {
                var edges = new Dictionary<Component, List<Component>>();
                foreach (var node in Model.Outputs)
                {
                    foreach (var consumer in node.Consumers)
                    {
                        if (!edges.TryGetValue(node.Publisher, out var list))
                            edges[node.Publisher] = list = new List<Component>();
                        list.Add(consumer);
                    }
                }

                var done = new HashSet<Component>();
                var stack = new List<Component>();
                foreach (var start in edges.Keys)
                    Visit(start, edges, done, stack);
            }

            private void Visit(Component current, Dictionary<Component, List<Component>> edges,
                HashSet<Component> done, List<Component> stack)
            {
                if (done.Contains(current)) return;
                var cycleStart = stack.IndexOf(current);
                if (cycleStart >= 0)
                {
                    var members = stack.GetRange(cycleStart, stack.Count - cycleStart);
                    Error($"Signal cycle: {string.Join(" → ", members.ConvertAll(Describe))} → {Describe(current)} — no member can ever fire.", current);
                    return;
                }

                stack.Add(current);
                if (edges.TryGetValue(current, out var next))
                    foreach (var consumer in next)
                        Visit(consumer, edges, done, stack);
                stack.RemoveAt(stack.Count - 1);
                done.Add(current);
            }

            private static string Describe(Component c) => $"{c.GetType().Name} on '{c.name}'";

            private void Error(string message, Component subject) =>
                Model.Findings.Add(new Finding(Severity.Error, message, subject));

            private void Info(string message, Component subject) =>
                Model.Findings.Add(new Finding(Severity.Info, message, subject));
        }
    }
}
