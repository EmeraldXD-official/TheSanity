using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Buff; 

namespace TheSanity.GlobalNPC.Environment
{
    public class BlackwhipGlobalNPC : Terraria.ModLoader.GlobalNPC
    {
        // =========================================================================
        // BAGIAN 1: Memberikan Buff/Tag ke Musuh & Player Saat Terkena Blackwhip
        // =========================================================================
        public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone) 
        {
            if (projectile.type == ModContent.ProjectileType<Projectiles.BlackwhipProjectile>()) 
            {
                Player player = Main.player[projectile.owner];
                var modPlayer = player.GetModPlayer<BlackwhipPlayer>();

                int selectedWhip = modPlayer.selectedWhipTagType;
                int duration = 240; 

                // 1. SELALU berikan kustom buff utama Blackwhip (+23 damage minion)
                npc.AddBuff(ModContent.BuffType<BlackwhipTagBuff>(), duration);

                // 2. Berikan Sub-Power Buff Vanilla & Efek Unik Mekanik Masing-Masing Whip
                if (selectedWhip == ItemID.BlandWhip) 
                {
                    npc.AddBuff(272, duration); // Leather Whip Tag
                    SpawnTagDust(npc, DustID.Dirt, 3);
                }
                else if (selectedWhip == ItemID.ThornWhip) 
                {
                    npc.AddBuff(70, duration); // Snapthorn Poison Tag
                    player.AddBuff(314, duration); // Buff Kecepatan "Jungle's Fury" ke Player
                    SpawnTagDust(npc, DustID.Grass, 5); 
                }
                else if (selectedWhip == ItemID.BoneWhip) 
                {
                    npc.AddBuff(338, duration); // Spinal Tap Tag
                    SpawnTagDust(npc, DustID.Bone, 4);
                }
                else if (selectedWhip == ItemID.FireWhip) 
                {
                    npc.AddBuff(323, duration); // Firecracker Tag
                    SpawnTagDust(npc, DustID.Torch, 8);
                    
                    // KEUNIKAN FIRECRACKER: Memicu ledakan api instan di posisi musuh
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f, Pitch = 0.2f }, npc.Center);
                    for (int i = 0; i < 10; i++) {
                        int d = Dust.NewDust(npc.position, npc.width, npc.height, DustID.SolarFlare, 0f, 0f, 100, default, 1.5f);
                        Main.dust[d].velocity *= 2f;
                        Main.dust[d].noGravity = true;
                    }
                }
                else if (selectedWhip == ItemID.CoolWhip) 
                {
                    npc.AddBuff(324, duration); // Cool Whip Tag (Snowflake)
                    SpawnTagDust(npc, DustID.IceTorch, 8); // Hanya memunculkan partikel es saja
                }
                else if (selectedWhip == ItemID.SwordWhip) 
                {
                    npc.AddBuff(308, duration); // Durendal Tag
                    player.AddBuff(308, duration); // Buff Kecepatan "Durendal's Blessing" ke Player
                    SpawnTagDust(npc, 57, 6); 
                }
                else if (selectedWhip == ItemID.ScytheWhip) 
                {
                    npc.AddBuff(311, duration); // Dark Harvest Tag
                    player.AddBuff(311, duration); // Buff Kecepatan "Harvest Time" ke Player
                    SpawnTagDust(npc, DustID.Shadowflame, 12); // Hanya memunculkan partikel aura kegelapan
                }
                else if (selectedWhip == ItemID.MaceWhip) 
                {
                    npc.AddBuff(319, duration); // Morning Star Tag
                    SpawnTagDust(npc, DustID.Gold, 6); 
                }
                else if (selectedWhip == ItemID.RainbowWhip) 
                {
                    npc.AddBuff(316, duration); // Kaleidoscope Tag
                    SpawnTagDust(npc, 267, 10); 
                    
                    // KEUNIKAN KALEIDOSCOPE: Kilatan cahaya prisma pelangi besar saat menebas musuh
                    for (int i = 0; i < 8; i++) {
                        int d = Dust.NewDust(npc.position, npc.width, npc.height, 90, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 150, default, 1.8f);
                        Main.dust[d].noGravity = true;
                    }
                }
            }
        }

        private void SpawnTagDust(NPC npc, int dustID, int count) {
            for (int i = 0; i < count; i++) {
                int d = Dust.NewDust(npc.position, npc.width, npc.height, dustID, 0f, 0f, 100, default(Color), 1.3f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity *= 1.4f;
            }
        }

        // =========================================================================
        // BAGIAN 2: Kalkulasi Bonus +23 Flat Damage untuk Minion (Kode Asli Kamu)
        // =========================================================================
        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
        {
            if (npc.HasBuff(ModContent.BuffType<BlackwhipTagBuff>()))
            {
                if (projectile.CountsAsClass(DamageClass.Summon) && !ProjectileID.Sets.IsAWhip[projectile.type])
                {
                    modifiers.FlatBonusDamage += 23;
                }
            }
        }
    }
}