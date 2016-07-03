using System.Collections.Generic;

namespace SoundTouch.Utility
{
    public class RangeEnumerable
    {
        public static IEnumerable<int> Range(int fromInclusive, int toExclusive, int step)
        {
            for (var i = fromInclusive; i < toExclusive; i += step)
            {
                yield return i;
            }
        }
    }
}