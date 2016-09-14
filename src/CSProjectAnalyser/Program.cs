using System;
using System.IO;
using CliParse;

namespace CSProjectAnalyser
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var analyserParams = new AnalyserParams();
                var result = Parser.Parse(analyserParams, args);
                if (!result.Successful || result.ShowHelp)
                {
                    Console.WriteLine(analyserParams.GetHelpInfo());
                    return;
                }
                
                if (Directory.Exists(analyserParams.Path) == false)
                {
                    Console.WriteLine("Invalid directory {0}", analyserParams.Path);
                    return;
                }

                var output = (new AnalysisController()).Process(analyserParams);
                Console.WriteLine(output);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
