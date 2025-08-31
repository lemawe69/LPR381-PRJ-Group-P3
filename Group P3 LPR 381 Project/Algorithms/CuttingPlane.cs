using LinearProgrammingSolver.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LinearProgrammingSolver.Algorithms
{
    public class CuttingPlane : ISolver
    {
        private readonly DualSimplex _dualSimplex;
        private const double TOLERANCE = 1e-6;

        public CuttingPlane()
        {
            _dualSimplex = new DualSimplex();
        }

        public Solution Solve(LinearProgram program)
        {
            var solution = new Solution();
            solution.AddStep("Canonical Form", FormatCanonicalForm(program));

            // Step 1: Solve the LP relaxation using dual simplex
            var initialSolution = _dualSimplex.Solve(program);

            // Check if the initial solution is infeasible or unbounded
            if (initialSolution.Messages.Any(m => m.Contains("infeasible") || m.Contains("unbounded")))
            {
                solution.AddMessage("Initial LP relaxation is infeasible or unbounded. Cannot proceed with Cutting Plane.");
                foreach (var message in initialSolution.Messages)
                    solution.AddMessage(message);
                return solution;
            }

            // Add initial solution steps to main solution
            foreach (var step in initialSolution.Steps)
                solution.Steps.Add(step);

            int cutIteration = 0;
            var currentProgram = program.Clone();
            var currentSolution = initialSolution;

            while (true)
            {
                // Step 2: Find the basic variable with fractional value closest to 0.5
                var fractionalVar = FindMostFractionalVariable(currentSolution, currentProgram);

                if (fractionalVar == null)
                {
                    // All integer solution found
                    solution.OptimalValue = currentSolution.OptimalValue;
                    solution.VariableValues = currentSolution.VariableValues;
                    solution.AddMessage($"\nOptimal integer solution found with value: {currentSolution.OptimalValue:F3}");
                    break;
                }

                cutIteration++;
                solution.AddMessage($"\nCut {cutIteration}: Variable {fractionalVar.Item1} = {fractionalVar.Item2:F3} is fractional (distance from 0.5: {Math.Abs(fractionalVar.Item2 - Math.Floor(fractionalVar.Item2) - 0.5):F3})");

                // Step 3: Generate Gomory cut from the constraint row
                var cut = GenerateGomoryCut(currentSolution, fractionalVar.Item1, currentProgram);
                if (cut == null)
                {
                    solution.AddMessage("Unable to generate cut. Terminating.");
                    break;
                }
                solution.AddStep($"Cut {cutIteration} Generation", cut.GenerationSteps);

                // --- FIX: Add the new slack variable FIRST to avoid index/length mismatches ---
                var slackVar = new LinearProgram.Variable
                {
                    Index = currentProgram.Variables.Count + 1,
                    Coefficient = 0,
                    Type = LinearProgram.VariableType.NonNegative
                };
                currentProgram.Variables.Add(slackVar);

                // Ensure all EXISTING constraints have coefficient lists long enough for the new variable
                foreach (var constraint in currentProgram.Constraints)
                {
                    while (constraint.Coefficients.Count < currentProgram.Variables.Count)
                        constraint.Coefficients.Add(0);
                }

                // Make sure the cut constraint itself has the correct length
                while (cut.Constraint.Coefficients.Count < currentProgram.Variables.Count)
                    cut.Constraint.Coefficients.Add(0);

                // set the slack coefficient (last variable) = 1
                int slackZeroBasedIndex = currentProgram.Variables.Count - 1;
                cut.Constraint.Coefficients[slackZeroBasedIndex] = 1;

                // Now add the cut constraint to the model
                currentProgram.Constraints.Add(cut.Constraint);

                // Use the 1-based slack index when formatting for human output
                solution.AddMessage($"Added cut: {FormatConstraint(cut.Constraint, currentProgram.Variables.Count)}");

                // Step 4: Solve the updated LP using dual simplex
                currentSolution = _dualSimplex.Solve(currentProgram);

                if (currentSolution.Messages.Any(m => m.Contains("infeasible")))
                {
                    solution.AddMessage("Problem became infeasible after adding cut. No integer solution exists.");
                    foreach (var message in currentSolution.Messages)
                        solution.AddMessage(message);
                    return solution;
                }
                if (currentSolution.Messages.Any(m => m.Contains("unbounded")))
                {
                    solution.AddMessage("Problem became unbounded after adding cut.");
                    foreach (var message in currentSolution.Messages)
                        solution.AddMessage(message);
                    return solution;
                }

                // Add solution steps
                foreach (var step in currentSolution.Steps)
                    solution.Steps.Add(step);

                solution.AddMessage($"Cut {cutIteration} result: Optimal value = {currentSolution.OptimalValue:F3}");

                // Prevent infinite loops
                if (cutIteration >= 50)
                {
                    solution.AddMessage("Maximum number of cuts reached. Terminating.");
                    break;
                }
            }

            return solution;
        }

        private Tuple<string, double> FindMostFractionalVariable(Solution solution, LinearProgram program)
        {
            string mostFractionalVar = null;
            double mostFractionalValue = 0;
            double closestToHalf = double.MaxValue;
            int lowestVarIndex = int.MaxValue;

            // Only consider decision variables (x1, x2, etc.)
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

        private CutInfo GenerateGomoryCut(Solution solution, string fractionalVarName, LinearProgram program)
        {
            var cutInfo = new CutInfo();
            var sb = new StringBuilder();

            // Find the tableau row corresponding to the fractional variable
            if (solution.FinalTableau == null)
            {
                sb.AppendLine("Final tableau not available for cut generation.");
                cutInfo.GenerationSteps = sb.ToString();
                return null;
            }

            int varIndex = GetVariableIndex(fractionalVarName) - 1; // Convert to 0-based
            int pivotRow = FindBasicVariableRow(solution.FinalTableau, varIndex);
            if (pivotRow == -1)
            {
                sb.AppendLine($"Variable {fractionalVarName} is not basic. Cannot generate cut.");
                cutInfo.GenerationSteps = sb.ToString();
                return null;
            }

            sb.AppendLine($"Generating Gomory cut from row {pivotRow + 1} (basic variable {fractionalVarName}):");
            sb.AppendLine();

            // Step 3: Extract the constraint equation from the tableau row
            var tableau = solution.FinalTableau;
            int cols = tableau.GetLength(1);
            double rhs = tableau[pivotRow, cols - 1];

            sb.AppendLine("Step 1: Extract constraint equation from tableau:");
            sb.Append($"{fractionalVarName} = {rhs:F3}");

            var coefficients = new List<double>();
            var variableNames = GetVariableNames(program, solution);

            // Get ALL variable coefficients from the tableau row (basic variable has coefficient 1, others have their tableau values)
            for (int j = 0; j < cols - 1; j++)
            {
                double coeff = tableau[pivotRow, j];
                coefficients.Add(coeff);
                if (j < variableNames.Count && j != varIndex) // Don't show the basic variable itself
                {
                    if (Math.Abs(coeff) > TOLERANCE)
                    {
                        if (coeff > 0)
                            sb.Append($" + {coeff:F3}{variableNames[j]}");
                        else
                            sb.Append($" - {Math.Abs(coeff):F3}{variableNames[j]}");
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine();

            // Step 4 & 5: Split integer and fractional parts, ensuring positive fractions
            sb.AppendLine("Step 2: Split into integer and fractional parts (ensuring positive fractions):");

            var integerParts = new List<double>();
            var fractionalParts = new List<double>();

            // Process RHS
            double rhsInteger = Math.Floor(rhs);
            double rhsFractional = rhs - rhsInteger;

            // If RHS fractional part is negative, adjust
            if (rhsFractional < 0)
            {
                rhsInteger -= 1;
                rhsFractional = rhs - rhsInteger;
            }

            sb.AppendLine($"RHS: {rhs:F3} = {rhsInteger:F0} + {rhsFractional:F3}");

            // Process ALL coefficients (including basic variable which should be 0 for cut generation)
            for (int j = 0; j < cols - 1; j++)
            {
                double coeff = tableau[pivotRow, j];

                // For the basic variable (current fractional variable), coefficient should be treated as 0 for cut
                if (j == varIndex)
                {
                    integerParts.Add(0);
                    fractionalParts.Add(0);
                }
                else
                {
                    double intPart = Math.Floor(coeff);
                    double fracPart = coeff - intPart;
                    // Ensure fractional part is positive
                    if (fracPart < 0)
                    {
                        intPart -= 1;
                        fracPart = coeff - intPart;
                    }
                    integerParts.Add(intPart);
                    fractionalParts.Add(fracPart);
                    if (j < variableNames.Count && Math.Abs(coeff) > TOLERANCE)
                    {
                        sb.AppendLine($"{variableNames[j]}: {coeff:F3} = {intPart:F0} + {fracPart:F3}");
                    }
                }
            }

            sb.AppendLine();

            // Step 6: Rearrange equation
            sb.AppendLine("Step 3: Rearrange - move integers to left, fractions to right:");
            sb.Append($"{fractionalVarName}");

            // Add integer parts to left side (with opposite sign)
            for (int j = 0; j < Math.Min(integerParts.Count, variableNames.Count); j++)
            {
                if (Math.Abs(integerParts[j]) > TOLERANCE && !IsBasicVariable(tableau, j))
                {
                    sb.Append($" - ({integerParts[j]:F0}){variableNames[j]}");
                }
            }

            sb.Append($" - ({rhsInteger:F0}) = ");

            // Add fractional parts to right side (with opposite sign)
            bool first = true;
            for (int j = 0; j < Math.Min(fractionalParts.Count, variableNames.Count); j++)
            {
                if (Math.Abs(fractionalParts[j]) > TOLERANCE && !IsBasicVariable(tableau, j))
                {
                    if (!first) sb.Append(" + ");
                    sb.Append($"{fractionalParts[j]:F3}{variableNames[j]}");
                    first = false;
                }
            }

            if (!first) sb.Append(" - ");
            else sb.Append("-");

            sb.AppendLine($"{rhsFractional:F3}");
            sb.AppendLine();

            // Step 7: Create the cut constraint (fractional part <= 0), canonicalized as equality with new slack
            sb.AppendLine("Step 4: Generate cut constraint (fractional part ≤ 0):");
            var cutConstraint = new LinearProgram.Constraint();

            // Initialize coefficients to the correct total length (all current decision vars + existing slacks)
            int totalVars = Math.Max(program.Variables.Count, fractionalParts.Count);
            for (int i = 0; i < totalVars; i++)
                cutConstraint.Coefficients.Add(0);

            sb.Append("Cut: ");
            bool firstTerm = true;

            // Build the left-hand fractional coefficients (only for non-basic variables considered)
            for (int j = 0; j < fractionalParts.Count; j++)
            {
                if (Math.Abs(fractionalParts[j]) <= TOLERANCE) continue;

                string varName = (j < variableNames.Count) ? variableNames[j] : $"v{j + 1}";
                if (!firstTerm)
                {
                    sb.Append(fractionalParts[j] > 0 ? " + " : " - ");
                }
                else if (fractionalParts[j] < 0)
                {
                    sb.Append("-");
                }
                sb.Append($"{Math.Abs(fractionalParts[j]):F3}{varName}");

                // Set coefficient in the constraint vector (only if within bounds)
                if (j < cutConstraint.Coefficients.Count)
                    cutConstraint.Coefficients[j] = fractionalParts[j];

                firstTerm = false;
            }

            sb.AppendLine($" ≤ {rhsFractional:F3}");
            sb.AppendLine();

            // Canonical form (add slack variable)
            sb.AppendLine("In canonical form with slack variable:");
            sb.Append("Cut: ");
            firstTerm = true;
            for (int j = 0; j < fractionalParts.Count; j++)
            {
                if (Math.Abs(fractionalParts[j]) <= TOLERANCE) continue;
                string varName = (j < variableNames.Count) ? variableNames[j] : $"v{j + 1}";
                if (!firstTerm)
                {
                    sb.Append(fractionalParts[j] > 0 ? " + " : " - ");
                }
                else if (fractionalParts[j] < 0)
                {
                    sb.Append("-");
                }
                sb.Append($"{Math.Abs(fractionalParts[j]):F3}{varName}");
                firstTerm = false;
            }

            // The printed slack index is program.Variables.Count + 1 because we WILL add the slack before inserting the constraint
            sb.AppendLine($" + s{program.Variables.Count + 1} = {-rhsFractional:F3}");

            // set relation and RHS on the constraint object (canonical equality)
            cutConstraint.Relation = LinearProgram.Relation.Equal;
            cutConstraint.Rhs = -rhsFractional;

            cutInfo.Constraint = cutConstraint;
            cutInfo.GenerationSteps = sb.ToString();

            return cutInfo;
        }

        private List<string> GetVariableNames(LinearProgram program, Solution solution)
        {
            var names = new List<string>();

            // Decision variables
            for (int i = 0; i < program.Variables.Count; i++)
            {
                names.Add($"x{i + 1}");
            }
            // Slack variables
            for (int i = 0; i < solution.SlackCount; i++)
            {
                names.Add($"s{i + 1}");
            }
            // Excess variables
            for (int i = 0; i < solution.ExcessCount; i++)
            {
                names.Add($"e{i + 1}");
            }
            // Artificial variables
            for (int i = 0; i < solution.ArtificialCount; i++)
            {
                names.Add($"a{i + 1}");
            }
            return names;
        }

        private int FindBasicVariableRow(double[,] tableau, int varColumn)
        {
            int rows = tableau.GetLength(0);
            for (int i = 1; i < rows; i++) // Start from 1 to skip objective row
            {
                if (Math.Abs(tableau[i, varColumn] - 1.0) < TOLERANCE)
                {
                    // Check if this is the only non-zero entry in the column
                    bool isBasic = true;
                    for (int k = 0; k < rows; k++)
                    {
                        if (k != i && Math.Abs(tableau[k, varColumn]) > TOLERANCE)
                        {
                            isBasic = false;
                            break;
                        }
                    }
                    if (isBasic)
                        return i;
                }
            }
            return -1;
        }

        private bool IsBasicVariable(double[,] tableau, int column)
        {
            int rows = tableau.GetLength(0);
            int onesCount = 0;
            for (int i = 0; i < rows; i++)
            {
                if (Math.Abs(tableau[i, column] - 1.0) < TOLERANCE)
                    onesCount++;
                else if (Math.Abs(tableau[i, column]) > TOLERANCE)
                    return false; // Non-zero, non-one entry found
            }
            return onesCount == 1;
        }

        private string FormatConstraint(LinearProgram.Constraint constraint, int variableCount)
{
    var sb = new StringBuilder();
    bool first = true;

    for (int i = 0; i < constraint.Coefficients.Count; i++)
    {
        double coeff = constraint.Coefficients[i];
        if (Math.Abs(coeff) <= TOLERANCE) continue;

        // Decide whether this is a decision or slack variable
        string varName = i < variableCount ? $"x{i + 1}" : $"s{i - variableCount + 1}";

        if (!first && coeff > 0) sb.Append(" + ");
        if (coeff < 0) sb.Append(" - ");
        else if (!first) sb.Append(" + ");

        sb.Append($"{Math.Abs(coeff):F3}{varName}");
        first = false;
    }

    sb.Append($" = {constraint.Rhs:F3}");
    return sb.ToString();
}


        public string FormatCanonicalForm(LinearProgram program)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Canonical Form (with slack variables):");

            // Objective function in canonical form: z - c1*x1 - c2*x2 - . = 0
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

            sb.AppendLine();
            sb.AppendLine("All variables >= 0");

            return sb.ToString();
        }

        private class CutInfo
        {
            public LinearProgram.Constraint Constraint { get; set; }
            public string GenerationSteps { get; set; }
        }
    }
}
