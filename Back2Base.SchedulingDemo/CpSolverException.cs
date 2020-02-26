using System;

namespace Back2Base.SchedulingDemo
{
    public class CpSolverException : Exception
    {
        public CpSolverException()
        {
        }

        public CpSolverException(string message)
            : base(message)
        {
        }

        public CpSolverException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
