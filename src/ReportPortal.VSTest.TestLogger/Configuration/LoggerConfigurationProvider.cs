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
                var key = parameter.Key.ToLowerInvariant().Replace(".", ConfigurationPath.KeyDelimeter);
                var value = parameter.Value;
                if (key == ConfigurationPath.LaunchTags.ToLowerInvariant())
                    value = parameter.Value.Replace(",", ";");
                Properties[key] = value;
            }

            return Properties;
        }
    }
}
