namespace Test
{
    internal class Program
    {
        private static Random random = new();

        private static IEnumerable<int> GetNumbers()
        {
            while (true)
            {
                yield return random.Next(20);
            }
        }

        private static void Main(string[] args)
        {
            Parallel.ForEach(GetNumbers(), number =>
            {
                Console.Write(number);
                Console.Write(" ");
            });
        }
    }
}