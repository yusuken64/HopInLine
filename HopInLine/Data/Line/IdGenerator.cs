namespace HopInLine.Data.Line
{
    public class IdGenerator
    {
        private static Random random = new Random();
        private const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        public static string GenerateUniqueId()
        {
            string newId = new string(Enumerable.Repeat(chars, 4)
                    .Select(s => s[random.Next(s.Length)]).ToArray());
            return newId;
        }
    }
}