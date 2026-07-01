using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework; 

namespace TheSanity
{
    public class ShieldInvincibilityController : global::Terraria.ModLoader.GlobalNPC
    {
        // Berjalan HANYA untuk Crystal ATAU semua NPC yang bersahabat (Town NPC, dll)
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) 
        {
            return entity.type == NPCID.DD2EterniaCrystal || entity.friendly;
        }

        public override void AI(NPC npc)
        {
            bool isProtected = false;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.ModProjectile is EterniaShieldHub shield && !shield.isCooldown)
                {
                    // - Jika ini Crystal, otomatis terlindungi (kebal) dari mana saja.
                    // - Jika ini Friendly NPC, cek apakah jarak mereka ada di DALAM radius tameng.
                    if (npc.type == NPCID.DD2EterniaCrystal || Vector2.Distance(p.Center, npc.Center) <= shield.targetOrbitRadius)
                    {
                        isProtected = true;
                        break; // Ketemu tameng aktif, berhenti mencari.
                    }
                }
            }

            // Terapkan status kebal jika memenuhi syarat di atas
            npc.dontTakeDamage = isProtected;
        }
    }
}