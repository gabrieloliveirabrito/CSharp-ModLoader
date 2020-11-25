using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModLoader.Data
{
    public class DebugConfig : List<string>
    {
        public bool All
        {
            get => Contains("All");
        }

        public bool Cache
        {
            get => All || Contains("Cache");
        }

        public bool Inject
        {
            get => All || Contains("Inject");
        }

        public bool Timer
        {
            get => All || Contains("Timer");
        }

        public bool Compilation
        {
            get => All || Contains("Compilation");
        }

        public bool Init
        {
            get => All || Contains("Init");
        }

        public bool Mod
        {
            get => All || Contains("Mod");
        }
    }
}
