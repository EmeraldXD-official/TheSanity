using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.NPCs
{
    // ==========================================
    // BAGIAN 1: REWORK BUTCHER (POHON, SPARK SPREAD, & DEBUFF CONTACT)
    // ==========================================
    public class ButcherRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.Butcher;
        }

        public override bool InstancePerEntity => true;
        private int sparkCooldown = 0; 

        public override void PostAI(NPC npc)
        {
            bool hitSomething = false;

            // Cek koordinat tile di dalam Hitbox Butcher
            int startX = (int)(npc.position.X / 16f);
            int endX = (int)((npc.position.X + npc.width) / 16f);
            int startY = (int)(npc.position.Y / 16f);
            int endY = (int)((npc.position.Y + npc.height) / 16f);

            for (int x = startX; x <= endX; x++)
            {
                for (int y = startY; y <= endY; y++)
                {
                    // 🔥 FIX: Menggunakan Terraria.Tile agar tidak bentrok dengan nama folder/namespace Tile kamu
                    Terraria.Tile tile = Main.tile[x, y];
                    
                    if (tile.HasTile && (tile.TileType == TileID.Trees || tile.TileType == TileID.PalmTree))
                    {
                        // Hancurkan pohonnya
                        WorldGen.KillTile(x, y, false, false, false);
                        hitSomething = true;
                    }
                }
            }

            // Cek Critter dan Town NPC di dalam Hitbox Butcher
            foreach (NPC target in Main.ActiveNPCs)
            {
                if (target.whoAmI != npc.whoAmI && npc.Hitbox.Intersects(target.Hitbox))
                {
                    if (target.CountsAsACritter || target.townNPC)
                    {
                        target.SimpleStrikeNPC(50, npc.direction);
                        hitSomething = true;
                    }
                }
            }

            // Jika mengenai pohon/critter/town NPC dan cooldown habis
            if (hitSomething && sparkCooldown <= 0)
            {
                SpawnSpreadSpark(npc);
            }

            if (sparkCooldown > 0)
            {
                sparkCooldown--;
            }
        }

        // ==========================================
        // [GUIDE LOCATION 1: HIT PLAYER & DEBUFF CONTACT]
        // Fungsi yang berjalan ketika gergaji Butcher mengenai player secara langsung
        // ==========================================
        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo)
        {
            // Memberikan debuff Bleeding saat terkena contact damage.
            target.AddBuff(BuffID.Bleeding, 300);

            // Pemicu percikan api (Spark) jika cooldown-nya sudah habis
            if (sparkCooldown <= 0)
            {
                SpawnSpreadSpark(npc);
            }
        }

        // Fungsi mekanik semburan acak (Spread Spark) ke depan
        private void SpawnSpreadSpark(NPC npc)
        {
            int sparkDamage = 30; 

            float speedX = npc.direction * Main.rand.NextFloat(5f, 9f); 
            float speedY = Main.rand.NextFloat(-4f, 1f);              
            
            Vector2 velocity = new Vector2(speedX, speedY);

            int projIndex = Projectile.NewProjectile(
                npc.GetSource_FromAI(), 
                npc.Center, 
                velocity, 
                ProjectileID.WandOfSparkingSpark, 
                sparkDamage, 
                0f, 
                Main.myPlayer
            );

            Main.projectile[projIndex].hostile = true;
            Main.projectile[projIndex].friendly = false;

            // 30 frame = 0,5 detik jeda antar spark
            sparkCooldown = 30; 
        }
    }

    // ==========================================
    // BAGIAN 2: MENAMBAHKAN DEBUFF ONFIRE3 KE SPARK VANILLA
    // ==========================================
    public class HostileSparkDebuff : GlobalProjectile
    {
        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info)
        {
            if (projectile.type == ProjectileID.WandOfSparkingSpark && projectile.hostile)
            {
                target.AddBuff(BuffID.OnFire3, 180); // 180 frame = 3 detik
            }
        }
    }
}