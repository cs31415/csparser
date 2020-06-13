using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace csParser
{
    /// <summary>
    /// Parse csharp files in given path and extract sql commands invoked
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            // get command line args
            if (args.Length < 1 || args.Length > 2)
            {
                Console.WriteLine("Usage: csparser [code-path] [filter]");
                return;
            }

            var path = args[0];
            Console.WriteLine($"Code path = {path}");
            if (!Directory.Exists(path))
            {
                Console.WriteLine($"Directory {path} does not exist. Please retry.");
                return;
            }

            var sw = Stopwatch.StartNew();
            sw.Start();

            var filter = args.Length > 1 ? args[1] : "*.cs";
            var runDir = $@"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}";
            var argIdxMapFile = $@"{runDir}\argMap.txt";

            var argMap = LoadArgMap(argIdxMapFile);

            var resultFileName = $@"{runDir}\storedprocs.csv";

            // Recursively iterate & parse files from input path
            var files = Directory.GetFiles(path, filter, SearchOption.AllDirectories);
            Console.WriteLine("1st pass");
            var results = new List<OutputRecord>();
            MethodCallsPass(1, files, argMap, results);

            var allFiles = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories);

            // Look up variables that are method arguments
            Console.WriteLine("2nd pass");
            var referencedMethods = results
                .Where(r => r.CommandText.StartsWith("methodCalls:"))
                .Select(r => r.CommandText.Replace("methodCalls:","").Replace(":",","))
                .ToArray();
            if (referencedMethods.Length > 0)
            {
                var refArgMap = LoadArgMap(referencedMethods);
                MethodCallsPass(2, allFiles, refArgMap, results);
            }
            else
            {
                Console.WriteLine("No referenced methods.");
            }

            // Look up variables if any
            Console.WriteLine("3rd pass to lookup vars"); 
            DeclarationsPass(3, results, allFiles);

            // write out results to file
            WriteResults(results, resultFileName);

            sw.Stop();
            Console.WriteLine($"Analysis complete. Elapsed time = {sw.Elapsed.TotalSeconds}s");
        }

        private static MultiMap<string, ArgMapRecord> LoadArgMap(string argIdxMapFile)
        {
            var argIdxRecords = File.ReadAllLines(argIdxMapFile);

            return LoadArgMap(argIdxRecords);
        }

        private static MultiMap<string, ArgMapRecord> LoadArgMap(string[] argIdxRecords)
        {
            var argMap = new MultiMap<string, ArgMapRecord>();
            foreach (var argIdxRecord in argIdxRecords)
            {
                if (string.IsNullOrWhiteSpace(argIdxRecord) || argIdxRecord.Trim().StartsWith("//"))
                {
                    continue;
                }

                var parts = argIdxRecord.Split(',');
                var methodExpr = parts[0];
                int argIdx = int.Parse(parts[1]);

                var methodParts = methodExpr.Split('.');
                var methodName = methodParts[methodParts.Length - 1];
                var classExpr = methodParts.Length > 1 ? methodParts[0] : string.Empty;
                var methods = new List<string>();
                if (classExpr != "")
                {
                    bool referencedMethods = classExpr.StartsWith(ParseHelper.ReferencedMethodPrefix);

                    var classParts = classExpr.Replace(ParseHelper.ReferencedMethodPrefix,"").Split('|');
                    foreach(var classPart in classParts)
                    {
                        var prefix = referencedMethods ? ParseHelper.ReferencedMethodPrefix : "";
                        methods.Add($"{prefix}{classPart}.{methodName}");
                    }
                }
                else
                {
                    methods.Add(methodName);
                }

                foreach (var method in methods)
                {
                    var argMapRecord = new ArgMapRecord();
                    argMapRecord.ArgIdx = argIdx;
                    if (parts.Length > 2)
                    {
                        var nameSpace = parts[2];
                        argMapRecord.NameSpace = nameSpace;
                    }

                    argMapRecord.MethodName = method;
                    argMap.Add(method, argMapRecord);
                }
            }

            return argMap;
        }

        private static void MethodCallsPass(int pass, string[] files, MultiMap<string, ArgMapRecord> argMap, List<OutputRecord> results)
        {
            foreach (var file in files)
            {
                if (file.Contains(@"\obj\") || file.Contains(@"\bin\"))
                    continue;
                Console.WriteLine($"pass {pass}: {file}");
                var fileResults = ParseHelper.ProcessFile(file, argMap);
                if (fileResults != null)
                {
                    
                    results.AddRange(fileResults);
                }
            }
        }

        private static void DeclarationsPass(int pass, IList<OutputRecord> results, string[] files)
        {
            var variableRecords = results.Where(r => r.IsVariable).ToList();
            var nVariables = variableRecords.Count();
            if (variableRecords.Any())
            {
                foreach (var file in files)
                {
                    if (nVariables <= 0)
                    {
                        break;
                    }

                    if (file.Contains(@"\obj\") || file.Contains(@"\bin\"))
                        continue;
                    Console.WriteLine($"pass {pass}: {file}");
                    var vals = ParseHelper.GetVariables(file);

                    foreach (var variableRecord in variableRecords)
                    {
                        if (vals.ContainsKey(variableRecord.CommandText))
                        {
                            nVariables--;
                            variableRecord.CommandText = vals[variableRecord.CommandText];
                            variableRecord.IsVariable = false;
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("No variables to lookup.");
            }
        }

        private static void WriteResults(IList<OutputRecord> results, string resultFileName)
        {
            var resultBuilder = new ResultBuilder();
            foreach (var result in results.Distinct())
            {
                if (!result.CommandText.Contains(ParseHelper.ReferencedMethodPrefix))
                    resultBuilder.AppendResults(result);
            }

            Console.WriteLine($"Writing results to {resultFileName}");
            File.WriteAllText(resultFileName, resultBuilder.ResultsText);
        }

    }
}
