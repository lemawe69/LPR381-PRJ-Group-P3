using Group_P3_LPR_381_Project.IO;
using Group_P3_LPR_381_Project.Models;
using Group_P3_LPR_381_Project.Solvers;
using System;

namespace Group_P3_LPR_381_Project.Menu
{
    public static class MenuHandler
    {
        public static void ShowMainMenu()
        {
            Console.WriteLine("=== Linear Programming Solver ===");
            Console.WriteLine("1. Solve with Primal Simplex");
            Console.WriteLine("2. Solve with Revised Simplex");
            Console.WriteLine("0. Exit");
            Console.Write("Choose option: ");
            var choice = Console.ReadLine();

            if (choice == "1" || choice == "2")
            {
                var model = InputParser.Parse("Resources/input.txt");
                if (model == null) return;

                if (choice == "1")
                {
                    var solver = new SimplexSolver();
                    solver.Solve(model);
                    OutputWriter.Write("Resources/output.txt", model, solver.Tableaus, solver.Solution);
                    ExcelExporter.ExportSimplexResults("Resources/output.xlsx", solver.Tableaus, solver.Solution);
                }
                else
                {
                    var solver = new RevisedSimplexSolver();
                    solver.Solve(model);
                    OutputWriter.WriteRevised("Resources/output.txt", model, solver.IterationLogs, solver.Solution);
                }

                Console.WriteLine("Solution exported.");
            }
        }
    }
}
