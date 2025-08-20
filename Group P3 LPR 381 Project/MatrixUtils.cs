using Group_P3_LPR_381_Project.Models;
using System.Linq;

namespace Group_P3_LPR_381_Project.Utils
{
    public static class MatrixUtils
    {
        public static double[,] BuildInitialTableau(LPModel model)
        {
            int m = model.Constraints.Count;
            int n = model.ObjectiveCoefficients.Length;
            double[,] tableau = new double[m + 1, n + m + 1];

            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < n; j++)
                    tableau[i, j] = model.Constraints[i].Coefficients[j];

                tableau[i, n + i] = 1; // Slack variable
                tableau[i, n + m] = model.Constraints[i].RHS;
            }

            for (int j = 0; j < n; j++)
                tableau[m, j] = -model.ObjectiveCoefficients[j];

            return tableau;
        }

        public static int FindPivotColumn(double[,] tableau)
        {
            int lastRow = tableau.GetLength(0) - 1;
            int cols = tableau.GetLength(1) - 1;
            int pivotCol = -1;
            double min = 0;

            for (int j = 0; j < cols; j++)
            {
                if (tableau[lastRow, j] < min)
                {
                    min = tableau[lastRow, j];
                    pivotCol = j;
                }
            }

            return pivotCol;
        }

        public static int FindPivotRow(double[,] tableau, int pivotCol)
        {
            int rows = tableau.GetLength(0) - 1;
            int pivotRow = -1;
            double minRatio = double.MaxValue;

            for (int i = 0; i < rows; i++)
            {
                double entry = tableau[i, pivotCol];
                double rhs = tableau[i, tableau.GetLength(1) - 1];

                if (entry > 0)
                {
                    double ratio = rhs / entry;
                    if (ratio < minRatio)
                    {
                        minRatio = ratio;
                        pivotRow = i;
                    }
                }
            }

            return pivotRow;
        }

        public static double[,] Pivot(double[,] tableau, int pivotRow, int pivotCol)
        {
            int rows = tableau.GetLength(0);
            int cols = tableau.GetLength(1);
            double[,] newTableau = new double[rows, cols];
            double pivot = tableau[pivotRow, pivotCol];

            for (int j = 0; j < cols; j++)
                newTableau[pivotRow, j] = tableau[pivotRow, j] / pivot;

            for (int i = 0; i < rows; i++)
            {
                if (i == pivotRow) continue;
                for (int j = 0; j < cols; j++)
                    newTableau[i, j] = tableau[i, j] - tableau[i, pivotCol] * newTableau[pivotRow, j];
            }

            return newTableau;
        }

        public static double[] ExtractSolution(double[,] tableau, int varCount)
        {
            int rows = tableau.GetLength(0) - 1;
            int cols = tableau.GetLength(1) - 1;
            double[] solution = new double[varCount];

            for (int j = 0; j < varCount; j++)
            {
                int pivotRow = -1;
                for (int i = 0; i < rows; i++)
                {
                    if (tableau[i, j] == 1)
                    {
                        bool isBasic = true;
                        for (int k = 0; k < rows; k++)
                        {
                            if (k != i && tableau[k, j] != 0)
                            {
                                isBasic = false;
                                break;
                            }
                        }

                        if (isBasic)
                        {
                            pivotRow = i;
                            break;
                        }
                    }
                }

                if (pivotRow != -1)
                    solution[j] = tableau[pivotRow, cols];
            }

            return solution;
        }

        public static double[,] BuildConstraintMatrix(LPModel model)
        {
            int m = model.Constraints.Count;
            int n = model.ObjectiveCoefficients.Length;
            double[,] A = new double[m, n];

            for (int i = 0; i < m; i++)
                for (int j = 0; j < n; j++)
                    A[i, j] = model.Constraints[i].Coefficients[j];

            return A;
        }

        public static int[] InitializeBasis(double[,] A)
        {
            int m = A.GetLength(0);
            return Enumerable.Range(A.GetLength(1), m).ToArray();
        }

        public static double[,] ExtractBasisMatrix(double[,] A, int[] basis)
        {
            int m = A.GetLength(0);
            double[,] B = new double[m, m];

            for (int i = 0; i < m; i++)
                for (int j = 0; j < m; j++)
                    B[i, j] = A[i, basis[j]];

            return B;
        }

        public static double[,] InvertMatrix(double[,] matrix)
        {
            int n = matrix.GetLength(0);
            var result = new double[n, n];
            var identity = new double[n, n];

            for (int i = 0; i < n; i++)
                identity[i, i] = 1;

            for (int i = 0; i < n; i++)
            {
                var col = Multiply(matrix, GetColumn(identity, i));
                for (int j = 0; j < n; j++)
                    result[j, i] = col[j];
            }

            return result;
        }

        public static double[] Multiply(double[] vector, double[,] matrix)
        {
            int cols = matrix.GetLength(1);
            double[] result = new double[cols];

            for (int j = 0; j < cols; j++)
                for (int i = 0; i < vector.Length; i++)
                    result[j] += vector[i] * matrix[i, j];

            return result;
        }

        public static double[] Multiply(double[,] matrix, double[] vector)
        {
            int rows = matrix.GetLength(0);
            double[] result = new double[rows];

            for (int i = 0; i < rows; i++)
                for (int j = 0; j < vector.Length; j++)
                    result[i] += matrix[i, j] * vector[j];

            return result;
        }

        public static double[] ComputeReducedCosts(double[] c, double[,] A, double[] y)
        {
            int n = c.Length;
            double[] rc = new double[n];

            for (int j = 0; j < n; j++)
            {
                double sum = 0;
                for (int i = 0; i < y.Length; i++)
                    sum += y[i] * A[i, j];
                rc[j] = c[j] - sum;
            }

            return rc;
        }

        public static int SelectEnteringVariable(double[] reducedCosts)
        {
            int index = -1;
            double min = 0;

            for (int i = 0; i < reducedCosts.Length; i++)
            {
                if (reducedCosts[i] < min)
                {
                    min = reducedCosts[i];
                    index = i;
                }
            }

            return index;
        }

        public static double[] GetColumn(double[,] matrix, int colIndex)
        {
            int rows = matrix.GetLength(0);
            double[] column = new double[rows];

            for (int i = 0; i < rows; i++)
                column[i] = matrix[i, colIndex];

            return column;
        }

        public static int SelectLeavingVariable(double[] xB, double[] direction)
        {
            int index = -1;
            double minRatio = double.MaxValue;

            for (int i = 0; i < xB.Length; i++)
            {
                if (direction[i] > 0)
                {
                    double ratio = xB[i] / direction[i];
                    if (ratio < minRatio)
                    {
                        minRatio = ratio;
                        index = i;
                    }
                }
            }

            return index;
        }

        public static double[] BuildFullSolution(int[] basis, double[,] A, double[] b)
        {
            int n = A.GetLength(1);
            double[] solution = new double[n];
            var B = ExtractBasisMatrix(A, basis);
            var B_inv = InvertMatrix(B);
            var xB = Multiply(B_inv, b);

            for (int i = 0; i < basis.Length; i++)
                solution[basis[i]] = xB[i];

            return solution;
        }
    }
}
