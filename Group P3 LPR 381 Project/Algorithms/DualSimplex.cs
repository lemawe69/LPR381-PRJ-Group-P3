using LinearProgrammingSolver.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LinearProgrammingSolver.Algorithms
{
    public class DualSimplex : ISolver
    {
        public Solution Solve(LinearProgram program)
        {
            var solution = new Solution();
            var tableau = new DualTableau(program);

            // Store tableau metadata in Solution
            solution.VariableCount = program.Variables.Count;
            solution.SlackCount = program.Constraints.Count(c => c.Relation == LinearProgram.Relation.LessThanOrEqual);
            solution.ExcessCount = program.Constraints.Count(c => c.Relation == LinearProgram.Relation.GreaterThanOrEqual);
            solution.ArtificialCount = program.Constraints.Count(c => c.Relation == LinearProgram.Relation.Equal);

            if (!program.IsMaximization)
                solution.AddMessage("Converted minimization problem to maximization by negating objective");

            if (program.Variables.Any(v => v.Type == LinearProgram.VariableType.Binary))
                solution.AddMessage("Warning: Binary variables detected. Solving as continuous relaxation. Use Branch-and-Bound for integer solutions.");

            solution.AddStep("Initial Tableau:", tableau.ToString());

            // Phase 1: Dual Simplex (make RHS feasible)
            while (tableau.HasNegativeRHS())
            {
                int pivotRow = tableau.FindDualPivotRow();
                if (pivotRow == -1)
                {
                    solution.AddMessage("Problem is infeasible (no pivot row found)");
                    return solution;
                }

                int pivotCol = tableau.FindDualPivotColumn(pivotRow);
                if (pivotCol == -1)
                {
                    solution.AddMessage("Problem is infeasible (no valid dual pivot column found)");
                    return solution;
                }

                tableau.Pivot(pivotRow, pivotCol);
                solution.AddStep($"Dual Iteration {tableau.IterationCount}: Pivot on constraint {pivotRow}, column {pivotCol + 1}", tableau.ToString());
            }

            solution.AddMessage("Feasible solution found, switching to Primal Simplex");

            // Phase 2: Primal Simplex (optimize)
            while (!tableau.IsOptimal())
            {
                int pivotCol = tableau.FindPivotColumn();
                if (pivotCol == -1)
                {
                    solution.AddMessage("Problem is unbounded (no entering variable found)");
                    return solution;
                }

                int pivotRow = tableau.FindPivotRow(pivotCol);
                if (pivotRow == -1)
                {
                    solution.AddMessage("Problem is unbounded (no pivot row found)");
                    return solution;
                }

                tableau.Pivot(pivotRow, pivotCol);
                solution.AddStep($"Primal Iteration {tableau.IterationCount}: Pivot on constraint {pivotRow}, column {pivotCol + 1}", tableau.ToString());
            }

            solution.OptimalValue = tableau.GetObjectiveValue();
            solution.VariableValues = tableau.GetSolution();
            solution.AddStep("Final Tableau:", tableau.ToString());

            // Store the final tableau matrix
            solution.FinalTableau = new double[tableau.Rows, tableau.Cols];
            for (int i = 0; i < tableau.Rows; i++)
                for (int j = 0; j < tableau.Cols; j++)
                    solution.FinalTableau[i, j] = tableau.GetValue(i, j);

            return solution;
        }
    }

    public class DualTableau
    {
        private double[,] _matrix;
        private readonly int _rows;
        private readonly int _cols;
        private readonly LinearProgram _program;
        public int IterationCount { get; private set; }
        private readonly int _slackCount;
        private readonly int _excessCount;
        private readonly int _artificialCount;
        private const double TOL = 1e-6;

        public int Rows => _rows;
        public int Cols => _cols;

        public DualTableau(LinearProgram program)
        {
            _program = program ?? throw new ArgumentNullException(nameof(program));
            _slackCount = program.Constraints.Count(c => c.Relation == LinearProgram.Relation.LessThanOrEqual);
            _excessCount = program.Constraints.Count(c => c.Relation == LinearProgram.Relation.GreaterThanOrEqual);
            _artificialCount = program.Constraints.Count(c => c.Relation == LinearProgram.Relation.Equal);
            _rows = program.Constraints.Count + 1;
            _cols = program.Variables.Count + _slackCount + _excessCount + _artificialCount + 1;
            _matrix = new double[_rows, _cols];
            IterationCount = 0;
            InitializeTableau();
        }

        private void InitializeTableau()
        {
            // Objective function
            for (int j = 0; j < _program.Variables.Count; j++)
                _matrix[0, j] = _program.IsMaximization ? -_program.Variables[j].Coefficient : _program.Variables[j].Coefficient;

            _matrix[0, _cols - 1] = 0;

            int slackIndex = 0;
            int excessIndex = 0;
            int artificialIndex = 0;

            for (int i = 1; i <= _program.Constraints.Count; i++)
            {
                var c = _program.Constraints[i - 1];
                bool isGreaterOrEqual = c.Relation == LinearProgram.Relation.GreaterThanOrEqual;
                bool isEquality = c.Relation == LinearProgram.Relation.Equal;

                // Decision variables
                for (int j = 0; j < _program.Variables.Count; j++)
                    _matrix[i, j] = (isGreaterOrEqual || isEquality) ? -c.Coefficients[j] : c.Coefficients[j];

                // Slack / Excess / Artificial variables
                if (isGreaterOrEqual)
                {
                    _matrix[i, _program.Variables.Count + slackIndex + excessIndex] = 1;
                    excessIndex++;
                }
                else if (c.Relation == LinearProgram.Relation.LessThanOrEqual)
                {
                    _matrix[i, _program.Variables.Count + slackIndex] = 1;
                    slackIndex++;
                }
                else if (isEquality)
                {
                    _matrix[i, _program.Variables.Count + _slackCount + _excessCount + artificialIndex] = 1;
                    artificialIndex++;
                }

                // RHS
                _matrix[i, _cols - 1] = (isGreaterOrEqual || isEquality) ? -c.Rhs : c.Rhs;
            }
        }

        public bool HasNegativeRHS()
        {
            for (int i = 1; i < _rows; i++)
                if (_matrix[i, _cols - 1] < -TOL)
                    return true;
            return false;
        }

        public int FindDualPivotRow()
        {
            int pivotRow = -1;
            double minRhs = 0;

            for (int i = 1; i < _rows; i++)
            {
                if (_matrix[i, _cols - 1] < minRhs)
                {
                    minRhs = _matrix[i, _cols - 1];
                    pivotRow = i;
                }
            }

            return pivotRow;
        }

        public int FindDualPivotColumn(int pivotRow)
        {
            int pivotCol = -1;
            double minRatio = double.MaxValue;

            for (int j = 0; j < _cols - 1; j++)
            {
                if (_matrix[pivotRow, j] < -TOL)
                {
                    double ratio = _matrix[0, j] / _matrix[pivotRow, j];
                    if (ratio < minRatio)
                    {
                        minRatio = ratio;
                        pivotCol = j;
                    }
                }
            }

            return pivotCol;
        }

        public bool IsOptimal()
        {
            for (int j = 0; j < _cols - 1; j++)
                if (_matrix[0, j] < -TOL)
                    return false;
            return true;
        }

        public int FindPivotColumn()
        {
            int pivotCol = -1;
            double minVal = 0;
            for (int j = 0; j < _cols - 1; j++)
            {
                if (_matrix[0, j] < minVal)
                {
                    minVal = _matrix[0, j];
                    pivotCol = j;
                }
            }
            return pivotCol;
        }

        public int FindPivotRow(int pivotCol)
        {
            int pivotRow = -1;
            double minRatio = double.MaxValue;

            for (int i = 1; i < _rows; i++)
            {
                if (_matrix[i, pivotCol] > TOL)
                {
                    double ratio = _matrix[i, _cols - 1] / _matrix[i, pivotCol];
                    if (ratio < minRatio && ratio >= -TOL)
                    {
                        minRatio = ratio;
                        pivotRow = i;
                    }
                }
            }

            return pivotRow;
        }

        public void Pivot(int pivotRow, int pivotCol)
        {
            IterationCount++;
            double pivotVal = _matrix[pivotRow, pivotCol];

            for (int j = 0; j < _cols; j++)
                _matrix[pivotRow, j] /= pivotVal;

            for (int i = 0; i < _rows; i++)
            {
                if (i == pivotRow) continue;
                double factor = _matrix[i, pivotCol];
                for (int j = 0; j < _cols; j++)
                    _matrix[i, j] -= factor * _matrix[pivotRow, j];
            }
        }

        public double GetObjectiveValue()
        {
            return _program.IsMaximization ? _matrix[0, _cols - 1] : -_matrix[0, _cols - 1];
        }

        public double GetValue(int row, int col) => _matrix[row, col];

        public Dictionary<string, double> GetSolution()
        {
            var solution = new Dictionary<string, double>();

            // Decision variables
            for (int j = 0; j < _program.Variables.Count; j++)
            {
                double value = 0.0;
                for (int i = 1; i < _rows; i++)
                {
                    if (Math.Abs(_matrix[i, j] - 1) < TOL)
                    {
                        bool isBasic = true;
                        for (int k = 1; k < _rows; k++)
                        {
                            if (k != i && Math.Abs(_matrix[k, j]) > TOL)
                            {
                                isBasic = false;
                                break;
                            }
                        }
                        if (isBasic)
                        {
                            value = _matrix[i, _cols - 1];
                            break;
                        }
                    }
                }
                solution[$"x{_program.Variables[j].Index}"] = value;
            }

            // Slack, excess, artificial variables
            int slackStart = _program.Variables.Count;
            int excessStart = slackStart + _slackCount;
            int artificialStart = excessStart + _excessCount;

            for (int j = slackStart; j < _cols - 1; j++)
            {
                double value = 0.0;
                for (int i = 1; i < _rows; i++)
                {
                    if (Math.Abs(_matrix[i, j] - 1) < TOL)
                    {
                        bool isBasic = true;
                        for (int k = 1; k < _rows; k++)
                        {
                            if (k != i && Math.Abs(_matrix[k, j]) > TOL)
                            {
                                isBasic = false;
                                break;
                            }
                        }
                        if (isBasic)
                        {
                            value = _matrix[i, _cols - 1];
                            break;
                        }
                    }
                }

                string varName;
                if (j < excessStart)
                    varName = $"s{j - slackStart + 1}";
                else if (j < artificialStart)
                    varName = $"e{j - excessStart + 1}";
                else
                    varName = $"a{j - artificialStart + 1}";

                solution[varName] = value;
            }

            return solution;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Tableau:");
            sb.Append("        ");

            for (int j = 0; j < _program.Variables.Count; j++)
                sb.Append($"x{_program.Variables[j].Index,-8}");
            for (int j = 0; j < _slackCount; j++)
                sb.Append($"s{j + 1,-8}");
            for (int j = 0; j < _excessCount; j++)
                sb.Append($"e{j + 1,-8}");
            for (int j = 0; j < _artificialCount; j++)
                sb.Append($"a{j + 1,-8}");

            sb.AppendLine("RHS");

            for (int i = 0; i < _rows; i++)
            {
                sb.Append(i == 0 ? "Obj:  " : $"Con{i}: ");
                for (int j = 0; j < _cols; j++)
                    sb.Append($"{_matrix[i, j],8:F3}");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}