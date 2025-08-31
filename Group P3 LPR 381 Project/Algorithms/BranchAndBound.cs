using LinearProgrammingSolver.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LinearProgrammingSolver.Algorithms
{
    public class BranchAndBound : ISolver
    {
        private const double TOLERANCE = 1e-6;
        private double _bestKnownValue;
        private bool _hasBestSolution;
        private Solution _bestIntegerSolution;
        private int _maxDepth = 30;

        public BranchAndBound()
        {
        }

        public Solution Solve(LinearProgram program)
        {
            var solution = new Solution();
            _bestKnownValue = program.IsMaximization ? double.MinValue : double.MaxValue;
            _hasBestSolution = false;
            _bestIntegerSolution = null;

            solution.AddMessage("Running Branch and Bound algorithm...");
            solution.AddStep("Canonical Form", FormatCanonicalForm(program));

            // Step 1: Solve the initial LP using dual simplex and display the optimal table
            var rootDualSimplex = new DualSimplex();
            var rootSolution = rootDualSimplex.Solve(program);

            // Check if the initial solution is infeasible or unbounded
            if (rootSolution.Messages.Any(m => m.Contains("infeasible") || m.Contains("unbounded")))
            {
                solution.AddMessage("Initial problem is infeasible or unbounded. Cannot proceed with Branch and Bound.");
                foreach (var message in rootSolution.Messages)
                    solution.AddMessage(message);
                return solution;
            }

            // Use depth-first search with a stack
            var stack = new Stack<SubProblem>();

            // Add root problem to stack
            stack.Push(new SubProblem
            {
                DualSimplex = rootDualSimplex,
                Solution = rootSolution,
                Path = "",
                Level = 0
            });

            // Process stack depth-first
            while (stack.Count > 0)
            {
                var current = stack.Pop();

                if (current.Level > _maxDepth)
                {
                    solution.AddMessage($"Maximum branching depth ({_maxDepth}) reached for sub-problem {current.Path}. Terminating branch.");
                    continue;
                }

                var result = ProcessSubProblem(current, solution, program);

                if (result != null && result.Count > 0)
                {
                    // Push in reverse order so that .1 gets processed before .2 (depth-first)
                    for (int i = result.Count - 1; i >= 0; i--)
                    {
                        stack.Push(result[i]);
                    }
                }
            }

            if (_bestIntegerSolution != null)
            {
                solution.OptimalValue = _bestIntegerSolution.OptimalValue;
                solution.VariableValues = _bestIntegerSolution.VariableValues;
                solution.AddMessage($"\nBest integer solution found with value: {_bestIntegerSolution.OptimalValue:F3}");
            }
            else
            {
                solution.AddMessage("No feasible integer solution found.");
            }

            return solution;
        }

        private List<SubProblem> ProcessSubProblem(SubProblem current, Solution mainSolution, LinearProgram originalProgram)
        {
            var sb = new StringBuilder();
            var subProblems = new List<SubProblem>();

            // Sub-problem header
            string subProblemTitle;
            if (string.IsNullOrEmpty(current.Path))
            {
                subProblemTitle = "Sub-problem ";
                sb.AppendLine("Root Problem (LP Relaxation):");
            }
            else
            {
                subProblemTitle = $"Sub-problem {current.Path}";
                sb.AppendLine($"Sub-problem {current.Path} started:");
            }

            // Check for infeasibility first
            if (current.Solution.Messages.Any(m => m.Contains("infeasible")))
            {
                sb.AppendLine($"Sub-problem {current.Path} is infeasible. Pruning this branch.");
                mainSolution.AddStep(subProblemTitle, sb.ToString());
                return subProblems;
            }

            // Show final tableau with proper formatting
            if (current.Solution.FinalTableau != null)
            {
                sb.AppendLine(FormatTableau(current.Solution.FinalTableau, current.Solution));
            }

            // Show solution values
            sb.AppendLine("Solution:");
            if (current.Solution.VariableValues != null)
            {
                // Show auxiliary variables first (s, e variables)
                var auxVars = current.Solution.VariableValues
                    .Where(kvp => kvp.Key.StartsWith("s") || kvp.Key.StartsWith("e"))
                    .OrderBy(kvp => kvp.Key);
                foreach (var kvp in auxVars)
                {
                    sb.AppendLine($"  {kvp.Key} = {kvp.Value:F3}");
                }

                // Show decision variables
                var decisionVars = current.Solution.VariableValues
                    .Where(kvp => kvp.Key.StartsWith("x"))
                    .OrderBy(kvp => GetVariableIndex(kvp.Key));
                foreach (var kvp in decisionVars)
                {
                    sb.AppendLine($"  {kvp.Key} = {kvp.Value:F3}");
                }
                sb.AppendLine($"  Objective Value = {current.Solution.OptimalValue:F3}");
            }

            // BOUND CHECK: Compare with best known solution
            if (_hasBestSolution && ShouldPrune(current.Solution.OptimalValue, _bestKnownValue, originalProgram.IsMaximization))
            {
                sb.AppendLine($"Sub-problem {current.Path} can be pruned by bound.");
                sb.AppendLine($"Current objective value {current.Solution.OptimalValue:F3} is not better than best known solution {_bestKnownValue:F3}.");
                mainSolution.AddStep(subProblemTitle, sb.ToString());
                return subProblems;
            }

            // Step 2: Check to see if any basic decision variables are decimal
            var fractionalVar = FindMostFractionalVariable(current.Solution, originalProgram);

            if (fractionalVar == null)
            {
                // All decision variables are integers
                if (string.IsNullOrEmpty(current.Path))
                {
                    sb.AppendLine("All decision variables are integers in the root problem. No branching required.");
                }
                else
                {
                    sb.AppendLine($"Sub-problem {current.Path} found an integer solution!");

                    if (!_hasBestSolution || IsBetterSolution(current.Solution.OptimalValue, _bestKnownValue, originalProgram.IsMaximization))
                    {
                        _bestKnownValue = current.Solution.OptimalValue;
                        _hasBestSolution = true;
                        _bestIntegerSolution = current.Solution;
                        sb.AppendLine($"This is the new best integer solution with value {current.Solution.OptimalValue:F3}");
                    }
                    else
                    {
                        sb.AppendLine($"This integer solution ({current.Solution.OptimalValue:F3}) is not better than current best ({_bestKnownValue:F3})");
                    }
                }

                mainSolution.AddStep(subProblemTitle, sb.ToString());
                return subProblems;
            }
            else
            {
                // There are fractional decision variables - branch
                string varName = fractionalVar.Item1;
                double varValue = fractionalVar.Item2;
                int varIndex = GetVariableIndex(varName);

                sb.AppendLine($"Sub-problem {current.Path} will be branched on variable {varName} = {varValue:F3}");
                mainSolution.AddStep(subProblemTitle, sb.ToString());

                double floorValue = Math.Floor(varValue);
                double ceilValue = Math.Ceiling(varValue);

                // Create Sub-problem 1: x <= floor(value)
                string subPath1 = string.IsNullOrEmpty(current.Path) ? "1" : current.Path + ".1";

                try
                {
                    // Clone the current dual simplex state
                    var dualSimplex1 = CloneDualSimplexState(current.DualSimplex);

                    var constraint1 = new LinearProgram.Constraint();
                    for (int i = 0; i < originalProgram.Variables.Count; i++)
                    {
                        constraint1.Coefficients.Add(0);
                    }
                    constraint1.Coefficients[varIndex - 1] = 1;
                    constraint1.Relation = LinearProgram.Relation.LessThanOrEqual;
                    constraint1.Rhs = floorValue;

                    var solution1 = dualSimplex1.AddConstraintAndResolve(constraint1);

                    subProblems.Add(new SubProblem
                    {
                        DualSimplex = dualSimplex1,
                        Solution = solution1,
                        Path = subPath1,
                        Level = current.Level + 1
                    });
                }
                catch (Exception ex)
                {
                    mainSolution.AddMessage($"Error creating sub-problem {subPath1}: {ex.Message}");
                }

                // Create Sub-problem 2: x >= ceil(value)
                string subPath2 = string.IsNullOrEmpty(current.Path) ? "2" : current.Path + ".2";

                try
                {
                    // Clone the current dual simplex state
                    var dualSimplex2 = CloneDualSimplexState(current.DualSimplex);

                    var constraint2 = new LinearProgram.Constraint();
                    for (int i = 0; i < originalProgram.Variables.Count; i++)
                    {
                        constraint2.Coefficients.Add(0);
                    }
                    constraint2.Coefficients[varIndex - 1] = 1;
                    constraint2.Relation = LinearProgram.Relation.GreaterThanOrEqual;
                    constraint2.Rhs = ceilValue;

                    var solution2 = dualSimplex2.AddConstraintAndResolve(constraint2);

                    subProblems.Add(new SubProblem
                    {
                        DualSimplex = dualSimplex2,
                        Solution = solution2,
                        Path = subPath2,
                        Level = current.Level + 1
                    });
                }
                catch (Exception ex)
                {
                    mainSolution.AddMessage($"Error creating sub-problem {subPath2}: {ex.Message}");
                }
            }

            return subProblems;
        }

        private DualSimplex CloneDualSimplexState(DualSimplex original)
        {
            return original.Clone();
        }

        private string FormatTableau(double[,] tableau, Solution solution)
        {
            if (tableau == null)
                return "Tableau not available";

            var sb = new StringBuilder();
            int rows = tableau.GetLength(0);
            int cols = tableau.GetLength(1);

            // Calculate variable counts
            int decisionVars = solution.VariableCount;
            int totalAuxVars = cols - decisionVars - 1; // -1 for RHS column

            // Header row
            sb.Append("\t");

            // Decision variables
            for (int i = 1; i <= decisionVars; i++)
                sb.Append($"x{i}\t");

            // Auxiliary variables
            int slackCount = 0;
            int excessCount = 0;
            int artificialCount = 0;

            for (int i = 0; i < totalAuxVars; i++)
            {
                // Determine variable type based on solution counts
                if (slackCount < solution.SlackCount)
                {
                    slackCount++;
                    sb.Append($"s{slackCount}\t");
                }
                else if (excessCount < solution.ExcessCount)
                {
                    excessCount++;
                    sb.Append($"e{excessCount}\t");
                }
                else if (artificialCount < solution.ArtificialCount)
                {
                    artificialCount++;
                    sb.Append($"a{artificialCount}\t");
                }
                else
                {
                    // Fallback naming
                    sb.Append($"aux{i + 1}\t");
                }
            }

            sb.AppendLine("RHS");

            // Data rows
            for (int i = 0; i < rows; i++)
            {
                if (i == 0)
                    sb.Append("z\t");
                else
                    sb.Append($"Con {i}\t");

                for (int j = 0; j < cols; j++)
                {
                    double value = tableau[i, j];
                    if (Math.Abs(value) < TOLERANCE)
                        value = 0;

                    // Format numbers with 3 decimal places and comma separators like in expected output
                    sb.Append($"{value:F3}\t".Replace('.', ','));
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private bool ShouldPrune(double currentValue, double bestKnownValue, bool isMaximization)
        {
            if (isMaximization)
                return currentValue <= bestKnownValue + TOLERANCE;
            else
                return currentValue >= bestKnownValue - TOLERANCE;
        }

        private Tuple<string, double> FindMostFractionalVariable(Solution solution, LinearProgram program)
        {
            string mostFractionalVar = null;
            double mostFractionalValue = 0;
            double closestToHalf = double.MaxValue;
            int lowestVarIndex = int.MaxValue;

            var decisionVars = solution.VariableValues
                .Where(kvp => kvp.Key.StartsWith("x"))
                .OrderBy(kvp => GetVariableIndex(kvp.Key))
                .ToList();

            foreach (var kvp in decisionVars)
            {
                double fractionalPart = kvp.Value - Math.Floor(kvp.Value);

                if (fractionalPart > TOLERANCE && fractionalPart < 1 - TOLERANCE)
                {
                    double distanceFromHalf = Math.Abs(fractionalPart - 0.5);
                    int varIndex = GetVariableIndex(kvp.Key);

                    if (distanceFromHalf < closestToHalf ||
                        (Math.Abs(distanceFromHalf - closestToHalf) < TOLERANCE && varIndex < lowestVarIndex))
                    {
                        closestToHalf = distanceFromHalf;
                        mostFractionalVar = kvp.Key;
                        mostFractionalValue = kvp.Value;
                        lowestVarIndex = varIndex;
                    }
                }
            }

            return mostFractionalVar != null ? Tuple.Create(mostFractionalVar, mostFractionalValue) : null;
        }

        private int GetVariableIndex(string varName)
        {
            return int.Parse(varName.Substring(1));
        }

        private bool IsBetterSolution(double newValue, double currentBest, bool isMaximization)
        {
            if (isMaximization)
                return newValue > currentBest;
            else
                return newValue < currentBest;
        }

        public string FormatCanonicalForm(LinearProgram program)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Canonical Form (with slack variables):");

            // Objective function in canonical form: z - c1*x1 - c2*x2 - ... = 0
            sb.Append("z");
            for (int i = 0; i < program.Variables.Count; i++)
            {
                double coeff = program.IsMaximization ? -program.Variables[i].Coefficient : program.Variables[i].Coefficient;
                if (coeff >= 0)
                    sb.Append($" + {coeff:F0}x{i + 1}");
                else
                    sb.Append($" - {Math.Abs(coeff):F0}x{i + 1}");
            }
            sb.AppendLine(" = 0");

            // Constraints in canonical form with slack variables
            int slackIndex = 1;
            for (int i = 0; i < program.Constraints.Count; i++)
            {
                var constraint = program.Constraints[i];

                // Decision variables
                bool first = true;
                for (int j = 0; j < constraint.Coefficients.Count && j < program.Variables.Count; j++)
                {
                    double coeff = constraint.Coefficients[j];
                    if (Math.Abs(coeff) < TOLERANCE) continue;

                    if (first)
                    {
                        sb.Append($"{coeff:F0}x{j + 1}");
                        first = false;
                    }
                    else
                    {
                        if (coeff >= 0)
                            sb.Append($" + {coeff:F0}x{j + 1}");
                        else
                            sb.Append($" - {Math.Abs(coeff):F0}x{j + 1}");
                    }
                }

                // Add slack/surplus variable based on constraint type
                if (constraint.Relation == LinearProgram.Relation.LessThanOrEqual)
                {
                    sb.Append($" + s{slackIndex}");
                    slackIndex++;
                }
                else if (constraint.Relation == LinearProgram.Relation.GreaterThanOrEqual)
                {
                    sb.Append($" - s{slackIndex}");
                    slackIndex++;
                }

                sb.AppendLine($" = {constraint.Rhs:F0}");
            }

            sb.AppendLine("All variables >= 0");

            return sb.ToString();
        }

        private class SubProblem
        {
            public DualSimplex DualSimplex { get; set; }
            public Solution Solution { get; set; }
            public string Path { get; set; }
            public int Level { get; set; }
        }
    }
}