using MinooWebCore.Data.Models;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MWSMan.Core.Persistence.StaticConfiguration
{
    public interface IMinooOperationConfiguration
    {
        Dictionary<string, object> ToDictionary();
        T Get<T>(string key);
        void Load(string? filePath = null, string? fileName = "AppConfig.json");
        IMinooOperationConfiguration LoadOperationsConfig(string? filePath = null, string fileName = "OperationsConfig.json");
        public List<ServerInfo> GetServerInfo(
           string serverName = null,
           string databaseName = null,
           double? vahedeTejariSN = null);
        public List<ServerInfo> GetAllSerevr();

        MethodInfoConfig GetMethods(string MethodName);
        IList<MethodInfoConfig> GetAllMethods();
        List<Tuple<VHostModel, UserRabbitModel>> GetVhostAndUserModel(string AppName);

    }



    public class MinooOperationConfiguration : IMinooOperationConfiguration
    {
        private Dictionary<string, object> _config;
        private readonly object _lock = new();
        private string _filePath;
        private FileSystemWatcher _watcher;
        private DateTime _lastReloadTime;
        private readonly TimeSpan _reloadDelay = TimeSpan.FromSeconds(5);
        private string _fileName;
        public MinooOperationConfiguration(string? path = null)
        {
            Load(path);
        }

        public void Load(string? filePath = null, string fileName = "AppConfig.json")
        {
            lock (_lock)
            {
                _filePath = string.IsNullOrEmpty(filePath)
                    ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName)
                    : filePath;
                _fileName = fileName;

                if (!File.Exists(_filePath))
                    throw new FileNotFoundException($"{_filePath} file not found.", _filePath);

                LoadInternal();
                StartWatcher();
            }
        }
        public IMinooOperationConfiguration LoadOperationsConfig(string? filePath = null, string fileName = "OperationsConfig.json")
        {


            MinooOperationConfiguration newconfig = new MinooOperationConfiguration();

            lock (_lock)
            {
                newconfig._filePath = string.IsNullOrEmpty(filePath)
                    ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName)
                    : filePath;
                newconfig._fileName = fileName;

                if (!File.Exists(_filePath))
                    throw new FileNotFoundException($"{_filePath} file not found.", _filePath);

                newconfig.LoadInternal();
                newconfig.StartWatcher();
                return newconfig;
            }
        }

        public T Get<T>(string key)
        {
            lock (_lock)
            {
                if (_config == null)
                    throw new InvalidOperationException("Configuration not loaded.");

                var parts = key.Split(':', StringSplitOptions.RemoveEmptyEntries);
                object current = _config;

                foreach (var part in parts)
                {
                    if (current is Dictionary<string, object> dict)
                    {
                        if (!dict.TryGetValue(part, out current))
                            throw new KeyNotFoundException($"Key '{part}' not found.");
                    }
                    else if (current is JObject jobj)
                    {
                        if (!jobj.TryGetValue(part, StringComparison.OrdinalIgnoreCase, out var token))
                            throw new KeyNotFoundException($"Key '{part}' not found.");
                        current = token;
                    }
                    else
                    {
                        break;
                    }
                }

                if (current is JToken jt)
                    return jt.ToObject<T>();

                if (current is T value)
                    return value;

                if (current is IConvertible)
                    return (T)Convert.ChangeType(current, typeof(T));

                throw new InvalidCastException($"Cannot convert value for key '{key}' to {typeof(T).Name}.");
            }
        }

        public Dictionary<string, object> ToDictionary()
        {
            lock (_lock)
            {
                return _config == null
                    ? throw new InvalidOperationException("Configuration not loaded.")
                    : new Dictionary<string, object>(_config);
            }
        }

        public List<ServerInfo> GetServerInfo(
            string serverName = null,
            string databaseName = null,
            double? vahedeTejariSN = null)
        {
            lock (_lock)
            {
                var result = new List<ServerInfo>();

                JArray servers = _config["ServersInfo"] as JArray;
                if (servers == null)
                    throw new Exception("ServersInfo not found or invalid.");

                foreach (JObject item in servers)
                {
                    bool match = false;

                    if ((!string.IsNullOrEmpty(serverName) && vahedeTejariSN != null)
                        && (string.Equals(item.Value<string>("ServerName"), serverName, StringComparison.OrdinalIgnoreCase) && string.Equals(item.Value<double?>("VahedeTejariSN"), vahedeTejariSN.Value)))
                    {
                        result.Clear();
                        result.Add(
                            new ServerInfo()
                            {
                                ServerName = item.Value<string>("ServerName"),
                                DatabaseName = item.Value<string>("databaseName"),
                                VahedeTejariSN = item.Value<decimal>("VahedeTejariSN")
                            });
                        break;
                    }

                    if (!string.IsNullOrEmpty(serverName) &&
                        string.Equals(item.Value<string>("ServerName"), serverName, StringComparison.OrdinalIgnoreCase))
                        match = true;

                    if (!string.IsNullOrEmpty(databaseName) &&
                        string.Equals(item.Value<string>("databaseName"), databaseName, StringComparison.OrdinalIgnoreCase))
                        match = true;

                    if (vahedeTejariSN.HasValue &&
                        item.Value<double?>("VahedeTejariSN") == vahedeTejariSN.Value)
                        match = true;

                    if (match)
                    {
                        result.Add(
                            new ServerInfo()
                            {
                                ServerName = item.Value<string>("ServerName"),
                                DatabaseName = item.Value<string>("databaseName"),
                                VahedeTejariSN = item.Value<decimal>("VahedeTejariSN")
                            }
                        );
                    }
                }
                if (result.Count > 1)
                    throw new Exception("More than 1 server was found for this Info.");
                if (result.Count < 1)
                    throw new Exception("The server for the given information could not be found.");
                return result;
            }
        }

        //public List<ServerInfo> GetServerInfo(
        //    string serverName = null,
        //    string databaseName = null,
        //    decimal? vahedeTejariSN = null)
        //{
        //    lock (_lock)
        //    {
        //        if (_config == null)
        //            throw new InvalidOperationException("Configuration not loaded.");

        //        if (!_config.TryGetValue("ServersInfo", out var serversObj))
        //            throw new KeyNotFoundException("ServersInfo not found in AppConfig.json.");

        //        var serversArray = serversObj as JArray ?? JArray.FromObject(serversObj);
        //        var result = new List<ServerInfo>();

        //        foreach (var token in serversArray)
        //        {
        //            var item = (JObject)token;

        //            if (!IsMatch(item, serverName, databaseName, vahedeTejariSN))
        //                continue;

        //            result.Add(new ServerInfo
        //            {
        //                ServerName = item.Value<string>("ServerName"),
        //                DatabaseName = item.Value<string>("databaseName"),
        //                VahedeTejariSN = item.Value<decimal>("VahedeTejariSN")
        //            });
        //        }

        //        return result;
        //    }
        //}

        //private static bool IsMatch(
        //    JObject item,
        //    string serverName,
        //    string databaseName,
        //    decimal? vahedeTejariSN)
        //{

        //    if (!string.IsNullOrEmpty(serverName) &&
        //        !string.Equals(item.Value<string>("ServerName"), serverName, StringComparison.OrdinalIgnoreCase))
        //        return false;

        //    if (!string.IsNullOrEmpty(databaseName) &&
        //        !string.Equals(item.Value<string>("DatabaseName"), databaseName, StringComparison.OrdinalIgnoreCase))
        //        return false;

        //    if (vahedeTejariSN.HasValue &&
        //        item.Value<decimal>("VahedeTejariSN") != vahedeTejariSN.Value)
        //        return false;

        //    return true;
        //}

        private void LoadInternal()
        {
            var json = File.ReadAllText(_filePath);
            _config = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
        }

        private void StartWatcher()
        {
            if (_watcher != null)
                return;

            _watcher = new FileSystemWatcher
            {
                Path = Path.GetDirectoryName(_filePath),
                Filter = Path.GetFileName(_filePath),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
            };

            _watcher.Changed += (_, _) =>
            {
                if ((DockerStaticDateTime.NowDateTime - _lastReloadTime) < _reloadDelay)
                    return;

                lock (_lock)
                {
                    _lastReloadTime = DockerStaticDateTime.NowDateTime;
                    LoadInternal();
                }
            };

            _watcher.EnableRaisingEvents = true;
        }

        public MethodInfoConfig GetMethods(string MethodName)
        {
            MethodInfoConfig methodInfo = null;

            JArray MethodsInfos = _config["MethodsInfo"] as JArray;
            if (MethodsInfos == null || MethodsInfos.Count == 0)
                throw new Exception($"MethodsInfos not found or invalid In {_fileName}.");

            foreach (JObject item in MethodsInfos)
            {
                if (string.Equals(item.Value<string>("MethodName"), MethodName))
                {
                    string dllPath = Get<string>("DllsPath") ?? throw new Exception($"DllsPath not found or invalid In {_fileName}.");
                    string dllName = item.Value<string>("DllName") ?? throw new Exception($"DllName not found or invalid In {_fileName}.");
                    string className = item.Value<string>("ClassName") ?? throw new Exception($"ClassName not found or invalid In {_fileName}.");

                    //string _MethodName = item.Value<string>("MethodName") ?? throw new Exception($"MethodName not found or invalid In {_fileName}.");

                    JArray parametersArray = item.Value<JArray>("MethodParameters");
                    List<MethodParameterConfig> parameters = new List<MethodParameterConfig>();
                    foreach (JToken param in parametersArray)
                    {
                        parameters.Add(new MethodParameterConfig() { Name = param["Name"]?.Value<string?>() ?? throw new Exception("Parameter Name In OperationsConfig.json is Eempty"), Type = param["Type"]?.Value<string?>() ?? throw new Exception("Parameter Type In OperationsConfig.json is Eempty") });
                    }


                    //IList<MethodParameterConfig> parameters = item.Value<IList<MethodParameterConfig>>("MethodParameters") ?? throw new Exception($"MethodParameters not found or invalid In {_fileName}.");
                    return new MethodInfoConfig(dllName, className, MethodName, parameters.ToList(), dllPath);
                }
            }
            throw new Exception($"Method with name {MethodName} not found");

        }

        public List<ServerInfo> GetAllSerevr()
        {
            JArray servers = _config["ServersInfo"] as JArray;
            if (servers == null)
                throw new Exception("ServersInfo not found or invalid.");

            List<ServerInfo> result = new List<ServerInfo>();

            foreach (JObject item in servers)
            {
                result.Add(
                    new ServerInfo()
                    {
                        ServerName = item.Value<string>("ServerName"),
                        DatabaseName = item.Value<string>("databaseName"),
                        VahedeTejariSN = item.Value<decimal>("VahedeTejariSN")
                    });

            }
            if (result.Count() == 0) throw new Exception("سروری یافت نشد ");
            return result;
        }
        public List<Tuple<VHostModel, UserRabbitModel>> GetVhostAndUserModel(string AppName)
        {
            lock (_lock)
            {
                JArray VhostApps = _config["VHostApps"] as JArray;
                if (VhostApps == null || VhostApps.Count == 0)
                    throw new Exception("VhostApps not found or invalid.");

                List<Tuple<VHostModel, UserRabbitModel>> result = new List<Tuple<VHostModel, UserRabbitModel>>();

                foreach (JObject item in VhostApps)
                {
                    if (string.Equals(item.Value<string>("VhostAppName"), AppName, StringComparison.OrdinalIgnoreCase))
                        result.Add(new Tuple<VHostModel, UserRabbitModel>(
                            new VHostModel(item.Value<string>("VhostName")),
                            new UserRabbitModel(item.Value<string>("VhostUserName"), item.Value<string>("VhostPassword"))
                            ));
                }
                //if (result.Count <= 0)
                //    throw new Exception($"VhostApps Does Not Include {AppName}");
                return result;
            }
        }

        public IList<MethodInfoConfig> GetAllMethods()
        {
            IList<MethodInfoConfig> Methdos = new List<MethodInfoConfig>();

            JArray MethodsInfos = _config["MethodsInfo"] as JArray;
            if (MethodsInfos == null || MethodsInfos.Count == 0)
                throw new Exception($"MethodsInfos not found or invalid In {_fileName}.");

            foreach (JObject item in MethodsInfos)
            {
               
                    string dllPath = Get<string>("DllsPath") ?? throw new Exception($"DllsPath not found or invalid In {_fileName}.");
                    string dllName = item.Value<string>("DllName") ?? throw new Exception($"DllName not found or invalid In {_fileName}.");
                    string className = item.Value<string>("ClassName") ?? throw new Exception($"ClassName not found or invalid In {_fileName}.");
                string methodName = item.Value<string>("MethodName") ?? throw new Exception($"MethodName not foud or invalid In {_fileName}");

                    //string _MethodName = item.Value<string>("MethodName") ?? throw new Exception($"MethodName not found or invalid In {_fileName}.");

                    JArray parametersArray = item.Value<JArray>("MethodParameters");
                    List<MethodParameterConfig> parameters = new List<MethodParameterConfig>();
                    foreach (JToken param in parametersArray)
                    {
                        parameters.Add(new MethodParameterConfig() { Name = param["Name"]?.Value<string?>() ?? throw new Exception("Parameter Name In OperationsConfig.json is Eempty"), Type = param["Type"]?.Value<string?>() ?? throw new Exception("Parameter Type In OperationsConfig.json is Eempty") });
                    }


                    //IList<MethodParameterConfig> parameters = item.Value<IList<MethodParameterConfig>>("MethodParameters") ?? throw new Exception($"MethodParameters not found or invalid In {_fileName}.");
                    Methdos.Add(new MethodInfoConfig(dllName, className, methodName, parameters.ToList(), dllPath));
            }
            return Methdos;
        }
    }
}
