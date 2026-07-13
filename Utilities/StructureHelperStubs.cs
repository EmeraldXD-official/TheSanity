using Terraria.ModLoader;
using Terraria.DataStructures;

namespace StructureHelper.API
{
    // Minimal stub to satisfy compile-time references to StructureHelper's Generator.
    public static class Generator
    {
        public static void GenerateStructure(string path, Point16 position, Mod mod)
        {
            // Fallback: do nothing. The real StructureHelper would read a structure file
            // and place tiles. Keeping this empty allows compilation where the external
            // library isn't present. Users who rely on structure generation should add
            // the StructureHelper mod or replace this with a proper implementation.
        }
    }
}
