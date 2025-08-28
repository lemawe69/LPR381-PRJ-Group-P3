using LinearProgrammingSolver.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LinearProgrammingSolver.Algorithms
{
    public class Knapsack
    {
        private class Item
        {
            public int OriginalIndex; // 1-based index
            public double Value;
            public double Weight;
            public double Ratio => Weight > 0 ? Value / Weight : double.PositiveInfinity;
        }

        private class Node
        {
            public double Weight;
            public double Value;
            public double Bound;
            public bool?[] Decisions; // null=undecided, true=take, false=reject
            public string Id;
            public List<(int Index, bool Take)> DecisionsMade = new List<(int, bool)>();
            public int Level; // Current decision level
        }

        private List<Item> items;
        private double capacity;

        public Solution Solve(LinearProgram program)
        {
            if (!program.isKnapsackProblem || !program.WeightConstraints.Any())
                throw new ArgumentException("Not a knapsack problem or weight constraints missing.");

            int n = program.Variables.Count;
            var values = program.Variables.Select(v => v.Coefficient).ToArray();
            var weights = program.WeightConstraints[0].Coefficients.ToArray();
            capacity = program.WeightConstraints[0].Capacity;

            Solution sol = new Solution();
            var output = new StringBuilder();
            output.AppendLine();
            //output.AppendLine("Primal Simplex Solution");
            output.AppendLine("Simplified Output");
            output.AppendLine("Ratio Test");

            // Initialize items with proper indexing
            items = new List<Item>(n);
            for (int i = 0; i < n; i++)
            {
                items.Add(new Item
                {
                    OriginalIndex = i + 1,
                    Value = values[i],
                    Weight = weights[i]
                });
            }

            // Ratio Test - sorted by ratio descending
            var sortedItems = items.OrderByDescending(i => i.Ratio).ToList();
            int rank = 1;
            foreach (var item in sortedItems)
            {
                string ratioStr = item.Ratio.ToString("0.###").Replace(".", ",");
                string spacing = GetRatioSpacing(item.OriginalIndex, item.Value, item.Weight, ratioStr);
                output.AppendLine($"x{item.OriginalIndex} {item.Value}/{item.Weight} = {ratioStr}{spacing}Rank {rank++}");
            }
            output.AppendLine();

            // Process Sub-problem 0 - LP relaxation
            output.AppendLine("Sub-problem 0");
            var rootRelaxation = ComputeLPRelaxation(capacity, 0, 0, new bool?[n]);
            DisplayLPRelaxation(output, new bool?[n], capacity, 0, 0, new List<(int, bool)>(), false);

            if (rootRelaxation.fractionalItem != null)
            {
                output.AppendLine();
                output.AppendLine($"Comment: Sub-problem 0 will be branched on x{rootRelaxation.fractionalItem.OriginalIndex} = 0 (Sub-problem 1) and x{rootRelaxation.fractionalItem.OriginalIndex} = 1 (Sub-problem 2)");
                output.AppendLine();
            }

            // Branch and Bound
            var queue = new Queue<Node>();
            Node root = new Node
            {
                Weight = 0,
                Value = 0,
                Decisions = new bool?[n],
                Id = "0",
                Level = 0
            };
            queue.Enqueue(root);

            double bestValue = double.NegativeInfinity;
            bool[] bestSolution = new bool[n];
            var candidates = new List<(string Id, double Value, bool[] Decisions, string Label)>();
            char candidateLabel = 'A';

            // Continue with branch and bound (skip root since already processed)
            while (queue.Count > 0)
            {
                Node node = queue.Dequeue();

                if (node.Id == "0")
                {
                    // Handle root branching
                    if (rootRelaxation.fractionalItem != null)
                    {
                        int branchIndex = items.FindIndex(it => it.OriginalIndex == rootRelaxation.fractionalItem.OriginalIndex);

                        // Create skip branch (x = 0)
                        var skipNode = new Node
                        {
                            Weight = 0,
                            Value = 0,
                            Decisions = new bool?[n],
                            Id = "1",
                            Level = 1
                        };
                        skipNode.Decisions[branchIndex] = false;
                        skipNode.DecisionsMade.Add((branchIndex, false));
                        queue.Enqueue(skipNode);

                        // Create take branch (x = 1)
                        var takeNode = new Node
                        {
                            Weight = rootRelaxation.fractionalItem.Weight,
                            Value = rootRelaxation.fractionalItem.Value,
                            Decisions = new bool?[n],
                            Id = "2",
                            Level = 1
                        };
                        takeNode.Decisions[branchIndex] = true;
                        takeNode.DecisionsMade.Add((branchIndex, true));
                        queue.Enqueue(takeNode);
                    }
                    continue;
                }

                // Check infeasibility
                if (node.Weight > capacity)
                {
                    output.AppendLine($"Sub-problem {node.Id}");
                    output.AppendLine("Comment: Infeasible");
                    output.AppendLine();
                    continue;
                }

                // Compute LP relaxation for current node
                var relaxation = ComputeLPRelaxation(capacity - node.Weight, node.Weight, node.Value, node.Decisions);
                node.Bound = node.Value + relaxation.addedValue;

                // Display current subproblem
                string decisionsStr = string.Join(" ", node.DecisionsMade.Select(d => $"x{items[d.Index].OriginalIndex} = {(d.Take ? 1 : 0)}"));
                if (string.IsNullOrEmpty(decisionsStr))
                    output.AppendLine($"Sub-problem {node.Id}");
                else
                    output.AppendLine($"Sub-problem {node.Id}: {decisionsStr}");

                DisplayLPRelaxation(output, node.Decisions, capacity, node.Weight, node.Value, node.DecisionsMade, true);

                if (!relaxation.hasFraction)
                {
                    // Integer solution found
                    double totalValue = node.Value + relaxation.takenItems.Sum(i => i.Value);
                    bool[] fullDecisions = new bool[n];

                    // Set decisions made so far
                    for (int i = 0; i < n; i++)
                        fullDecisions[i] = node.Decisions[i] ?? false;

                    // Add remaining items taken in relaxation
                    foreach (var item in relaxation.takenItems)
                    {
                        int idx = items.FindIndex(it => it.OriginalIndex == item.OriginalIndex);
                        if (idx >= 0) fullDecisions[idx] = true;
                    }

                    var takenItems = items.Where((item, j) => fullDecisions[j]).OrderBy(item => item.OriginalIndex);
                    string valueStr = string.Join(" + ", takenItems.Select(item => item.Value.ToString("0")));

                    output.AppendLine("Comment: Optimal solution available");
                    output.AppendLine($"z = {valueStr} = {totalValue}");
                    output.AppendLine($"Candidate {candidateLabel}");

                    candidates.Add((node.Id, totalValue, (bool[])fullDecisions.Clone(), candidateLabel.ToString()));
                    candidateLabel++;

                    if (totalValue > bestValue)
                    {
                        bestValue = totalValue;
                        bestSolution = (bool[])fullDecisions.Clone();
                    }
                    output.AppendLine();
                    continue;
                }

                // Branch on fractional item
                if (relaxation.fractionalItem != null && node.Level < n)
                {
                    int branchIndex = items.FindIndex(it => it.OriginalIndex == relaxation.fractionalItem.OriginalIndex);
                    if (branchIndex < 0 || node.Decisions[branchIndex].HasValue) continue; // Skip if already decided

                    string skipId = $"{node.Id}.1";
                    string takeId = $"{node.Id}.2";

                    output.AppendLine();
                    output.AppendLine($"Comment: Sub-problem {node.Id} will be branched on x{relaxation.fractionalItem.OriginalIndex} = 0( Sub-problem {skipId}) and x{relaxation.fractionalItem.OriginalIndex} = 1 Sub-problem {takeId})");
                    output.AppendLine();

                    // Create skip branch (x_i = 0)
                    var skipNode = new Node
                    {
                        Weight = node.Weight,
                        Value = node.Value,
                        Decisions = (bool?[])node.Decisions.Clone(),
                        Id = skipId,
                        DecisionsMade = new List<(int, bool)>(node.DecisionsMade),
                        Level = node.Level + 1
                    };
                    skipNode.DecisionsMade.Add((branchIndex, false));
                    skipNode.Decisions[branchIndex] = false;
                    queue.Enqueue(skipNode);

                    // Create take branch (x_i = 1)
                    var takeNode = new Node
                    {
                        Weight = node.Weight + relaxation.fractionalItem.Weight,
                        Value = node.Value + relaxation.fractionalItem.Value,
                        Decisions = (bool?[])node.Decisions.Clone(),
                        Id = takeId,
                        DecisionsMade = new List<(int, bool)>(node.DecisionsMade),
                        Level = node.Level + 1
                    };
                    takeNode.DecisionsMade.Add((branchIndex, true));
                    takeNode.Decisions[branchIndex] = true;
                    queue.Enqueue(takeNode);
                }
            }

            // Output comparison
            output.AppendLine("COMPARISON OF CANDIDATES");
            foreach (var candidate in candidates)
            {
                var takenItems = items.Where((item, j) => candidate.Decisions[j]).OrderBy(item => item.OriginalIndex);
                string valueStr = string.Join(" + ", takenItems.Select(item => item.Value.ToString("0")));
                output.AppendLine($"Candidate {candidate.Label}: z = {valueStr} = {candidate.Value}");
            }

            var bestCandidate = candidates.OrderByDescending(c => c.Value).First();
            output.AppendLine($"Candidate {bestCandidate.Label} is the best candidate");
            output.AppendLine();

            // Final optimal solution
            /*output.AppendLine("Optimal Solution:");
            for (int i = 0; i < n; i++)
            {
                output.AppendLine($"x{items[i].OriginalIndex} = {(bestSolution[i] ? 1.000 : 0.000).ToString("0.000").Replace(".", ",")}");
            } 
            output.AppendLine($"Optimal Value: {bestValue.ToString("0.000").Replace(".", ",")}"); */

            sol.AddStep("Primal Simplex Solution", output.ToString());
            sol.OptimalValue = bestValue;
            sol.VariableValues = items.ToDictionary(i => $"x{i.OriginalIndex}", i => bestSolution[items.IndexOf(i)] ? 1.0 : 0.0);

            return sol;
        }

        private string GetRatioSpacing(int index, double value, double weight, string ratioStr)
        {
            // Calculate spacing to align "Rank" properly
            int baseLength = $"x{index} {value}/{weight} = {ratioStr}".Length;
            int targetLength = 40; // Approximate target for alignment
            int spacesNeeded = Math.Max(1, targetLength - baseLength);
            return new string(' ', spacesNeeded);
        }

        private void DisplayLPRelaxation(StringBuilder output, bool?[] decisions, double totalCapacity, double usedWeight, double currentValue, List<(int, bool)> decisionsMade, bool showFixed)
        {
            double remainingCapacity = totalCapacity - usedWeight;

            // Show fixed decisions first (only for non-root nodes)
            if (showFixed)
            {
                foreach (var decision in decisionsMade)
                {
                    Item item = items[decision.Item1];
                    if (decision.Item2)
                    {
                        output.AppendLine($"∗ 𝑥{item.OriginalIndex} = 1 {totalCapacity}-{item.Weight}={totalCapacity - item.Weight}");
                        remainingCapacity = totalCapacity - item.Weight;
                        totalCapacity = remainingCapacity;
                    }
                    else
                    {
                        output.AppendLine($"∗ 𝑥{item.OriginalIndex} = 0           {totalCapacity}-0={totalCapacity}");
                    }
                }
            }

            // Show LP relaxation for undecided variables (sorted by ratio)
            var undecided = items.Where((item, index) => !decisions[index].HasValue).OrderByDescending(i => i.Ratio).ToList();
            foreach (var item in undecided)
            {
                if (remainingCapacity <= 0)
                {
                    output.AppendLine($"𝑥{item.OriginalIndex} = 0");
                }
                else if (item.Weight <= remainingCapacity)
                {
                    string spacing = showFixed ? "" : "              ";
                    output.AppendLine($"𝑥{item.OriginalIndex} = 1{spacing}{remainingCapacity}-{item.Weight}={remainingCapacity - item.Weight}");
                    remainingCapacity -= item.Weight;
                }
                else
                {
                    double fraction = remainingCapacity / item.Weight;
                    string fractionStr = fraction.ToString("0.###").Replace(".", ",");
                    output.AppendLine($"𝑥{item.OriginalIndex} = {fractionStr} {remainingCapacity}-{item.Weight}");
                    remainingCapacity = 0;
                }
            }
        }

        private (Item fractionalItem, double addedValue, List<Item> takenItems, bool hasFraction) ComputeLPRelaxation(double remainingCapacity, double curWeight, double curValue, bool?[] decisions)
        {
            var undecided = items.Where((item, index) => !decisions[index].HasValue).OrderByDescending(i => i.Ratio).ToList();
            double addedValue = 0;
            double tempCapacity = remainingCapacity;
            List<Item> takenItems = new List<Item>();
            Item fractionalItem = null;
            bool hasFraction = false;

            foreach (var item in undecided)
            {
                if (item.Weight <= tempCapacity)
                {
                    tempCapacity -= item.Weight;
                    addedValue += item.Value;
                    takenItems.Add(item);
                }
                else if (tempCapacity > 0)
                {
                    // Fractional assignment
                    addedValue += item.Value * (tempCapacity / item.Weight);
                    fractionalItem = item;
                    hasFraction = true;
                    break;
                }
            }

            return (fractionalItem, addedValue, takenItems, hasFraction);
        }
    }
}