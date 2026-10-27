using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace ElecKit.Core
{
    [DataContract]
    public class CableDefinition
    {
        [DataMember] public string Name { get; set; }
        [DataMember] public int Conductors { get; set; }
        [DataMember] public double CrossSectionMm2 { get; set; }
        [DataMember] public double OuterDiameterMm { get; set; }
        [DataMember] public double AmpacityA { get; set; }   // 0 = not set; Voltage Drop only checks it when filled in
        [DataMember] public string Notes { get; set; }
    }

    public static class CableCatalogStore
    {
        public static string FilePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ElecKit", "cables.json");
            }
        }

        public static List<CableDefinition> Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    using (var stream = File.OpenRead(FilePath))
                    {
                        var list = (List<CableDefinition>)new DataContractJsonSerializer(typeof(List<CableDefinition>)).ReadObject(stream);
                        if (list != null && list.Count > 0) return list;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return Defaults();
        }

        public static void Save(IEnumerable<CableDefinition> items)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            using (var stream = File.Create(FilePath))
                new DataContractJsonSerializer(typeof(List<CableDefinition>)).WriteObject(stream, new List<CableDefinition>(items));
        }

        /// <summary>Starter rows only. Outer diameters are rough sample values: replace them with manufacturer data.</summary>
        public static List<CableDefinition> Defaults()
        {
            const string note = "Sample value - check manufacturer data";
            var list = new List<CableDefinition>();
            double[] sizes = { 1.5, 2.5, 4, 6, 10, 16 };
            double[] diameters = { 3.0, 3.6, 4.2, 4.8, 6.0, 7.2 };
            for (int i = 0; i < sizes.Length; i++)
            {
                list.Add(new CableDefinition
                {
                    Name = "1C x " + UnitConv.Format(sizes[i]) + " mm2",
                    Conductors = 1,
                    CrossSectionMm2 = sizes[i],
                    OuterDiameterMm = diameters[i],
                    Notes = note
                });
            }
            return list;
        }
    }
}
