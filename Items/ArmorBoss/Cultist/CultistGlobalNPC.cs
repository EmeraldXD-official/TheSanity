using Terraria;
using Terraria.ModLoader;

namespace TheSanity.Items.ArmorBoss.Cultist
{
    public class CultistGlobalNPC : Terraria.ModLoader.GlobalNPC
    {
        public override void OnHitByItem(NPC npc, Terraria.Player player, Item item, NPC.HitInfo hit, int damageDone)
        {
            player.GetModPlayer<Items.ArmorBoss.Cultist.Player.CultistPlayer>().NotifyDirectHit();
        }

        public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
        {
            if (projectile.owner >= 0 && projectile.owner < Main.maxPlayers)
            {
                Terraria.Player owner = Main.player[projectile.owner];
                if (owner.active)
                    owner.GetModPlayer<Items.ArmorBoss.Cultist.Player.CultistPlayer>().NotifyDirectHit();
            }
        }
    }
}
