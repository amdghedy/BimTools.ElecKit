using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace ElecKit.Core
{
    [DataContract]
    public class SelectionSet
    {
        [DataMember] public string Project { get; set; }
        [DataMember] public string Name { get; set; }
        [DataMember] public List<string> UniqueIds { get; set; }
    }

    /// <summary>Saved selections, kept in %APPDATA%\ElecKit\selection-sets.json (matched to the project by its title).</summary>
    public static class SelectionSetStore
    {
        public static string FilePath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ElecKit", "selection-sets.json");
            }
        }

        public static List<SelectionSet> Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    using (var stream = File.OpenRead(FilePath))
                    {
                        var list = (List<SelectionSet>)new DataContractJsonSerializer(typeof(List<SelectionSet>)).ReadObject(stream);
                        if (list != null) return list;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return new List<SelectionSet>();
        }

        public static void Save(List<SelectionSet> sets)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            using (var stream = File.Create(FilePath))
                new DataContractJsonSerializer(typeof(List<SelectionSet>)).WriteObject(stream, sets);
        }
    }
}
