using LinearProgrammingSolver.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LinearProgrammingSolver.Algorithms
{
    public class BranchAndBound : ISolver
    {
        private readonly PrimalSimplex _primalSimplex;
        private readonly DualSimplex _dualSimplex;
        private int _subProblemCounter;
        private const double TOLERANCE = 1e-6;

        public BranchAndBound()
        {
            _primalSimplex = new PrimalSimplex();
            _dualSimplex = new DualSimplex();
            _subProblemCounter = 0;
        }

        public Solution Solve(LinearProgram program)
        {
            var solution = new Solution();

            solution.AddStep("Canonical Form", FormatCanonicalForm(program));

            var initialSolution = _dualSimplex.Solve(program);

            // Check if the initial solution is infeasible or unbounded
            if (initialSolution.Messages.Any(m => m.Contains("infeasible") || m.Contains("unbounded")))
            {
                solution.AddMessage("Initial problem is infeasible or unbounded. Cannot proceed with Branch and Bound.");
                foreach (var message in initialSolution.Messages)
                    solution.AddMessage(message);
                return solution;
            }

            // Start the branch and bound process
            var bestSolution = SolveBranchAndBound(program, solution, "", 1);

            if (bestSolution != null)
            {
                solution.OptimalValue = bestSolution.OptimalValue;
                solution.VariableValues = bestSolution.VariableValues;
                solution.AddMessage($"\nBest integer solution found with value: {bestSolution.OptimalValue:F3}");
            }
            else
            {
                solution.AddMessage("No feasible integer solution found.");
            }

            return solution;
        }

        private Solution SolveBranchAndBound(LinearProgram program, Solution mainSolution, string subProblemPath, int level)
        {
            if (level > 20) // Prevent infinite recursion
            {
                mainSolution.AddMessage($"Maximum branching depth reached for {subProblemPath}. Terminating branch.");
                return null;
            }

            // Solve current sub-problem
            var currentSolution = _dualSimplex.Solve(program);

            // Build local step with tableau and messages
            var sb = new StringBuilder();

            // Sub-problem header
            //if (!string.IsNullOrEmpty(subProblemPath))
            //    sb.AppendLine($"Sub-problem {subProblemPath} started:");

            // Sub-problem header
            if (!string.IsNullOrEmpty(subProblemPath))
            {
                sb.AppendLine($"Sub-problem {subProblemPath} started:");
            }
            else
            {
                sb.AppendLine("Primal Simplex Optimal Tableau:");
            }

            // Display tableau
            sb.AppendLine(FormatTableau(currentSolution));

            // Check for infeasibility or unboundedness
            if (currentSolution.Messages.Any(m => m.Contains("infeasible")))
            {
                sb.AppendLine($"Sub-problem {subProblemPath} is infeasible.");
                mainSolution.AddStep($"Sub-problem {subProblemPath}", sb.ToString());
                return null;
            }

            if (currentSolution.Messages.Any(m => m.Contains("unbounded")))
            {
                sb.AppendLine($"Sub-problem {subProblemPath} is unbounded.");
                mainSolution.AddStep($"Sub-problem {subProblemPath}", sb.ToString());
                return null;
            }

            // Find fractional variable
            var fractionalVar = FindMostFractionalVariable(currentSolution, program);

            if (fractionalVar == null)
            {
                // Optimal integer solution for this sub-problem
                sb.AppendLine($"Sub-problem {subProblemPath} is optimal. There is no need to branch it any further.");
                mainSolution.AddStep($"Sub-problem {subProblemPath}", sb.ToString());
                return currentSolution;
            }
            else
            {
                // Branching message for this sub-problem
                sb.AppendLine($"Sub-problem {subProblemPath} will be branched on variable {fractionalVar.Item1} = {fractionalVar.Item2:F3}");

                mainSolution.AddStep($"Sub-problem {subProblemPath}", sb.ToString());

            }

            string varName = fractionalVar.Item1;
            double varValue = fractionalVar.Item2;
            int varIndex = GetVariableIndex(varName);

            double floorValue = Math.Floor(varValue);
            double ceilValue = Math.Ceiling(varValue);

            Solution bestSubSolution = null;
            double bestObjectiveValue = program.IsMaximization ? double.MinValue : double.MaxValue;

            // Sub-problem 1: x <= floor(value)
            string subPath1 = string.IsNullOrEmpty(subProblemPath) ? "1" : subProblemPath + ".1";
            mainSolution.AddMessage($"Sub-problem {subPath1}: {varName} <= {floorValue}");

            var program1 = program.Clone();
            AddBoundConstraint(program1, varIndex, floorValue, true); // <=
            var solution1 = SolveBranchAndBound(program1, mainSolution, subPath1, level + 1);
            if (solution1 != null && IsBetterSolution(solution1.OptimalValue, bestObjectiveValue, program.IsMaximization))
            {
                bestObjectiveValue = solution1.OptimalValue;
                bestSubSolution = solution1;
            }

            // Sub-problem 2: x >= ceil(value)
            string subPath2 = string.IsNullOrEmpty(subProblemPath) ? "2" : subProblemPath + ".2";
            mainSolution.AddMessage($"Sub-problem {subPath2}: {varName} >= {ceilValue}");

            var program2 = program.Clone();
            AddBoundConstraint(program2, varIndex, ceilValue, false); // >=
            var solution2 = SolveBranchAndBound(program2, mainSolution, subPath2, level + 1);
            if (solution2 != null && IsBetterSolution(solution2.OptimalValue, bestObjectiveValue, program.IsMaximization))
            {
                bestSubSolution = solution2;
            }

            return bestSubSolution;
        }

        private Tuple<string, double> FindMostFractionalVariable(Solution solution, LinearProgram program)
        {
            string mostFractionalVar = null;
            double mostFractionalValue = 0;
            double closestToHalf = double.MaxValue;
            int lowestVarIndex = int.MaxValue;

            // Sort decision variables by index to ensure consistent ordering
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

                    // Choose variable closest to 0.5, if tied then choose lowest index
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
            // Extract index from variable name (e.g., "x1" -> 1, "x2" -> 2)
            return int.Parse(varName.Substring(1));
        }

        private void AddBoundConstraint(LinearProgram program, int varIndex, double bound, bool isLessOrEqual)
        {
            var constraint = new LinearProgram.Constraint();

            // Initialize coefficients for all variables
            for (int i = 0; i < program.Variables.Count; i++)
            {
                constraint.Coefficients.Add(0);
            }

            // Set coefficient for the bounded variable
            constraint.Coefficients[varIndex - 1] = 1; // varIndex is 1-based, list is 0-based

            constraint.Relation = isLessOrEqual ?
                LinearProgram.Relation.LessThanOrEqual :
                LinearProgram.Relation.GreaterThanOrEqual;
            constraint.Rhs = bound;

            program.Constraints.Add(constraint);
        }

        private bool IsBetterSolution(double newValue, double currentBest, bool isMaximization)
        {
            if (isMaximization)
                return newValue > currentBest;
            else
                return newValue < currentBest;
        }

        private string FormatTableau(Solution solution)
        {
            if (solution.FinalTableau == null)
               return "Tableau not available";

            var sb = new StringBuilder();
            int rows = solution.FinalTableau.GetLength(0);
            int cols = solution.FinalTableau.GetLength(1);

            // Header row
            sb.Append("\t");
            for (int i = 1; i <= solution.VariableCount; i++)
                sb.Append($"x{i}\t");
            for (int i = 1; i <= solution.SlackCount; i++)
                sb.Append($"s{i}\t");
            for (int i = 1; i <= solution.ExcessCount; i++)
                sb.Append($"e{i}\t");
            for (int i = 1; i <= solution.ArtificialCount; i++)
                sb.Append($"a{i}\t");
            sb.AppendLine("RHS");

            // Tableau rows
            for (int i = 0; i < rows; i++)
            {
                if (i == 0)
                    sb.Append("z\t");
                else
                    sb.Append($"Con {i}\t");

                for (int j = 0; j < cols; j++)
                {
                    double value = solution.FinalTableau[i, j];
                    if (Math.Abs(value) < TOLERANCE)
                        value = 0;

                    sb.Append($"{FormatValue(value)}\t");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string FormatValue(double value)
        {
            if (Math.Abs(value) < TOLERANCE)
                value = 0;

            return value.ToString("F3"); // Always decimal, 3 decimal places
        }

        private string FormatCanonicalForm(LinearProgram program)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Canonical Form (with slack variables):");
            sb.AppendLine();

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
            sb.AppendLine();

            // Constraints in canonical form with slack variables
            int slackIndex = 1;
            for (int i = 0; i < program.Constraints.Count; i++)
            {
                var constraint = program.Constraints[i];

                // Decision variables
                bool first = true;
                for (int j = 0; j < constraint.Coefficients.Count; j++)
                {
                    double coeff = constraint.Coefficients[j];
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
                // For equality constraints, no slack variable needed

                sb.AppendLine($" = {constraint.Rhs:F0}");
            }

            sb.AppendLine();
            sb.AppendLine("All variables >= 0");

            return sb.ToString();
        }
    }
}