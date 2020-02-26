using System;

namespace Back2Base.OrDemo
{
    public static class Extensions
    {
        public static long T(this ValueTuple<int, int> tuple)
        {
            return tuple.Item1 * 60 + tuple.Item2;
        }

        public static string T(this long minutes)
        {
            var t = new TimeSpan(0, (int)minutes, 0);
            return t.ToString("hh\\:mm");
        }
    }
}
