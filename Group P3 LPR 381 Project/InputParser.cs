using Group_P3_LPR_381_Project.Models;
using System.Collections.Generic;

namespace Group_P3_LPR_381_Project.IO
{
    public static class InputParser
    {
        public static LPModel Parse(string path)
        {
            var lines = File.ReadAllLines(path);
            if (lines.Length < 3) return null;

            var objectiveParts = lines[0].Split(' ');
            var isMax = objectiveParts[0].ToLower() == "max";
            var coefficients = objectiveParts.Skip(1).Select(p => double.Parse(p.Replace("+", ""))).ToArray();

            var constraints = new List<Constraint>();
            for (int i = 1; i < lines.Length - 1; i++)
            {
                var parts = lines[i].Split(' ');
                var coeffs = parts.Take(coefficients.Length).Select(p => double.Parse(p.Replace("+", ""))).ToArray();
                var relation = parts[coefficients.Length];
                var rhs = double.Parse(parts[coefficients.Length + 1]);
                constraints.Add(new Constraint(coeffs, relation, rhs));
            }

            var restrictions = lines.Last().Split(' ');
            return new LPModel(isMax, coefficients, constraints, restrictions);
        }
    }
}
