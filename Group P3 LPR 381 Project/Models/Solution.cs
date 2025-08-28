using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LinearProgrammingSolver.Models
{
    public class Solution
    {
        public double OptimalValue { get; set; }
        public Dictionary<string, double> VariableValues { get; set; } = new Dictionary<string, double>();
        public List<string> Steps { get; } = new List<string>();
        public List<string> Messages { get; } = new List<string>();
        public List<double[,]> IterationTableaux { get; set; } = new List<double[,]>();
        public List<string> IterationMessages { get; set; } = new List<string>();
        public double[,] FinalTableau { get; set; } // New property to store the final tableau matrix
        public int VariableCount { get; set; } // To track number of decision variables
        public int SlackCount { get; set; } // To track number of slack variables
        public int ExcessCount { get; set; } // To track number of excess variables
        public int ArtificialCount { get; set; } // To track number of artificial variables

        public Solution()
        {
           VariableValues = new Dictionary<string, double>(); // Initialize to avoid null
           Steps = new List<string>();
           Messages = new List<string>();
           IterationTableaux = new List<double[,]>();
           IterationMessages = new List<string>();
           VariableValues = null;
           OptimalValue = 0;
           FinalTableau = null;
           VariableCount = 0;
           SlackCount = 0;
           ExcessCount = 0;
           ArtificialCount = 0;
        }

        public void AddStep(string title, string content)
        {
            Steps.Add(string.Concat(title, "\n", content));

        }

        public void AddMessage(string message)
        {
            Messages.Add(message);
        }

        public void AddIteration(double[,] tableau, string message = null)
        {
            IterationTableaux.Add((double[,])tableau.Clone());
            if (!string.IsNullOrEmpty(message))
                IterationMessages.Add(message);
            else
                IterationMessages.Add($"Iteration {IterationTableaux.Count}");
        }

        /*public override string ToString()
        {
            var result = string.Join("\n\n", Steps) + "\n\n";
            result += string.Join("\n", Messages) + "\n\n";
            result += "Optimal Solution:\n";

            foreach (var kvp in VariableValues)
            {
                result += string.Format("{0} = {1:F3}\n", kvp.Key, kvp.Value);
            }

            result += string.Format("\nOptimal Value: {0:F3}", OptimalValue);
            return result;
        }*/

        public override string ToString()
        {
            var result = new StringBuilder();
            if (Steps.Any())
            {
                result.AppendLine(string.Join("\n\n", Steps));
            }
            if (Messages.Any())
            {
                result.AppendLine("\n" + string.Join("\n", Messages));
            }

            if (IterationTableaux.Any())
            {
                result.AppendLine("\nTableau Iterations:");
                for (int i = 0; i < IterationTableaux.Count; i++)
                {
                    result.AppendLine($"--- {IterationMessages[i]} ---");
                    result.AppendLine(FormatTableau(IterationTableaux[i]));
                }
            }

            result.AppendLine("\nOptimal Solution:");
            if (VariableValues != null && VariableValues.Any())
            {
                foreach (var kvp in VariableValues.OrderBy(kvp => kvp.Key))
                {
                    result.AppendLine($"{kvp.Key} = {kvp.Value:F3}");
                }
                result.AppendLine($"\nOptimal Value: {OptimalValue:F3}");
            }
            else
            {
                result.AppendLine("No solution found.");
            }
            return result.ToString();
        }

        private string FormatTableau(double[,] tableau)
        {
            var sb = new StringBuilder();
            int rows = tableau.GetLength(0);
            int cols = tableau.GetLength(1);

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    sb.Append($"{tableau[i, j]:F3}\t");
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
