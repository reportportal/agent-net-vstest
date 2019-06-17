using ReportPortal.Shared.Configuration;
using System.Collections.Generic;

namespace ReportPortal.VSTest.TestLogger.Configuration
{
    class LoggerConfigurationProvider : IConfigurationProvider
    {
        private Dictionary<string, string> _parameters;

        public LoggerConfigurationProvider(Dictionary<string, string> parameters)
        {
            _parameters = parameters;
        }

        public IDictionary<string, string> Properties { get; } = new Dictionary<string, string>();

        public IDictionary<string, string> Load()
        {
            foreach (var parameter in _parameters)
            {
                if (parameter.Key.ToLowerInvariant() == "launch.name")
                {
                    Properties[ConfigurationPath.LaunchName] = parameter.Value;
                }
                else if (parameter.Key.ToLowerInvariant() == "launch.description")
                {
                    Properties[ConfigurationPath.LaunchDescription] = parameter.Value;
                }
                else if (parameter.Key.ToLowerInvariant() == "launch.tags")
                {
                    Properties[ConfigurationPath.LaunchTags] = parameter.Value.Replace(",", ";");
                }
                else if (parameter.Key.ToLowerInvariant() == "launch.isdebugmode")
                {
                    Properties[ConfigurationPath.LaunchDebugMode] = parameter.Value;
                }
                else if (parameter.Key.ToLowerInvariant() == "server.project")
                {
                    Properties[ConfigurationPath.ServerProject] = parameter.Value;
                }
                else if (parameter.Key.ToLowerInvariant() == "server.authentication.uuid")
                {
                    Properties[ConfigurationPath.ServerAuthenticationUuid] = parameter.Value;
                }
            }

            return Properties;
        }
    }
}
