using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ModLoader.Data
{
    using Enums;

    public class ModData
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        
        [JsonConverter(typeof(StringEnumConverter))]
        public ModType Type { get; set; }

        public string Filename { get; set; }
        public string MainClass { get; set; }

        public override string ToString()
        {
            string about = "";
            about += $"Mod Data of {Name}" + Environment.NewLine;
            about += $"- ID: {ID}" + Environment.NewLine;
            about += $"- Description: {Description}" + Environment.NewLine;
            about += $"- Type: {Enum.GetName(typeof(ModType), Type)}" + Environment.NewLine;
            about += $"- Filename: {Filename}" + Environment.NewLine;
            about += $"- MainClass: {MainClass}" + Environment.NewLine;

            return about;
        }
    }
}
