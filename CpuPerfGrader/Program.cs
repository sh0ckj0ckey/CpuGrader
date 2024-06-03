namespace CpuPerfGrader
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string cpuName = CpuPerfHelper.GetCpuName();
            CpuPerfHelper.GetCpuInfo(out uint coreCount, out uint threadCount, out uint frequency);
            CpuPerfHelper.GetCpuPerfGrade(cpuName, coreCount, threadCount, frequency);
            Console.WriteLine($"Your CPU is {cpuName}, has {coreCount} cores, {threadCount} threads, max frequency is {frequency}GHz.");

            var grade = CpuPerfHelper.GetCpuPerfGrade(cpuName, coreCount, threadCount, frequency);
            Console.WriteLine($"Your CPU grade seems to be {grade.ToString().ToUpper()}.");

            Console.ReadKey();
        }
    }
}
