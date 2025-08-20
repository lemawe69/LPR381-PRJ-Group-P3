using System.Collections.Generic;
using System.Text;

namespace Group_P3_LPR_381_Project.Models
{
    public class LPModel
    {
        public bool IsMax { get; }
        public double[] ObjectiveCoefficients { get; }
        public List<Constraint> Constraints { get; }
        public string[] SignRestrictions { get; }

        public LPModel(bool isMax, double[] obj, List<Constraint> cons, string[] signs)
        {
            IsMax = isMax;
            ObjectiveCoefficients = obj;
            Constraints = cons;
            SignRestrictions = signs;
        }

        public string ToCanonicalString()
        {
            var sb = new StringBuilder();
            sb.Append(IsMax ? "Maximize: " : "Minimize: ");
            for (int i = 0; i < ObjectiveCoefficients.Length; i++)
                sb.Append($"{ObjectiveCoefficients[i]}x{i + 1} ");
            sb.AppendLine("\nSubject to:");
            foreach (var c in Constraints)
                sb.AppendLine(c.ToString());
            return sb.ToString();
        }
    }
}
