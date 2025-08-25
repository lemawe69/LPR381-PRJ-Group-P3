using System;
using System.Collections.Generic;
using System.Linq;

namespace LinearProgrammingSolver.Models
{
    public class LinearProgram
    {
        public bool IsMaximization { get; set; }
        public List<Variable> Variables { get; set; }
        public List<Constraint> Constraints { get; set; }

        public LinearProgram()
        {
            Variables = new List<Variable>();
            Constraints = new List<Constraint>();
        }

        public static LinearProgram Parse(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Input cannot be null or empty");

            string[] lines = input.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2)
                throw new ArgumentException("Input must contain at least 2 lines (objective and sign restrictions)");

            LinearProgram program = new LinearProgram();

            try
            {
                ParseObjectiveFunction(lines[0], program);

                if (lines.Length > 2)
                    ParseConstraints(lines.Skip(1).Take(lines.Length - 2).ToArray(), program);

                ParseSignRestrictions(lines[lines.Length - 1], program);
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Error parsing input: " + ex.Message, ex);
            }

            return program;
        }

        private static void ParseObjectiveFunction(string line, LinearProgram program)
        {
            string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            program.IsMaximization = parts[0].Equals("max", StringComparison.OrdinalIgnoreCase);

            if ((parts.Length - 1) % 2 != 0)
                throw new ArgumentException("Objective coefficients must come in sign/value pairs");

            for (int i = 1; i < parts.Length; i += 2)
            {
                string sign = parts[i];
                if (sign != "+" && sign != "-")
                    throw new ArgumentException("Invalid sign '" + sign + "' in objective function");

                double coefficient;
                if (!double.TryParse(parts[i + 1], out coefficient))
                    throw new ArgumentException("Invalid coefficient value '" + parts[i + 1] + "'");

                program.Variables.Add(new Variable
                {
                    Coefficient = (sign == "+") ? coefficient : -coefficient,
                    Index = program.Variables.Count + 1,
                    Type = VariableType.Continuous
                });
            }
        }

        private static void ParseConstraints(string[] lines, LinearProgram program)
        {
            foreach (string line in lines)
            {
                string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < program.Variables.Count * 2 + 2)
                    throw new ArgumentException("Constraint line has insufficient values: " + line);

                Constraint constraint = new Constraint();

                for (int i = 0; i < program.Variables.Count; i++)
                {
                    string sign = parts[i * 2];
                    if (sign != "+" && sign != "-")
                        throw new ArgumentException("Invalid sign '" + sign + "' in constraint coefficients");

                    double coeff;
                    if (!double.TryParse(parts[i * 2 + 1], out coeff))
                        throw new ArgumentException("Invalid coefficient value '" + parts[i * 2 + 1] + "'");

                    constraint.Coefficients.Add((sign == "+") ? coeff : -coeff);
                }

                string relation = parts[program.Variables.Count * 2];
                if (relation == "<=")
                    constraint.Relation = Relation.LessThanOrEqual;
                else if (relation == ">=")
                    constraint.Relation = Relation.GreaterThanOrEqual;
                else if (relation == "=")
                    constraint.Relation = Relation.Equal;
                else
                    throw new ArgumentException("Invalid constraint operator '" + relation + "'");

                double rhs;
                if (!double.TryParse(parts[program.Variables.Count * 2 + 1], out rhs))
                    throw new ArgumentException("Invalid RHS value '" + parts[program.Variables.Count * 2 + 1] + "'");

                constraint.Rhs = rhs;
                program.Constraints.Add(constraint);
            }
        }

        private static void ParseSignRestrictions(string line, LinearProgram program)
        {
            string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != program.Variables.Count)
                throw new ArgumentException("Expected " + program.Variables.Count + " sign restrictions but got " + parts.Length);

            for (int i = 0; i < parts.Length; i++)
            {
                string restriction = parts[i].ToLower();
                switch (restriction)
                {
                    case "+": program.Variables[i].Type = VariableType.NonNegative; break;
                    case "-": program.Variables[i].Type = VariableType.NonPositive; break;
                    case "urs": program.Variables[i].Type = VariableType.Unrestricted; break;
                    case "int": program.Variables[i].Type = VariableType.Integer; break;
                    case "bin": program.Variables[i].Type = VariableType.Binary; break;
                    default: throw new ArgumentException("Unknown variable type '" + parts[i] + "'");
                }
            }
        }

        public override string ToString()
        {
            string result = (IsMaximization ? "Maximize" : "Minimize") + ":\n  ";
            for (int i = 0; i < Variables.Count; i++)
            {
                if (i > 0) result += " + ";
                result += (Variables[i].Coefficient >= 0 ? Variables[i].Coefficient.ToString("F3") : "(" + Variables[i].Coefficient.ToString("F3") + ")")
                    + "x" + Variables[i].Index;
            }

            result += "\n\nSubject to:\n";
            for (int i = 0; i < Constraints.Count; i++)
            {
                result += "  ";
                for (int j = 0; j < Variables.Count; j++)
                    result += (Constraints[i].Coefficients[j] >= 0 ? "+ " : "- ") + Math.Abs(Constraints[i].Coefficients[j]).ToString("F3") + "x" + Variables[j].Index + " ";

                string op = "? ";
                if (Constraints[i].Relation == Relation.LessThanOrEqual) op = "<= ";
                if (Constraints[i].Relation == Relation.GreaterThanOrEqual) op = ">= ";
                if (Constraints[i].Relation == Relation.Equal) op = "= ";

                result += op + Constraints[i].Rhs.ToString("F3") + "\n";
            }

            result += "\nWith:\n";
            for (int i = 0; i < Variables.Count; i++)
            {
                if (i > 0) result += ", ";
                result += "x" + Variables[i].Index + " " + GetVariableTypeString(Variables[i].Type);
            }

            return result;
        }

        private string GetVariableTypeString(VariableType type)
        {
            switch (type)
            {
                case VariableType.NonNegative: return "Non-negative";
                case VariableType.NonPositive: return "Non-positive";
                case VariableType.Unrestricted: return "Unrestricted";
                case VariableType.Integer: return "Integer";
                case VariableType.Binary: return "Binary";
                default: return "Continuous";
            }
        }

        public enum Relation { LessThanOrEqual, GreaterThanOrEqual, Equal }
        public enum VariableType { NonNegative, NonPositive, Unrestricted, Integer, Binary, Continuous }

        public class Variable
        {
            public int Index { get; set; }
            public double Coefficient { get; set; }
            public VariableType Type { get; set; }
            public double Value { get; set; }
        }

        public class Constraint
        {
            public List<double> Coefficients { get; set; }
            public Relation Relation { get; set; }
            public double Rhs { get; set; }
            public double Slack { get; set; }

            public Constraint()
            {
                Coefficients = new List<double>();
            }
        }
    }
}
