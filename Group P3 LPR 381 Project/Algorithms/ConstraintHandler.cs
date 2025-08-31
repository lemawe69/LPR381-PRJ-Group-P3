using LinearProgrammingSolver.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static LinearProgrammingSolver.Models.LinearProgram;

namespace Group_P3_LPR_381_Project.Algorithms
{
    internal class ConstraintHandler
    {
        private const double TOL = 1e-6;

        public class ConstraintAdditionResult
        {
            public double[,] NewTableau { get; set; }
            public List<string> NewAuxiliaryVariableNames { get; set; }
            public List<string> Messages { get; set; }
            public bool RequiresDualSimplex { get; set; }
            public int NewSlackCount { get; set; }
            public int NewExcessCount { get; set; }
            public int NewArtificialCount { get; set; }

            public ConstraintAdditionResult()
            {
                Messages = new List<string>();
            }
        }

        public ConstraintAdditionResult AddConstraint(
            double[,] currentTableau,
            LinearProgram.Constraint constraint,
            List<string> auxiliaryVariableNames,
            int variableCount,
            int slackCount,
            int excessCount,
            int artificialCount)
        {
            var result = new ConstraintAdditionResult();

            int currentRows = currentTableau.GetLength(0);
            int currentCols = currentTableau.GetLength(1);

            // Determine new auxiliary variable based on constraint type
            string newAuxVarName = "";
            result.NewSlackCount = slackCount;
            result.NewExcessCount = excessCount;
            result.NewArtificialCount = artificialCount;

            switch (constraint.Relation)
            {
                case LinearProgram.Relation.LessThanOrEqual:
                    result.NewSlackCount++;
                    newAuxVarName = $"s{result.NewSlackCount}";
                    break;
                case LinearProgram.Relation.GreaterThanOrEqual:
                    result.NewExcessCount++;
                    newAuxVarName = $"e{result.NewExcessCount}";
                    break;
                case LinearProgram.Relation.Equal:
                    // Per document: Split = into >= and <=
                    // This requires adding two rows: one >= with excess (+1 after -1 multiply), one <= with slack (+1)
                    // But for compatibility, we'll use artificial for = as in original code
                    result.NewArtificialCount++;
                    newAuxVarName = $"a{result.NewArtificialCount}";
                    break;
            }

            // Create new tableau with additional row and column
            int newRows = currentRows + 1;
            int newCols = currentCols + 1;
            result.NewTableau = new double[newRows, newCols];

            // Copy existing tableau
            for (int i = 0; i < currentRows; i++)
            {
                for (int j = 0; j < currentCols - 1; j++)
                {
                    result.NewTableau[i, j] = currentTableau[i, j];
                }
                result.NewTableau[i, newCols - 2] = 0; // New aux column
                result.NewTableau[i, newCols - 1] = currentTableau[i, currentCols - 1];
            }

            // Add the new constraint row
            int newConstraintRow = newRows - 1;

            // Copy coefficients
            for (int j = 0; j < variableCount; j++)
            {
                result.NewTableau[newConstraintRow, j] = j < constraint.Coefficients.Count ? constraint.Coefficients[j] : 0;
            }

            // Set zeros for existing aux variables
            for (int j = variableCount; j < newCols - 2; j++)
            {
                result.NewTableau[newConstraintRow, j] = 0;
            }

            // Set new aux coefficient
            switch (constraint.Relation)
            {
                case LinearProgram.Relation.LessThanOrEqual:
                    result.NewTableau[newConstraintRow, newCols - 2] = 1;
                    break;
                case LinearProgram.Relation.GreaterThanOrEqual:
                    // Multiply by -1 for dual simplex
                    for (int j = 0; j < newCols - 1; j++)
                    {
                        result.NewTableau[newConstraintRow, j] *= -1;
                    }
                    result.NewTableau[newConstraintRow, newCols - 1] = -constraint.Rhs;
                    result.NewTableau[newConstraintRow, newCols - 2] = 1; // Positive excess
                    break;
                case LinearProgram.Relation.Equal:
                    result.NewTableau[newConstraintRow, newCols - 2] = 1;
                    result.NewTableau[0, newCols - 2] = -1000; // Big-M penalty
                    break;
            }

            result.NewTableau[newConstraintRow, newCols - 1] = constraint.Relation == Relation.GreaterThanOrEqual ? -constraint.Rhs : constraint.Rhs;

            result.NewAuxiliaryVariableNames = new List<string>(auxiliaryVariableNames);
            result.NewAuxiliaryVariableNames.Add(newAuxVarName);

            result.Messages.Add($"Added new constraint with {newAuxVarName}");

            // Fix basic variables
            result = FixBasicVariables(result, variableCount);

            // Fix auxiliary coefficient
            result = FixNegativeAuxiliaryVariable(result);

            // Check if dual simplex is required
            result.RequiresDualSimplex = HasNegativeRHS(result.NewTableau);

            return result;
        }

        private ConstraintAdditionResult FixBasicVariables(ConstraintAdditionResult result, int variableCount)
        {
            int rows = result.NewTableau.GetLength(0);
            int cols = result.NewTableau.GetLength(1);
            int newConstraintRow = rows - 1;

            // Check each decision variable
            for (int varCol = 0; varCol < variableCount; varCol++)
            {
                int basicRow = -1;
                for (int i = 1; i < rows - 1; i++) // Exclude objective and new constraint
                {
                    if (Math.Abs(result.NewTableau[i, varCol] - 1.0) < TOL)
                    {
                        bool isBasic = true;
                        for (int k = 0; k < rows - 1; k++)
                        {
                            if (k != i && k != 0 && Math.Abs(result.NewTableau[k, varCol]) > TOL)
                            {
                                isBasic = false;
                                break;
                            }
                        }

                        if (isBasic)
                        {
                            basicRow = i;
                            break;
                        }
                    }
                }

                if (basicRow != -1 && Math.Abs(result.NewTableau[newConstraintRow, varCol]) > TOL)
                {
                    result.Messages.Add($"Problem 1: Basic variable x{varCol + 1} was basic in row {basicRow + 1}, but now has coefficient {result.NewTableau[newConstraintRow, varCol]:F3} in new constraint. Fixing by subtracting row {basicRow + 1} - new constraint.");

                    // To match example's Con 2 - Con 3, use newConstraintRow = basicRow - newConstraintRow
                    for (int j = 0; j < cols; j++)
                    {
                        result.NewTableau[newConstraintRow, j] = result.NewTableau[basicRow, j] - result.NewTableau[newConstraintRow, j];
                    }
                }
            }

            return result;
        }

        private ConstraintAdditionResult FixNegativeAuxiliaryVariable(ConstraintAdditionResult result)
        {
            int rows = result.NewTableau.GetLength(0);
            int cols = result.NewTableau.GetLength(1);
            int newConstraintRow = rows - 1;
            int newAuxVarCol = cols - 2;

            string auxVarName = result.NewAuxiliaryVariableNames.Last();
            double auxCoefficient = result.NewTableau[newConstraintRow, newAuxVarCol];

            if (auxVarName.StartsWith("s"))
            {
                if (auxCoefficient < -TOL)
                {
                    result.Messages.Add("Problem 2: The new slack variable (s) coefficient is negative. Fix by multiplying the row by -1.");
                    for (int j = 0; j < cols; j++)
                    {
                        result.NewTableau[newConstraintRow, j] *= -1;
                    }
                }
            }
            else if (auxVarName.StartsWith("e"))
            {
                if (auxCoefficient < -TOL)
                {
                    result.Messages.Add("Problem 2: The new excess variable (e) coefficient is negative. Fix by multiplying the row by -1 to make positive for dual simplex.");
                    for (int j = 0; j < cols; j++)
                    {
                        result.NewTableau[newConstraintRow, j] *= -1;
                    }
                }
                else
                {
                    result.Messages.Add("Excess variable coefficient is positive, ready for dual simplex.");
                }
            }
            else if (auxVarName.StartsWith("a"))
            {
                if (auxCoefficient < -TOL)
                {
                    result.Messages.Add("Problem 2: Artificial variable coefficient is negative. Fix by multiplying the row by -1.");
                    for (int j = 0; j < cols; j++)
                    {
                        result.NewTableau[newConstraintRow, j] *= -1;
                    }
                }
            }

            return result;
        }

        private bool HasNegativeRHS(double[,] tableau)
        {
            int rows = tableau.GetLength(0);
            int cols = tableau.GetLength(1);

            for (int i = 1; i < rows; i++)
            {
                if (tableau[i, cols - 1] < -TOL)
                    return true;
            }
            return false;
        }

        public string TableauToString(double[,] tableau, List<string> variableNames, List<string> auxiliaryVariableNames)
        {
            var sb = new StringBuilder();
            int rows = tableau.GetLength(0);
            int cols = tableau.GetLength(1);

            sb.AppendLine("Tableau:");

            sb.Append("        ");

            for (int j = 0; j < variableNames.Count; j++)
                sb.Append($"{variableNames[j],-10}");

            foreach (var auxVarName in auxiliaryVariableNames)
            {
                sb.Append($"{auxVarName,-10}");
            }

            sb.AppendLine("RHS");

            for (int i = 0; i < rows; i++)
            {
                if (i == 0)
                    sb.Append("Z:      ");
                else
                    sb.Append($"Con {i}:  ");

                for (int j = 0; j < cols; j++)
                {
                    double value = tableau[i, j];
                    if (Math.Abs(value) < TOL)
                        value = 0;

                    sb.Append($"{value,10:F3}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}