using System.Collections.Generic;
using UnityEngine;

namespace Game.Sectors
{
    /// <summary>Derived signal-wiring graph + validation over a sector's baked manifest (ports themselves are hierarchy components, not manifest entities). Pure C# (no UnityEditor) so it is unit-testable in EditMode.</summary>
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

        public class PortNode
        {
            public SignalPort Port;
            public Component Publisher;
            public string Role;
            public readonly List<Component> Consumers = new();
        }

        public class Model
        {
            public readonly List<PortNode> Ports = new();
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

            public PortNode NodeFor(SignalPort port)
            {
                foreach (var node in Ports)
                    if (node.Port == port) return node;
                return null;
            }
        }

        public static Model Build(Sector sector)
        {
            var builder = new Builder(sector);
            foreach (var port in sector.GetComponentsInChildren<SignalPort>(true))
                builder.NodeFor(port);

            foreach (var module in sector.Modules)
            {
                switch (module)
                {
                    case null: break;
                    case TriggerVolume volume:
                        builder.Claim(volume, "inside", volume.InsidePort);
                        break;
                    case SectorSpineModule spine:
                        foreach (var (step, port) in spine.StepPorts)
                            builder.Claim(spine, step, port);
                        break;
                    case ActivationRule rule:
                        builder.Claim(rule, "fired", rule.FiredPort);
                        if (rule is AmbushEncounter ambush)
                            builder.Claim(ambush, "cleared", ambush.ClearedPort);
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
            private readonly Dictionary<SignalPort, PortNode> nodes = new();

            public Builder(Sector sector) => this.sector = sector;

            public PortNode NodeFor(SignalPort port)
            {
                if (nodes.TryGetValue(port, out var node)) return node;
                node = new PortNode { Port = port };
                nodes[port] = node;
                Model.Ports.Add(node);
                return node;
            }

            public void Claim(Component publisher, string role, SignalPort port)
            {
                if (!ValidRef(publisher, role, port)) return;
                var node = NodeFor(port);
                if (node.Publisher)
                {
                    Error($"Port '{port.name}' is claimed by both {Describe(node.Publisher)} ({node.Role}) and {Describe(publisher)} ({role}) — ports are single-owner.", port);
                    return;
                }
                node.Publisher = publisher;
                node.Role = role;
            }

            public void Consume(Component consumer, string role, SignalPort port)
            {
                if (!ValidRef(consumer, role, port)) return;
                NodeFor(port).Consumers.Add(consumer);
            }

            public void FinishFindings()
            {
                foreach (var node in Model.Ports)
                {
                    if (!node.Publisher)
                        Error($"SignalPort on '{node.Port.name}' has no publisher — nothing can ever raise it.", node.Port);
                    else if (node.Consumers.Count == 0)
                        Info($"Port '{node.Port.name}' ({node.Role}) is published but unconsumed.", node.Port);
                }
                FindCycles();
            }

            private bool ValidRef(Component owner, string role, SignalPort port)
            {
                if (!port)
                {
                    Error($"{Describe(owner)} has an unassigned {role} port.", owner);
                    return false;
                }
                if (!port.transform.IsChildOf(sector.transform))
                {
                    Error($"{Describe(owner)} references {role} port '{port.name}' outside the sector.", owner);
                    return false;
                }
                return true;
            }

            private void FindCycles()
            {
                var edges = new Dictionary<Component, List<Component>>();
                foreach (var node in Model.Ports)
                {
                    if (!node.Publisher) continue;
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
