using LinearProgrammingSolver.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LinearProgrammingSolver.Algorithms
{
    public class CuttingPlane : ISolver
    {
        private const double TOL = 1e-6;

        public Solution Solve(LinearProgram program)
        {
            var solution = new Solution();

            // Step 1: Solve the LP relaxation using dual simplex
            var dualSimplex = new DualSimplex();
            var lpSolution = dualSimplex.Solve(program);

            if (lpSolution.OptimalValue == null || lpSolution.VariableValues == null)
            {
                solution.AddMessage("LP relaxation failed to find optimal solution");
                return solution;
            }

            solution.AddMessage("Starting Cutting Plane Algorithm");
            solution.AddMessage($"LP Relaxation Optimal Value: {lpSolution.OptimalValue:F4}");
            solution.AddStep("LP Relaxation Solution:", FormatSolution(lpSolution.VariableValues));

            // Create a working tableau from the LP solution
            var tableau = new CuttingPlaneTableau(lpSolution.FinalTableau, program);
            solution.AddStep("Initial Optimal Tableau:", tableau.ToString());

            int iteration = 0;
            int maxIterations = 50;

            while (iteration < maxIterations)
            {
                // Step 2: Find the basic variable with fractional part closest to 0.5
                var fractionalVar = FindMostFractionalVariable(tableau, program);

                if (fractionalVar == null)
                {
                    solution.AddMessage("Integer optimal solution found!");
                    break;
                }

                iteration++;
                solution.AddMessage($"Iteration {iteration}: Found fractional variable {fractionalVar.VariableName} = {fractionalVar.Value:F4} (fractional part: {fractionalVar.FractionalPart:F4})");

                // Step 3-8: Generate Gomory cut
                var cut = GenerateGomoryCut(tableau, fractionalVar.Row, iteration);
                solution.AddStep($"Generated Gomory Cut {iteration}:", cut.ToString());

                // Step 9: Add the cut to the tableau
                tableau.AddCut(cut);
                solution.AddStep($"Tableau after adding Cut {iteration}:", tableau.ToString());

                // Solve the new problem using dual simplex
                bool solved = SolveDualSimplex(tableau, solution, iteration);

                if (!solved)
                {
                    solution.AddMessage("Failed to solve after adding cut - problem may be infeasible");
                    break;
                }

                solution.AddStep($"Tableau after solving Cut {iteration}:", tableau.ToString());
            }

            if (iteration >= maxIterations)
            {
                solution.AddMessage("Maximum iterations reached");
            }

            // Extract final solution
            solution.OptimalValue = tableau.GetObjectiveValue();
            solution.VariableValues = tableau.GetSolution(program);
            solution.AddStep("Final Integer Solution:", FormatSolution(solution.VariableValues));

            return solution;
        }

        private FractionalVariable FindMostFractionalVariable(CuttingPlaneTableau tableau, LinearProgram program)
        {
            FractionalVariable bestVar = null;
            double closestTo05 = double.MaxValue;

            // Check decision variables only
            for (int j = 0; j < program.Variables.Count; j++)
            {
                // Find if this variable is basic
                int basicRow = tableau.FindBasicRow(j);
                if (basicRow != -1)
                {
                    double value = tableau.GetValue(basicRow, tableau.Cols - 1);
                    double fractionalPart = value - Math.Floor(value);

                    if (fractionalPart > TOL && fractionalPart < 1 - TOL)
                    {
                        double distanceFrom05 = Math.Abs(fractionalPart - 0.5);

                        if (distanceFrom05 < closestTo05 ||
                            (Math.Abs(distanceFrom05 - closestTo05) < TOL && (bestVar == null || j < bestVar.ColumnIndex)))
                        {
                            closestTo05 = distanceFrom05;
                            bestVar = new FractionalVariable
                            {
                                ColumnIndex = j,
                                Row = basicRow,
                                Value = value,
                                FractionalPart = fractionalPart,
                                VariableName = $"x{j + 1}"
                            };
                        }
                    }
                }
            }

            return bestVar;
        }

        private GomoryCut GenerateGomoryCut(CuttingPlaneTableau tableau, int pivotRow, int cutNumber)
        {
            var cut = new GomoryCut { CutNumber = cutNumber };

            // Extract the constraint equation from the chosen row
            for (int j = 0; j < tableau.Cols - 1; j++)
            {
                cut.OriginalCoefficients[j] = tableau.GetValue(pivotRow, j);
            }
            cut.OriginalRHS = tableau.GetValue(pivotRow, tableau.Cols - 1);

            // Generate fractional parts (always between 0 and 1)
            for (int j = 0; j < tableau.Cols - 1; j++)
            {
                double coeff = cut.OriginalCoefficients[j];
                double fractionalPart = coeff - Math.Floor(coeff);
                if (fractionalPart < 0) fractionalPart += 1;
                cut.FractionalParts[j] = fractionalPart;
            }

            double rhsFractional = cut.OriginalRHS - Math.Floor(cut.OriginalRHS);
            if (rhsFractional < 0) rhsFractional += 1;
            cut.RHSFractional = rhsFractional;

            // Create the Gomory cut: sum(f_j * x_j) >= f_0
            // In standard form with slack: sum(f_j * x_j) - s = f_0
            // For dual simplex (negative RHS): -sum(f_j * x_j) + s = -f_0
            for (int j = 0; j < tableau.Cols - 1; j++)
            {
                cut.CutCoefficients[j] = -cut.FractionalParts[j];
            }
            cut.CutRHS = -rhsFractional;

            return cut;
        }

        private bool SolveDualSimplex(CuttingPlaneTableau tableau, Solution solution, int iteration)
        {
            int maxDualIterations = 100;
            int dualIteration = 0;

            while (tableau.HasNegativeRHS() && dualIteration < maxDualIterations)
            {
                dualIteration++;

                int pivotRow = tableau.FindDualPivotRow();
                if (pivotRow == -1) return false;

                int pivotCol = tableau.FindDualPivotColumn(pivotRow);
                if (pivotCol == -1)
                {
                    solution.AddMessage($"Cut {iteration}: No valid dual pivot column - problem is infeasible");
                    return false;
                }

                tableau.Pivot(pivotRow, pivotCol);
                solution.AddStep($"Cut {iteration}, Dual Iteration {dualIteration}: Pivot({pivotRow}, {pivotCol})", tableau.ToString());
            }

            return dualIteration < maxDualIterations;
        }

        private string FormatSolution(Dictionary<string, double> variables)
        {
            var sb = new StringBuilder();
            foreach (var kvp in variables.OrderBy(x => x.Key))
            {
                sb.AppendLine($"{kvp.Key} = {kvp.Value:F4}");
            }
            return sb.ToString();
        }
    }

    public class FractionalVariable
    {
        public int ColumnIndex { get; set; }
        public int Row { get; set; }
        public double Value { get; set; }
        public double FractionalPart { get; set; }
        public string VariableName { get; set; }
    }

    public class GomoryCut
    {
        public int CutNumber { get; set; }
        public Dictionary<int, double> OriginalCoefficients { get; set; } = new Dictionary<int, double>();
        public double OriginalRHS { get; set; }
        public Dictionary<int, double> FractionalParts { get; set; } = new Dictionary<int, double>();
        public double RHSFractional { get; set; }
        public Dictionary<int, double> CutCoefficients { get; set; } = new Dictionary<int, double>();
        public double CutRHS { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Gomory Cut {CutNumber}:");
            sb.AppendLine($"Source row RHS: {OriginalRHS:F4}, fractional part: {RHSFractional:F4}");

            // Original constraint
            sb.Append("Source constraint: ");
            bool first = true;
            foreach (var kvp in OriginalCoefficients.Where(x => Math.Abs(x.Value) > 1e-6))
            {
                if (!first && kvp.Value >= 0) sb.Append(" + ");
                else if (!first) sb.Append(" ");
                sb.Append($"{kvp.Value:F4}*x{kvp.Key + 1}");
                first = false;
            }
            sb.AppendLine($" = {OriginalRHS:F4}");

            // Fractional parts
            sb.Append("Fractional parts: ");
            first = true;
            foreach (var kvp in FractionalParts.Where(x => Math.Abs(x.Value) > 1e-6))
            {
                if (!first) sb.Append(" + ");
                sb.Append($"{kvp.Value:F4}*f{kvp.Key + 1}");
                first = false;
            }
            sb.AppendLine($" >= {RHSFractional:F4}");

            // Cut constraint
            sb.Append("Cut constraint: ");
            first = true;
            foreach (var kvp in CutCoefficients.Where(x => Math.Abs(x.Value) > 1e-6))
            {
                if (!first && kvp.Value >= 0) sb.Append(" + ");
                else if (!first) sb.Append(" ");
                sb.Append($"{kvp.Value:F4}*x{kvp.Key + 1}");
                first = false;
            }
            sb.AppendLine($" + s{CutNumber} = {CutRHS:F4}");

            return sb.ToString();
        }
    }

    public class CuttingPlaneTableau
    {
        private double[,] _matrix;
        private int _rows;
        private int _cols;
        private const double TOL = 1e-6;

        public int Rows => _rows;
        public int Cols => _cols;

        public CuttingPlaneTableau(double[,] initialTableau, LinearProgram program)
        {
            _rows = initialTableau.GetLength(0);
            _cols = initialTableau.GetLength(1);
            _matrix = new double[_rows, _cols];

            for (int i = 0; i < _rows; i++)
                for (int j = 0; j < _cols; j++)
                    _matrix[i, j] = initialTableau[i, j];
        }

        public double GetValue(int row, int col) => _matrix[row, col];

        public double GetObjectiveValue() => _matrix[0, _cols - 1];

        public bool HasNegativeRHS()
        {
            for (int i = 1; i < _rows; i++)
                if (_matrix[i, _cols - 1] < -TOL)
                    return true;
            return false;
        }

        public int FindBasicRow(int col)
        {
            int basicRow = -1;
            bool foundOne = false;

            for (int i = 1; i < _rows; i++) // Skip objective row
            {
                if (Math.Abs(_matrix[i, col] - 1.0) < TOL)
                {
                    if (foundOne) return -1; // Multiple 1s - not basic
                    basicRow = i;
                    foundOne = true;
                }
                else if (Math.Abs(_matrix[i, col]) > TOL)
                {
                    return -1; // Non-zero, non-one element - not basic
                }
            }

            // Also check objective row for non-zero (should be 0 for basic variable)
            if (foundOne && Math.Abs(_matrix[0, col]) > TOL)
                return -1;

            return basicRow;
        }

        public void AddCut(GomoryCut cut)
        {
            // Create new expanded matrix
            var newMatrix = new double[_rows + 1, _cols + 1];

            // Copy existing tableau structure carefully
            for (int i = 0; i < _rows; i++)
            {
                // Copy all columns except RHS
                for (int j = 0; j < _cols - 1; j++)
                    newMatrix[i, j] = _matrix[i, j];

                // Add new slack variable column (0 for existing rows)
                newMatrix[i, _cols - 1] = 0.0;

                // Copy RHS to new last column
                newMatrix[i, _cols] = _matrix[i, _cols - 1];
            }

            // Add the cut constraint row
            int cutRow = _rows;
            for (int j = 0; j < _cols - 1; j++)
            {
                if (cut.CutCoefficients.ContainsKey(j) && Math.Abs(cut.CutCoefficients[j]) > TOL)
                    newMatrix[cutRow, j] = cut.CutCoefficients[j];
                else
                    newMatrix[cutRow, j] = 0.0;
            }

            // Set new slack variable coefficient to 1
            newMatrix[cutRow, _cols - 1] = 1.0;

            // Set RHS
            newMatrix[cutRow, _cols] = cut.CutRHS;

            _matrix = newMatrix;
            _rows++;
            _cols++;
        }

        public int FindDualPivotRow()
        {
            int pivotRow = -1;
            double mostNegative = -TOL;

            for (int i = 1; i < _rows; i++)
            {
                double rhs = _matrix[i, _cols - 1];
                if (rhs < mostNegative)
                {
                    mostNegative = rhs;
                    pivotRow = i;
                }
            }
            return pivotRow;
        }

        public int FindDualPivotColumn(int pivotRow)
        {
            int pivotCol = -1;
            double minRatio = double.PositiveInfinity;

            for (int j = 0; j < _cols - 1; j++)
            {
                double constraintCoeff = _matrix[pivotRow, j];
                double reducedCost = _matrix[0, j];

                if (constraintCoeff < -TOL)
                {
                    double ratio = Math.Abs(reducedCost / constraintCoeff);
                    if (ratio < minRatio)
                    {
                        minRatio = ratio;
                        pivotCol = j;
                    }
                }
            }
            return pivotCol;
        }

        public void Pivot(int pivotRow, int pivotCol)
        {
            double pivotElement = _matrix[pivotRow, pivotCol];

            if (Math.Abs(pivotElement) < TOL)
                throw new InvalidOperationException($"Pivot element too small: {pivotElement}");

            // Normalize pivot row
            for (int j = 0; j < _cols; j++)
                _matrix[pivotRow, j] /= pivotElement;

            // Eliminate column in other rows
            for (int i = 0; i < _rows; i++)
            {
                if (i == pivotRow) continue;

                double multiplier = _matrix[i, pivotCol];
                if (Math.Abs(multiplier) > TOL)
                {
                    for (int j = 0; j < _cols; j++)
                        _matrix[i, j] -= multiplier * _matrix[pivotRow, j];
                }
            }
        }

        public Dictionary<string, double> GetSolution(LinearProgram program)
        {
            var solution = new Dictionary<string, double>();

            // Initialize all decision variables to 0
            for (int j = 0; j < program.Variables.Count; j++)
                solution[$"x{j + 1}"] = 0.0;

            // Find basic decision variables
            for (int j = 0; j < program.Variables.Count; j++)
            {
                int basicRow = FindBasicRow(j);
                if (basicRow != -1)
                {
                    double value = _matrix[basicRow, _cols - 1];
                    solution[$"x{j + 1}"] = Math.Max(0.0, value);
                }
            }

            return solution;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Cutting Plane Tableau:");

            // Column headers
            sb.Append("        ");
            for (int j = 0; j < _cols - 1; j++)
            {
                if (j < 10)
                    sb.Append($"x{j + 1}     ");
                else
                    sb.Append($"Col{j + 1,-6}");
            }
            sb.AppendLine("RHS");

            // Tableau rows
            for (int i = 0; i < _rows; i++)
            {
                sb.Append(i == 0 ? "Obj:    " : $"Row{i,-2}:  ");
                for (int j = 0; j < _cols; j++)
                    sb.Append($"{_matrix[i, j],8:F3}");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}