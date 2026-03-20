using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace HASS.Agent.Core
{
    public class SensorsManager
    {
        private readonly string _file;

        public SensorsManager(string file)
        {
            _file = file;
        }

        public Task<List<SensorModel>> GetSensorsAsync()
        {
            var list = SensorsLoader.Load(_file);
            return Task.FromResult(list);
        }
    }
}
