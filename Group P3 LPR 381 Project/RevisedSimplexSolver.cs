using Group_P3_LPR_381_Project.Models;
using Group_P3_LPR_381_Project.Utils;
using System;
using System.Collections.Generic;

namespace Group_P3_LPR_381_Project.Solvers
{
    public class RevisedSimplexSolver
    {
        public List<string> IterationLogs { get; private set; } = new();
        public double[] Solution { get; private set; }

        public void Solve(LPModel model)
        {
            var A = MatrixUtils.BuildConstraintMatrix(model);
            var b = model.Constraints.Select(c => c.RHS).ToArray();
            var c = model.ObjectiveCoefficients;

            var basis = MatrixUtils.InitializeBasis(A);
            var iteration = 0;

            while (true)
            {
                var B = MatrixUtils.ExtractBasisMatrix(A, basis);
                var B_inv = MatrixUtils.InvertMatrix(B);
                var xB = MatrixUtils.Multiply(B_inv, b);
                var cb = basis.Select(i => c[i]).ToArray();
                var y = MatrixUtils.Multiply(cb, B_inv);
                var reducedCosts = MatrixUtils.ComputeReducedCosts(c, A, y);

                IterationLogs.Add($"Iteration {++iteration}: Reduced Costs = {string.Join(", ", reducedCosts.Select(r => Math.Round(r, 3)))}");

                int entering = MatrixUtils.SelectEnteringVariable(reducedCosts);
                if (entering == -1) break;

                var direction = MatrixUtils.Multiply(B_inv, MatrixUtils.GetColumn(A, entering));
                int leaving = MatrixUtils.SelectLeavingVariable(xB, direction);
                if (leaving == -1) throw new Exception("Unbounded solution.");

                basis[leaving] = entering;
            }

            Solution = MatrixUtils.BuildFullSolution(basis, A, b);
        }
    }
}
