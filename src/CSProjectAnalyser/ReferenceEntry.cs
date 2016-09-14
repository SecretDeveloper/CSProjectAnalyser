namespace CSProjectAnalyser
{
    public class ReferenceEntry
    {
        public string Include { get; set; }
        public string Name { get; set; }
        public string HintPath { get; set; }
        public bool VersionSpecified { get; set; }
        public string Version { get; set; }
    }
}