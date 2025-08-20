using Group_P3_LPR_381_Project.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace Group_P3_LPR_381_Project.IO
{
    public static class OutputWriter
    {
        public static void Write(string path, LPModel model, List<double[,]> tableaus, double[] solution)
        {
            using var writer = new StreamWriter(path);
            writer.WriteLine("Canonical Form:");
            writer.WriteLine(model.ToCanonicalString());

            int count = 1;
            foreach (var tableau in tableaus)
            {
                writer.WriteLine($"\nTableau {count++}:");
                for (int i = 0; i < tableau.GetLength(0); i++)
                {
                    for (int j = 0; j < tableau.GetLength(1); j++)
                        writer.Write($"{Math.Round(tableau[i, j], 3)}\t");
                    writer.WriteLine();
                }
            }

            writer.WriteLine("\nOptimal Solution:");
            for (int i = 0; i < solution.Length; i++)
                writer.WriteLine($"x{i + 1} = {Math.Round(solution[i], 3)}");
        }

        public static void WriteRevised(string path, LPModel model, List<string> logs, double[] solution)
        {
            using var writer = new StreamWriter(path);
            writer.WriteLine("Canonical Form:");
            writer.WriteLine(model.ToCanonicalString());

            foreach (var log in logs)
                writer.WriteLine(log);

            writer.WriteLine("\nOptimal Solution:");
            for (int i = 0; i < solution.Length; i++)
                writer.WriteLine($"x{i + 1} = {Math.Round(solution[i], 3)}");
        }
    }
}
