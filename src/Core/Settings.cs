using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace ElecKit.Core
{
    [DataContract]
    public sealed class Settings
    {
        [DataMember] public string ReportFolder { get; set; }
        [DataMember] public bool DoNotOpenReports { get; set; }
        [DataMember] public double FilletRadiusMm { get; set; }
        [DataMember] public double? ConduitOffsetMm { get; set; }
        [DataMember] public string LastConduitType { get; set; }
        [DataMember] public string LastLineStyle { get; set; }
        [DataMember] public string LastWireType { get; set; }
        [DataMember] public string LastCircuitParam { get; set; }
        [DataMember] public double? NearbyMm { get; set; }

        /// <summary>Fillet radius in mm; 150 mm until the user sets one.</summary>
        public double EffectiveFilletRadiusMm
        {
            get { return FilletRadiusMm > 0 ? FilletRadiusMm : 150.0; }
        }

        /// <summary>Conduit height above the level in mm; 2700 mm until the user sets one.</summary>
        public double EffectiveConduitOffsetMm
        {
            get { return ConduitOffsetMm.HasValue ? ConduitOffsetMm.Value : 2700.0; }
        }

        /// <summary>Search distance for Wire Info to Comments in mm; 500 mm until set.</summary>
        public double EffectiveNearbyMm
        {
            get { return NearbyMm.HasValue && NearbyMm.Value > 0 ? NearbyMm.Value : 500.0; }
        }

        private static Settings _current;

        public static Settings Current
        {
            get { return _current ?? (_current = Load()); }
        }

        public static string FilePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ElecKit", "settings.json");
            }
        }

        public string ResolveReportFolder()
        {
            if (!string.IsNullOrWhiteSpace(ReportFolder) && Directory.Exists(ReportFolder))
                return ReportFolder;
            return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        }

        private static Settings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    using (var stream = File.OpenRead(FilePath))
                        return (Settings)new DataContractJsonSerializer(typeof(Settings)).ReadObject(stream);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return new Settings();
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            using (var stream = File.Create(FilePath))
                new DataContractJsonSerializer(typeof(Settings)).WriteObject(stream, this);
        }
    }
}
