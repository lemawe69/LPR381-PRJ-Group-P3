using System.Text;

namespace Group_P3_LPR_381_Project.Models
{
    public class Constraint
    {
        public double[] Coefficients { get; }
        public string Relation { get; }
        public double RHS { get; }

        public Constraint(double[] coeffs, string rel, double rhs)
        {
            Coefficients = coeffs;
            Relation = rel;
            RHS = rhs;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Coefficients.Length; i++)
                sb.Append($"{Coefficients[i]}x{i + 1} ");
            sb.Append($"{Relation} {RHS}");
            return sb.ToString();
        }
    }
}
