using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.InteropServices;
using static System.Console;

namespace GaussDB.BasicTest
{
    internal class Program
    {
        static async Task Main(string[] args)
        {

            WriteLine("""
         42                                                    
         42              ,d                             ,d     
         42              42                             42     
 ,adPPYb,42  ,adPPYba, MM42MMM 8b,dPPYba,   ,adPPYba, MM42MMM  
a8"    `Y42 a8"     "8a  42    42P'   `"8a a8P_____42   42     
8b       42 8b       d8  42    42       42 8PP!!!!!!!   42     
"8a,   ,d42 "8a,   ,a8"  42,   42       42 "8b,   ,aa   42,    
 `"8bbdP"Y8  `"YbbdP"'   "Y428 42       42  `"Ybbd8"'   "Y428  

""");

            const double Mebi = 1024 * 1024;
            const double Gibi = Mebi * 1024;
            GCMemoryInfo gcInfo = GC.GetGCMemoryInfo();
            long totalMemoryBytes = gcInfo.TotalAvailableMemoryBytes;

            // OS and .NET information
            WriteLine($"{nameof(RuntimeInformation.OSArchitecture)}: {RuntimeInformation.OSArchitecture}");
            WriteLine($"{nameof(RuntimeInformation.OSDescription)}: {RuntimeInformation.OSDescription}");
            WriteLine($"{nameof(RuntimeInformation.FrameworkDescription)}: {RuntimeInformation.FrameworkDescription}");
            WriteLine();

            // Environment information
            WriteLine($"{nameof(Environment.UserName)}: {Environment.UserName}");
            WriteLine($"HostName : {Dns.GetHostName()}");
            WriteLine();

            // Hardware information
            WriteLine($"{nameof(Environment.ProcessorCount)}: {Environment.ProcessorCount}");
            WriteLine($"{nameof(GCMemoryInfo.TotalAvailableMemoryBytes)}: {totalMemoryBytes} ({GetInBestUnit(totalMemoryBytes)})");

            string[] memoryLimitPaths = new string[]
            {
    "/sys/fs/cgroup/memory.max",
    "/sys/fs/cgroup/memory.high",
    "/sys/fs/cgroup/memory.low",
    "/sys/fs/cgroup/memory/memory.limit_in_bytes",
            };

            string[] currentMemoryPaths = new string[]
            {
    "/sys/fs/cgroup/memory.current",
    "/sys/fs/cgroup/memory/memory.usage_in_bytes",
            };

            // cgroup information
            if (OperatingSystem.IsLinux() &&
                GetBestValue(memoryLimitPaths, out long memoryLimit, out string? bestMemoryLimitPath) &&
                memoryLimit > 0)
            {
                // get memory cgroup information
                GetBestValue(currentMemoryPaths, out long currentMemory, out string? memoryPath);

                WriteLine($"cgroup memory constraint: {bestMemoryLimitPath}");
                WriteLine($"cgroup memory limit: {memoryLimit} ({GetInBestUnit(memoryLimit)})");
                WriteLine($"cgroup memory usage: {currentMemory} ({GetInBestUnit(currentMemory)})");
                WriteLine($"GC Hard limit %: {(double)totalMemoryBytes / memoryLimit * 100:N0}");
            }

            string GetInBestUnit(long size)
            {
                if (size < Mebi)
                {
                    return $"{size} bytes";
                }
                else if (size < Gibi)
                {
                    double mebibytes = size / Mebi;
                    return $"{mebibytes:F} MiB";
                }
                else
                {
                    double gibibytes = size / Gibi;
                    return $"{gibibytes:F} GiB";
                }
            }

            bool GetBestValue(string[] paths, out long limit, [NotNullWhen(true)] out string? bestPath)
            {
                foreach (string path in paths)
                {
                    if (Path.Exists(path) &&
                        long.TryParse(File.ReadAllText(path), out limit))
                    {
                        bestPath = path;
                        return true;
                    }
                }

                bestPath = null;
                limit = 0;
                return false;
            }

            var connString = "host=localhost;port=5432;username=gaussdb;password=EFCore@123123;database=test";

            var dataSourceBuilder = new GaussDBDataSourceBuilder(connString);
            var dataSource = dataSourceBuilder.Build();

            var conn = await dataSource.OpenConnectionAsync();

            // Insert some data
            await using (var cmd = new GaussDBCommand("INSERT INTO test.test (column1) VALUES (@p)", conn))
            {
                cmd.Parameters.AddWithValue("p", "Hello world");
                await cmd.ExecuteNonQueryAsync();
            }

            // Retrieve all rows
            await using (var cmd = new GaussDBCommand("SELECT (column1) FROM test.test", conn))
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    Console.WriteLine(reader.GetString(0));
            }

            await using (var cmdversion = new GaussDBCommand("SELECT version();", conn))
            await using (var reader = await cmdversion.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    Console.WriteLine(reader.GetString(0));
            }
        }
    }
}
