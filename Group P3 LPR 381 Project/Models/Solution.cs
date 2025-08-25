using System.Collections.Generic;
using System.Linq;

namespace LinearProgrammingSolver.Models
{
    public class Solution
    {
        public double OptimalValue { get; set; }
        public Dictionary<string, double> VariableValues { get; set; } = new Dictionary<string, double>();
        public List<string> Steps { get; } = new List<string>();
        public List<string> Messages { get; } = new List<string>();
        public double[,] FinalTableau { get; set; } // New property to store the final tableau matrix
        public int VariableCount { get; set; } // To track number of decision variables
        public int SlackCount { get; set; } // To track number of slack variables
        public int ExcessCount { get; set; } // To track number of excess variables
        public int ArtificialCount { get; set; } // To track number of artificial variables

        public Solution()
        {
            Steps = new List<string>();
            Messages = new List<string>();
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

        public override string ToString()
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
        }
    }
}
