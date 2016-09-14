using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CSProjectAnalyserTest
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            var parm = new CSProjectAnalyser.AnalyserParams();
            parm.Path = "C:\\Projects\\EN\\PlaneBiz\\Development\\Release16Sep";
            parm.AssemblyToAnalyse = "BaseEnrollment";
            parm.Summary = true;

            var controller = new CSProjectAnalyser.AnalysisController();

            using (var sw = new StreamWriter(new MemoryStream()))
            {
                controller.Process(parm, sw);
            }
        }
    }
}
