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
#if DEBUG
                args = "C:\\Projects\\EN\\PlaneBiz\\Development\\Release16Sep BaseEnrollment -s".Split(' ');
#endif

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

                using (var sw = new StreamWriter(Console.OpenStandardOutput()))
                {
                    sw.AutoFlush = true;
                    var outt = Console.Out;
                    Console.SetOut(sw);
                    (new AnalysisController()).Process(analyserParams, sw);
                    Console.SetOut(outt);
                    
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}
