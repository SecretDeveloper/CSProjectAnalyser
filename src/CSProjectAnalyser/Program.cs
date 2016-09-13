using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace CSProjectAnalyser
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0 || args.Length > 2)
            {
                Console.Out.WriteLine("ProjParser <source folder> <assemblyname>");
                return;
            }
            if (Directory.Exists(args[0]) == false)
            {
                Console.WriteLine("Invalid directory {0}", args[0]);
                return;
            }


            var filePaths = FindFilesByExtension(args[0]).ToList(); // grab all csproj files
            var dictCSProjFiles = InitCSProjDictionary(filePaths);


            // Gather all referenced items from all cs project files
            Dictionary<string, ReferenceEntry> allReferenceEntries = new Dictionary<string, ReferenceEntry>();
            foreach (var project in dictCSProjFiles.Values)
            {
                foreach (var referenceEntry in GetReferencesEntriesFromCSProj(project.File))
                {
                    if (allReferenceEntries.ContainsKey(referenceEntry.Name) == false)
                        allReferenceEntries.Add(referenceEntry.Name, referenceEntry);

                    project.Uses.Add(referenceEntry);
                }
            }

            // update usedby
            foreach (var project in dictCSProjFiles.Values)
            {
                foreach (var usedReferences in project.Uses)
                {
                    if (dictCSProjFiles.ContainsKey(usedReferences.Name))
                    {
                        dictCSProjFiles[usedReferences.Name].UsedBy.Add(project);
                    }
                }
            }




            Dictionary<string, ProjectReferenceMap> items = dictCSProjFiles;
            if (args.Length > 1)
            {
                items = new Dictionary<string, ProjectReferenceMap>();
                items.Add(args[1], dictCSProjFiles[args[1]]);
            }

            foreach (var projectReferenceMap in items.Values)
            {
                PrintReferenceMap(projectReferenceMap);
            }

            if (args.Length < 2)
            {
                Console.WriteLine("\n\n--------------------SUMMARY----------------------");
                Console.Out.WriteLine("Not referenced (Top level?)\n");
                var topLevel = from project in dictCSProjFiles.Values
                    where project.UsedBy.Count == 0
                    orderby project.Name
                    select project;

                foreach (var project in topLevel)
                {
                    Console.Out.WriteLine("    {0} - {1}",project.Name, project.File.Replace(args[0],""));
                }


                Console.Out.WriteLine("---------------------------\r\n\r\nMost referenced ()");
                var most = from project in dictCSProjFiles.Values
                    where project.UsedBy.Count > 0
                    orderby project.UsedBy.Count descending
                    select project;

                foreach (var project in most)
                {
                    Console.Out.WriteLine("    {0} - {1}",project.Name, project.UsedBy.Count);
                }
            }

        }

        private static void PrintReferenceMap(ProjectReferenceMap projectReferenceMap)
        {
            Console.Out.WriteLine("-------------------{0}--------------", projectReferenceMap.Name);
            Console.Out.WriteLine("References (" + projectReferenceMap.Uses.Count + "):");
            foreach (var usage in projectReferenceMap.Uses.OrderBy(t => t.Name))
            {
                var s = "  " + usage.Name;
                if (usage.VersionSpecified) s += "\n    "+"    VersionSpecific";
                if (!string.IsNullOrEmpty(usage.Version)) s += "\n    " + "    Version:" + usage.Version;
                if (usage.HintPath.Length > 0) s += "\n    " + "    HintPath:" + usage.HintPath;

                Console.Out.WriteLine("  {0}", s);
            }

            Console.Out.WriteLine("Referenced by (" + projectReferenceMap.UsedBy.Count + "):");
            foreach (var usage in projectReferenceMap.UsedBy.OrderBy(t => t.Name))
            {
                Console.Out.WriteLine("  " + usage.Name);
            }
            Console.WriteLine();
        }

        private static Dictionary<string, ProjectReferenceMap> InitCSProjDictionary(IEnumerable<string> filePaths)
        {
            Dictionary<string, ProjectReferenceMap> csProjDictionary = new Dictionary<string, ProjectReferenceMap>();
            foreach (var filePath in filePaths)
            {
                var fi = new FileInfo(filePath);
                var name = fi.Name;
                var assembly = GetAssemblyNameFromCSProj(filePath);

                if (string.IsNullOrEmpty(assembly)) continue;

                var project = new ProjectReferenceMap
                {
                    Assembly = assembly,
                    File = filePath,
                    Name = name,
                    UsedBy = new List<ProjectReferenceMap>(),
                    Uses = new List<ReferenceEntry>()
                };

                if (!csProjDictionary.ContainsKey(assembly))
                {
                    csProjDictionary.Add(assembly, project);
                }
            }
            return csProjDictionary;
        }

        private static IEnumerable<string> FindFilesByExtension(string path, string extension = "*.csproj", bool recursive = true)
        {
            //Collect ProjectReferenceMap Files and build list
            foreach (string file in Directory.EnumerateFiles(path, extension, SearchOption.AllDirectories))
            {
                if (file.Contains("_old")) continue;

                yield return file;
            }
        }

        private static string GetAssemblyNameFromCSProj(string file)
        {
            try
            {
                XNamespace msbuild = "http://schemas.microsoft.com/developer/msbuild/2003";
                XDocument projDefinition = XDocument.Load(file);
                //            string[] references = projDefinition
                //                .Element(msbuild + "ProjectReferenceMap")
                //                .Elements(msbuild + "ItemGroup")
                //                .Elements(msbuild + "Reference")
                //                .Elements(msbuild + "HintPath")
                //                .Select(refElem => refElem.Value).ToArray();
                //
                //            Array.Sort(references);
                //            foreach (string reference in references)
                //            {
                //                Console.WriteLine(reference);
                //            }


                return (
                    projDefinition.Element(msbuild + "Project")
                        .Element(msbuild + "PropertyGroup")
                        .Element(msbuild + "AssemblyName")
                        .Value);
            }
            catch (Exception e)
            {
                Console.WriteLine(file + " caused error " + e.ToString());
                return string.Empty;
            }
        }

        private static List<ReferenceEntry> GetReferencesEntriesFromCSProj(string file)
        {
            XNamespace msbuild = "http://schemas.microsoft.com/developer/msbuild/2003";
            XDocument projDefinition = XDocument.Load(file);
            List<ReferenceEntry> references = projDefinition
                .Element(msbuild + "Project")
                .Elements(msbuild + "ItemGroup")
                .Elements(msbuild + "Reference")
                //.Elements(msbuild + "HintPath")
                .Select(refElem => ParseReferenceEntry(refElem)).ToList();
            
            return references;
        }

        private static ReferenceEntry ParseReferenceEntry(XElement refElem)
        {
            XNamespace msbuild = "http://schemas.microsoft.com/developer/msbuild/2003";
            /*
             <Reference Include="LTCExistingCoverageImport, Version=1.0.0.0, Culture=neutral, processorArchitecture=MSIL">
              <VersionSpecified>False</VersionSpecified>
              <HintPath>..\..\Assemblies\BuildOutput\LTCExistingCoverageImport.dll</HintPath>
            </Reference>
             */
            var r = new ReferenceEntry();
            r.HintPath = refElem.Element(msbuild + "HintPath") == null ? "" : refElem.Element(msbuild + "HintPath").Value;

            var specVal = refElem.Element(msbuild + "VersionSpecified") == null ? "False" : refElem.Element(msbuild + "VersionSpecified").Value;
            r.VersionSpecified = specVal.ToLower().Equals("true");
            r.Include = refElem.Attribute("Include") == null ? "" : refElem.Attribute("Include").Value;

            r.Name = r.Include;
            //Include path includes some additional junk - strip 
            if (r.Include.IndexOf(',') > 0)
            {
                r.Name = r.Include.Split(',')[0];
                r.Version = r.Include.Split(',')[1].Split('=')[1];   // McHack happy meal
            }

            return r;
        }
    }

    internal class ReferenceEntry
    {
        public string Include { get; set; }
        public string Name { get; set; }
        public string HintPath { get; set; }
        public bool VersionSpecified { get; set; }
        public string Version { get; set; }
    }
}
