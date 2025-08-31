using Group_P3_LPR_381_Project.Models;
using Group_P3_LPR_381_Project.Utils;
using System;
using System.Collections.Generic;

namespace Group_P3_LPR_381_Project.Solvers
{
    public class SimplexSolver
    {
        public List<double[,]> Tableaus { get; private set; } = new();
        public double[] Solution { get; private set; }

        public void Solve(LPModel model)
        {
            var tableau = MatrixUtils.BuildInitialTableau(model);
            Tableaus.Add((double[,])tableau.Clone());

            while (true)
            {
                int pivotCol = MatrixUtils.FindPivotColumn(tableau);
                if (pivotCol == -1) break;

                int pivotRow = MatrixUtils.FindPivotRow(tableau, pivotCol);
                if (pivotRow == -1) throw new Exception("Unbounded solution.");

                tableau = MatrixUtils.Pivot(tableau, pivotRow, pivotCol);
                Tableaus.Add((double[,])tableau.Clone());
            }

            Solution = MatrixUtils.ExtractSolution(tableau, model.ObjectiveCoefficients.Length);
        }
    }
}
