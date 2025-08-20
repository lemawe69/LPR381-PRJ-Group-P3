using LinearProgrammingSolver.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LinearProgrammingSolver.Algorithms
{
    public class RevisedPrimalSimplex : ISolver
    {
        public Solution Solve(LinearProgram program)
        {
            var solution = new Solution();
            var tableau = new RevisedTableau(program);

            solution.AddStep("Initial Basis:", tableau.GetCurrentBasisInfo());
            solution.AddStep("Initial Prices:", tableau.GetPriceVector());

            while (!tableau.IsOptimal())
            {
                int enteringVar = tableau.FindEnteringVariable();
                if (enteringVar == -1)
                {
                    solution.AddMessage("Problem is optimal or unbounded.");
                    break;
                }

                int leavingVar = tableau.FindLeavingVariable(enteringVar);
                if (leavingVar == -1)
                {
                    solution.AddMessage("Problem is unbounded.");
                    break;
                }

                tableau.UpdateBasis(enteringVar, leavingVar);
                solution.AddStep($"Iteration {tableau.IterationCount}:", tableau.GetCurrentIterationInfo());
            }

            solution.OptimalValue = tableau.GetObjectiveValue();
            solution.VariableValues = tableau.GetSolution();
            solution.AddMessage($"Optimal solution found with value: {solution.OptimalValue:F3}");

            return solution;
        }
    }

    public class RevisedTableau
    {
        private readonly LinearProgram _program;
        private List<int> _basisIndices;
        private double[,] _basisInverse;
        private double[] _solutionVector;
        public int IterationCount { get; private set; }

        public RevisedTableau(LinearProgram program)
        {
            _program = program;
            Initialize();
            IterationCount = 0;
        }

        private void Initialize()
        {
            int m = _program.Constraints.Count;
            int n = _program.Variables.Count;

            _basisIndices = Enumerable.Range(n, m).ToList();
            _basisInverse = new double[m, m];
            for (int i = 0; i < m; i++)
                _basisInverse[i, i] = 1.0;

            _solutionVector = _program.Constraints.Select(c => c.Rhs).ToArray();
        }

        public bool IsOptimal()
        {
            double[] cB = _basisIndices.Select(idx => idx < _program.Variables.Count ? _program.Variables[idx].Coefficient : 0).ToArray();

            for (int j = 0; j < _program.Variables.Count; j++)
            {
                if (_basisIndices.Contains(j)) continue;
                double[] a = _program.Constraints.Select(c => c.Coefficients[j]).ToArray();
                double reducedCost = _program.Variables[j].Coefficient - cB.Zip(MultiplyVector(_basisInverse, a), (cb, val) => cb * val).Sum();
                if (reducedCost > 1e-8) return false;
            }
            return true;
        }

        public int FindEnteringVariable()
        {
            double[] cB = _basisIndices.Select(idx => idx < _program.Variables.Count ? _program.Variables[idx].Coefficient : 0).ToArray();
            int enteringVar = -1;
            double maxReducedCost = 0;

            for (int j = 0; j < _program.Variables.Count; j++)
            {
                if (_basisIndices.Contains(j)) continue;
                double[] a = _program.Constraints.Select(c => c.Coefficients[j]).ToArray();
                double reducedCost = _program.Variables[j].Coefficient - cB.Zip(MultiplyVector(_basisInverse, a), (cb, val) => cb * val).Sum();
                if (reducedCost > maxReducedCost)
                {
                    maxReducedCost = reducedCost;
                    enteringVar = j;
                }
            }

            return enteringVar;
        }

        public int FindLeavingVariable(int enteringVar)
        {
            double[] enteringColumn = _program.Constraints.Select(c => c.Coefficients[enteringVar]).ToArray();
            double[] u = MultiplyVector(_basisInverse, enteringColumn);

            int leavingVarPos = -1;
            double minRatio = double.MaxValue;

            for (int i = 0; i < u.Length; i++)
            {
                if (u[i] > 1e-8)
                {
                    double ratio = _solutionVector[i] / u[i];
                    if (ratio < minRatio)
                    {
                        minRatio = ratio;
                        leavingVarPos = i;
                    }
                }
            }

            return leavingVarPos;
        }

        public void UpdateBasis(int enteringVar, int leavingVarPos)
        {
            IterationCount++;
            _basisIndices[leavingVarPos] = enteringVar;

            int m = _program.Constraints.Count;
            int n = _program.Variables.Count;
            double[,] B = new double[m, m];

            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < m; j++)
                {
                    int varIndex = _basisIndices[j];
                    B[i, j] = (varIndex < n) ? _program.Constraints[i].Coefficients[varIndex] : (i == j ? 1.0 : 0.0);
                }
            }

            _basisInverse = InvertMatrix(B);
            RecalculateSolutionVector();
        }

        private void RecalculateSolutionVector()
        {
            double[] b = _program.Constraints.Select(c => c.Rhs).ToArray();
            _solutionVector = MultiplyVector(_basisInverse, b);
        }

        public double GetObjectiveValue()
        {
            double value = 0;
            for (int i = 0; i < _basisIndices.Count; i++)
            {
                int varIndex = _basisIndices[i];
                if (varIndex < _program.Variables.Count)
                    value += _program.Variables[varIndex].Coefficient * _solutionVector[i];
            }
            return value;
        }

        public Dictionary<string, double> GetSolution()
        {
            var solution = new Dictionary<string, double>();
            for (int j = 0; j < _program.Variables.Count; j++)
            {
                int basisPos = _basisIndices.IndexOf(j);
                solution[$"x{_program.Variables[j].Index}"] = (basisPos != -1) ? _solutionVector[basisPos] : 0;
            }
            return solution;
        }

        public string GetCurrentBasisInfo()
        {
            string info = "Current Basis Variables:\n";
            for (int i = 0; i < _basisIndices.Count; i++)
            {
                int varIndex = _basisIndices[i];
                info += varIndex < _program.Variables.Count
                    ? $"  x{_program.Variables[varIndex].Index} = {_solutionVector[i]:F3}\n"
                    : $"  Slack {varIndex - _program.Variables.Count + 1} = {_solutionVector[i]:F3}\n";
            }
            return info;
        }

        public string GetPriceVector()
        {
            double[] prices = _basisIndices.Select(idx => idx < _program.Variables.Count ? _program.Variables[idx].Coefficient : 0).ToArray();
            return "Price Vector: [" + string.Join(", ", prices.Select(p => p.ToString("F3"))) + "]";
        }

        public string GetCurrentIterationInfo()
        {
            return GetCurrentBasisInfo() + GetPriceVector() + "\n" + $"Current Objective Value: {GetObjectiveValue():F3}\n";
        }

        private double[] MultiplyVector(double[,] matrix, double[] vector)
        {
            int m = matrix.GetLength(0);
            int n = matrix.GetLength(1);
            double[] result = new double[m];
            for (int i = 0; i < m; i++)
                for (int j = 0; j < n; j++)
                    result[i] += matrix[i, j] * vector[j];
            return result;
        }

        private double[,] InvertMatrix(double[,] matrix)
        {
            int n = matrix.GetLength(0);
            double[,] result = new double[n, n];
            double[,] tmp = (double[,])matrix.Clone();

            for (int i = 0; i < n; i++)
                result[i, i] = 1.0;

            for (int i = 0; i < n; i++)
            {
                double pivot = tmp[i, i];
                if (Math.Abs(pivot) < 1e-12) throw new Exception("Matrix is singular!");

                for (int j = 0; j < n; j++)
                {
                    tmp[i, j] /= pivot;
                    result[i, j] /= pivot;
                }

                for (int k = 0; k < n; k++)
                {
                    if (k == i) continue;
                    double factor = tmp[k, i];
                    for (int j = 0; j < n; j++)
                    {
                        tmp[k, j] -= factor * tmp[i, j];
                        result[k, j] -= factor * result[i, j];
                    }
                }
            }

            return result;
        }
    }
}
