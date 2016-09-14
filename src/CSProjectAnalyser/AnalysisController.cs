using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace CSProjectAnalyser
{
    internal class AnalysisController
    {
        private StringBuilder _sb = new StringBuilder();
        private Dictionary<string, ProjectReferenceMap> _dictCsProjFiles;
        private List<string> _recursedAssemblies = new List<string>();

        internal string Process(AnalyserParams analyserParams)
        {
            _sb.Clear();

            var filePaths = Enumerable.ToList<string>(FindFilesByExtension(analyserParams.Path)); // grab all csproj files
            _dictCsProjFiles = InitCSProjDictionary(filePaths);


            // Gather all referenced items from all cs project files
            Dictionary<string, ReferenceEntry> allReferenceEntries = new Dictionary<string, ReferenceEntry>();
            foreach (var project in _dictCsProjFiles.Values)
            {
                foreach (var referenceEntry in GetReferencesEntriesFromCSProj(project.File))
                {
                    if (!analyserParams.ShouldIncludeReference(referenceEntry.Name)) continue;  // skip items we want to ignore.

                    if (!allReferenceEntries.ContainsKey(referenceEntry.Name))
                    {
                        allReferenceEntries.Add(referenceEntry.Name, referenceEntry);
                    }
                    project.Uses.Add(referenceEntry);
                }
            }

            // update usedby
            foreach (var project in _dictCsProjFiles.Values)
            {
                foreach (var usedReferences in project.Uses)
                {
                    if (_dictCsProjFiles.ContainsKey(usedReferences.Name))
                    {
                        _dictCsProjFiles[usedReferences.Name].UsedBy.Add(project);
                    }
                }
            }


            Dictionary<string, ProjectReferenceMap> items = _dictCsProjFiles;
            if (analyserParams.AssemblyToAnalyse.Length > 1)
            {
                if (_dictCsProjFiles.ContainsKey(analyserParams.AssemblyToAnalyse) == false)
                    return
                        string.Format(
                            "The provided assembly value {0} was not found during scan, please check the assembly name is correct.");

                items = new Dictionary<string, ProjectReferenceMap>();
                items.Add(analyserParams.AssemblyToAnalyse, _dictCsProjFiles[analyserParams.AssemblyToAnalyse]);
            }

            foreach (var projectReferenceMap in items.Values)
            {
                PrintReferenceMap(projectReferenceMap, analyserParams);
            }

            if (analyserParams.Summary)
            {
                _sb.AppendLine("\n\n--------------------SUMMARY----------------------");

                _sb.AppendLine("Distinct assemblies found:"+_dictCsProjFiles.Keys.Count);

                _sb.AppendLine("Not referenced (Top level?)\n");
                var topLevel = from project in _dictCsProjFiles.Values
                    where project.UsedBy.Count == 0
                    orderby project.Name
                    select project;

                foreach (var project in topLevel)
                {
                    _sb.AppendLine(string.Format("    {0} - {1}", project.Name, project.File.Replace(analyserParams.Path, "")));
                }


                _sb.AppendLine("---------------------------\r\n\r\nTop 20 most referenced ()");
                var most = from project in _dictCsProjFiles.Values
                    where project.UsedBy.Count > 0
                    orderby project.UsedBy.Count descending
                    select project;

                foreach (var project in most.Take(20))
                {
                    _sb.AppendLine(string.Format("    {0} - {1}", project.Name, project.UsedBy.Count));
                }
            }
            return _sb.ToString();
        }

        private void PrintReferenceMap(ProjectReferenceMap projectReferenceMap, AnalyserParams analyserParams)
        {
            _sb.AppendLine(string.Format("-------------------{0}--------------", projectReferenceMap.Name));
            _sb.AppendLine("References (" + projectReferenceMap.Uses.Count + "):");
            foreach (var usage in projectReferenceMap.Uses.OrderBy(t => t.Name))
            {
                PrintReferenceEntry(usage, analyserParams, "-->", 0);
            }

            _sb.AppendLine("Referenced by (" + projectReferenceMap.UsedBy.Count + "):");
            foreach (var usage in projectReferenceMap.UsedBy.OrderBy(t => t.Name))
            {
                _sb.AppendLine("  " + usage.Name);
            }
            _sb.AppendLine();
        }

        private void PrintReferenceEntry(ReferenceEntry usage, AnalyserParams analyserParams, string indent, int currentDepth)
        {
            var s = usage.Name;
            if (analyserParams.Verbosity >= Verbosity.Medium)
            {
                if (usage.VersionSpecified) s += "\n    " + "    VersionSpecific";
                if (!string.IsNullOrEmpty(usage.Version)) s += "\n    " + "    Version:" + usage.Version;

                if(analyserParams.Verbosity == Verbosity.High)
                    if (usage.HintPath.Length > 0) s += "\n    " + "    HintPath:" + usage.HintPath;
            }
            _sb.AppendLine(string.Format("{0} {1}",indent, s));

            //recurse through entries
            if (analyserParams.RecurseDependencies && analyserParams.AssemblyToAnalyse.Length > 0 && _dictCsProjFiles.ContainsKey(usage.Name))
            {
                if (currentDepth > analyserParams.RecurseDependenciesMaxDepth)
                {
                    _sb.AppendLine(indent + "RECURSTION DEPTH EXCEEDED - potential circular references");
                    return;
                }

                //_sb.AppendLine(indent + "which references:");
                var project = _dictCsProjFiles[usage.Name];
                foreach (var dependency in project.Uses.OrderBy(t => t.Name))
                {
                    PrintReferenceEntry(dependency, analyserParams, indent + "-->", currentDepth+1);
                }
            }
        }

        private Dictionary<string, ProjectReferenceMap> InitCSProjDictionary(IEnumerable<string> filePaths)
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

        private IEnumerable<string> FindFilesByExtension(string path, string extension = "*.csproj", bool recursive = true)
        {
            //Collect ProjectReferenceMap Files and build list
            foreach (string file in Directory.EnumerateFiles(path, extension, SearchOption.AllDirectories))
            {
                if (file.Contains("_old")) continue;

                yield return file;
            }
        }

        private string GetAssemblyNameFromCSProj(string file)
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
                _sb.AppendLine(file + " caused error " + e.ToString());
                return string.Empty;
            }
        }

        private List<ReferenceEntry> GetReferencesEntriesFromCSProj(string file)
        {
            XNamespace msbuild = "http://schemas.microsoft.com/developer/msbuild/2003";
            XDocument projDefinition = XDocument.Load(file);
            List<ReferenceEntry> references = projDefinition
                .Element(msbuild + "Project")
                .Elements(msbuild + "ItemGroup")
                .Elements(msbuild + "Reference")
                //.Elements(msbuild + "HintPath")
                .Select<XElement, ReferenceEntry>(ParseReferenceEntry).ToList();
            
            return references;
        }

        private ReferenceEntry ParseReferenceEntry(XElement refElem)
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
}