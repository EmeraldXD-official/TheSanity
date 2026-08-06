// ==========================================================
// Tambahin method ini di dalam class TheSanity : Mod (file utama mod kamu)
// ==========================================================
using Terraria.ModLoader;
using TheSanity.Globals;

namespace TheSanity
{
    public partial class TheSanity : Mod
    {
        // Mod lain bisa manggil ini walau gak reference project TheSanity sama sekali:
        //
        //   ModLoader.GetMod("TheSanity")?.Call("AddFlaskEffect", flaskBuffType, npcDebuffType, durationTicks);
        //
        // Contoh nyata dari mod lain yang bikin "Flask of Shadow" custom:
        //
        //   Mod theSanity = ModLoader.GetMod("TheSanity");
        //   if (theSanity != null)
        //   {
        //       theSanity.Call("AddFlaskEffect",
        //           ModContent.BuffType<FlaskOfShadowBuff>(), // buff flask mereka
        //           ModContent.BuffType<ShadowDebuff>(),      // debuff yang mau kena ke NPC
        //           300);                                     // durasi (tick)
        //   }
        //
        public override object Call(params object[] args)
        {
            if (args.Length > 0 && args[0] is string command)
            {
                switch (command)
                {
                    case "AddFlaskEffect":
                        // args[1] = flaskBuffType (int)
                        // args[2] = npcDebuffType (int)
                        // args[3] = durationTicks (int)
                        if (args.Length >= 4 && args[1] is int flaskBuff && args[2] is int npcDebuff && args[3] is int duration)
                        {
                            FlaskProjectileGlobal.AddFlaskEffect(flaskBuff, npcDebuff, duration);
                            return true;
                        }
                        break;
                }
            }

            return null;
        }
    }
}
