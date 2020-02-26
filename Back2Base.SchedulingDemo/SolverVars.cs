using System.Collections.Generic;
using Google.OrTools.Sat;

namespace Back2Base.SchedulingDemo
{
    public class SolverVars
    {
        public IntVar[,,] Work { get; }

        public SolverVars(IntVar[,,] work)
        {
            Work = work;
        }

        // Linear terms of the objective in a minimization context.
        public List<IntVar> ObjIntVars { get; } = new List<IntVar>();
        public List<int> ObjIntCoeffs { get; } = new List<int>();
        public List<IntVar> ObjBoolVars { get; } = new List<IntVar>();
        public List<int> ObjBoolCoeffs { get; } = new List<int>();
        public CpSolverStatus Status { get; set; }
    }
}
