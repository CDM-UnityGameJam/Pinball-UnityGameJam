namespace Pinball.Core
{
    // The lucky number changes on a timer and is never the same twice in a row.
    // Port of the old: current = (current - 1 + Random.Range(1, 6)) % 6 + 1.
    public static class LuckyNumberRule
    {
        public static int First(int min, int max, IRandom random) => random.Range(min, max + 1);

        public static int Next(int current, int min, int max, IRandom random)
        {
            int count = max - min + 1;
            if (count <= 1) return min;
            return (current - min + random.Range(1, count)) % count + min;
        }
    }
}
