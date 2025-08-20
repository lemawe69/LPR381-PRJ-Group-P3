using LinearProgrammingSolver.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace LinearProgrammingSolver.Algorithms
{
    public class PrimalSimplex : ISolver
    {
        public Solution Solve(LinearProgram program)
        {
            var solution = new Solution();
            var tableau = new Tableau(program);

            if (!program.IsMaximization)
            {
                solution.AddMessage("Converted minimization problem to maximization by negating objective");
            }

            solution.AddStep("Initial Tableau:", tableau.ToString());

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
                solution.AddStep($"Iteration {tableau.IterationCount}: Pivot on row {pivotRow}, column {pivotCol}", tableau.ToString());
            }

            solution.OptimalValue = tableau.GetObjectiveValue();
            solution.VariableValues = tableau.GetSolution();
            solution.AddStep("Final Tableau:", tableau.ToString());

            return solution;
        }
    }

    public class Tableau
    {
        private double[,] _matrix;
        private readonly int _rows;
        private readonly int _cols;
        private readonly LinearProgram _program;
        public int IterationCount { get; private set; }

        public Tableau(LinearProgram program)
        {
            _program = program ?? throw new ArgumentNullException(nameof(program));
            int slackCount = _program.Constraints.Count;
            _rows = slackCount + 1;
            _cols = _program.Variables.Count + slackCount + 1;
            _matrix = new double[_rows, _cols];
            IterationCount = 0;
            InitializeTableau();
        }

        private void InitializeTableau()
        {
            for (int j = 0; j < _program.Variables.Count; j++)
            {
                _matrix[0, j] = _program.IsMaximization
                    ? -_program.Variables[j].Coefficient
                    : _program.Variables[j].Coefficient;
            }

            for (int i = 1; i <= _program.Constraints.Count; i++)
            {
                var constraint = _program.Constraints[i - 1];

                for (int j = 0; j < _program.Variables.Count; j++)
                    _matrix[i, j] = constraint.Coefficients[j];

                for (int j = 0; j < _program.Constraints.Count; j++)
                    _matrix[i, _program.Variables.Count + j] = (i - 1 == j) ? 1 : 0;

                _matrix[i, _cols - 1] = constraint.Rhs;
            }
        }

        public bool IsOptimal()
        {
            for (int j = 0; j < _cols - 1; j++)
            {
                if (_matrix[0, j] < -1e-6)
                    return false;
            }
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
                if (_matrix[i, pivotCol] > 1e-6)
                {
                    double ratio = _matrix[i, _cols - 1] / _matrix[i, pivotCol];
                    if (ratio < minRatio)
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
            return _program.IsMaximization
                ? _matrix[0, _cols - 1]
                : -_matrix[0, _cols - 1];
        }

        public Dictionary<string, double> GetSolution()
        {
            var solution = new Dictionary<string, double>();
            for (int j = 0; j < _program.Variables.Count; j++)
            {
                bool isBasic = false;
                int basicRow = -1;

                for (int i = 1; i < _rows; i++)
                {
                    if (Math.Abs(_matrix[i, j] - 1) < 1e-6)
                    {
                        bool allZeros = true;
                        for (int k = 1; k < _rows; k++)
                        {
                            if (k != i && Math.Abs(_matrix[k, j]) > 1e-6)
                            {
                                allZeros = false;
                                break;
                            }
                        }

                        if (allZeros)
                        {
                            isBasic = true;
                            basicRow = i;
                            break;
                        }
                    }
                }

                solution[$"x{_program.Variables[j].Index}"] = isBasic
                    ? _matrix[basicRow, _cols - 1]
                    : 0;
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

            for (int j = 0; j < _cols - _program.Variables.Count - 1; j++)
                sb.Append($"s{j + 1,-8}");

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
