using LinearProgrammingSolver.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static LinearProgrammingSolver.Models.LinearProgram;

namespace LinearProgrammingSolver.Algorithms
{
    public class BranchAndBound
    {
        // Solve the problem using either primal or dual simplex class
        // Primal if all the constraints are <=
        // Dual for a mix of <=, >= and = constraints

        // Check to see if any basic decision variables are decimal
        // If all are integers, then give an appropriate message, but then no branch and bound can take place

        // If there are more than one basic decision variable that is a decimal do the following check:
        // The decision variable closest to 0.5 gets branched. If all are the same distance, choose the lowest/smallest value

        // The chosen variable gets branched into 2 sub-problems: sub-problem 1 ( floor) and sub-problem 2 (celing)
        // - for example: x1 = 1.25 so sub-problem 1: x1 <= 1 and sub-problem 2: x1 >= 2

        // Display heading= Sub-problem 1: x1 <= 1
        // Add the newly formed constraint to the OPTIMAL table. Rememeber if the sign is <= it gets an s variable and >= gets an e variable

        // As you can see, the basic varibale is now non-basic. Ensure that it goes basic again and newly added variable is positive

        // Now solve using the dual simplex class ( if there are negatives in the RHS column)
        // CHECK: Are any basic decision variables are decimal? 
        // Yes? Check is problem is infeasible or unbound, if not, Branch to sub-problem 1.1 and 1.2
        // No? Table is optimal!

        // The code should give live updates, show all the sub-problems, when a problem is optimal, infeasible etc. and also not go over constraint 30 - preventing infinite looping
    }
}
