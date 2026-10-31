namespace MinooWebCore.Data.Models
{
    public class MethodInfoConfig
    {
        public MethodInfoConfig(string dllName, string className, string methodName, List<MethodParameterConfig> methodParameters, string dllsPath)
        {
            DllName = dllName;
            ClassName = className;
            MethodName = methodName;
            MethodParameters = methodParameters;
            DllsPath = dllsPath;
        }
        public string DllName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string MethodName { get; set; } = string.Empty;
        public List<MethodParameterConfig> MethodParameters { get; set; } = new();
        public string DllsPath { get; set; } = string.Empty;

    }
    public class MethodParameterConfig
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }
    //public class PluginConfig
    //{
    //    public List<MethodInfoConfig> MethodsInfo { get; set; } = new();
    //}
}
