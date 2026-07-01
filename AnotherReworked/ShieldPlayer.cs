using Terraria;
using Terraria.ModLoader;

namespace TheSanity
{
    public class ShieldPlayer : ModPlayer
    {
        public bool IsProtectedByShield = false;

        public override void ResetEffects() {
            IsProtectedByShield = false;
        }

        // Blokir damage dari NPC (kontak fisik)
        public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers) {
            if (IsProtectedByShield) {
                modifiers.FinalDamage.Base = 0;
                modifiers.DisableSound(); // Hilangkan suara kena hit
            }
        }

        // Blokir damage dari Projectile (peluru musuh)
        public override void ModifyHitByProjectile(Projectile proj, ref Player.HurtModifiers modifiers) {
            if (IsProtectedByShield) {
                modifiers.FinalDamage.Base = 0;
                modifiers.DisableSound(); // Hilangkan suara kena hit
            }
        }
    }
}