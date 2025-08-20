using System.Collections.Generic;

namespace LinearProgrammingSolver.Models
{
    public class Solution
    {
        public double OptimalValue { get; set; }
        public Dictionary<string, double> VariableValues { get; set; } = new Dictionary<string, double>();
        public List<string> Steps { get; } = new List<string>();
        public List<string> Messages { get; } = new List<string>();

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
