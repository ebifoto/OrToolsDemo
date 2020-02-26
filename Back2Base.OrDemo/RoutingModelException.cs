using System;

namespace Back2Base.OrDemo
{
    public class RoutingModelException : Exception
    {
        public RoutingModelException()
        {
        }

        public RoutingModelException(string message)
            : base(message)
        {
        }

        public RoutingModelException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
