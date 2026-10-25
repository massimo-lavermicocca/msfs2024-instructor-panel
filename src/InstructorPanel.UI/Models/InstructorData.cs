using Newtonsoft.Json;

namespace InstructorPanel.UI.Models
{
    public class InstructorData
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public uint Altitude { get; set; }
        public int Heading { get; set; }
        public uint Speed { get; set; }

        public InstructorData()
        {
#if DEBUG
            try
            {
                var _temp = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText("appsettings.Development.Custom.json"));
                Longitude = (double)_temp["Longitude"];
                Latitude = (double)_temp["Latitude"];
                Altitude = (uint)(long)_temp["Altitude"];
                Heading = (int)(long)_temp["Heading"];
                Speed = (uint)(long)_temp["Speed"];
            }
            catch { }
#endif
        }
    }
}
