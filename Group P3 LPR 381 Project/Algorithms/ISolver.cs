namespace LinearProgrammingSolver.Algorithms
{
    public interface ISolver
    {
        Models.Solution Solve(Models.LinearProgram program);
    }
}
