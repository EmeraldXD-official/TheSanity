using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Buff; 
using TheSanity.Items.Whips;

namespace TheSanity.GlobalNPC.Environment
{
    public class BlackwhipGlobalNPC : Terraria.ModLoader.GlobalNPC
    {
        public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone) 
        {
            if (projectile.type == ModContent.ProjectileType<Projectiles.BlackwhipProjectile>()) 
            {
                Player player = Main.player[projectile.owner];
                var modPlayer = player.GetModPlayer<BlackwhipPlayer>();

                int selectedWhip = modPlayer.selectedWhipTagType;
                int duration = 240; 

                npc.AddBuff(ModContent.BuffType<BlackwhipTagBuff>(), duration);

                if (selectedWhip == ModContent.ItemType<AbyssalKrakenTentacle>())
                {
                    npc.AddBuff(70, duration);
                    SpawnTagDust(npc, DustID.Water, 6);
                }
                else if (selectedWhip == ModContent.ItemType<FeatherWireWhip>())
                {
                    player.AddBuff(ModContent.BuffType<FeatherFrenzy>(), duration);
                    SpawnTagDust(npc, DustID.Electric, 5);
                }
                else if (selectedWhip == ItemID.BlandWhip) 
                {
                    npc.AddBuff(149, duration);
                    SpawnTagDust(npc, DustID.Dirt, 3);
                }
                else if (selectedWhip == ItemID.ThornWhip) 
                {
                    npc.AddBuff(20, duration);
                    player.AddBuff(314, duration);
                    SpawnTagDust(npc, DustID.Grass, 5); 
                }
                else if (selectedWhip == ItemID.BoneWhip) 
                {
                    npc.AddBuff(326, duration);
                    SpawnTagDust(npc, DustID.Bone, 4);
                }
                else if (selectedWhip == ItemID.FireWhip) 
                {
                    npc.AddBuff(323, duration);
                    SpawnTagDust(npc, DustID.Torch, 8);
                    
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f, Pitch = 0.2f }, npc.Center);
                    for (int i = 0; i < 10; i++) {
                        int d = Dust.NewDust(npc.position, npc.width, npc.height, DustID.SolarFlare, 0f, 0f, 100, default, 1.5f);
                        Main.dust[d].velocity *= 2f;
                        Main.dust[d].noGravity = true;
                    }
                }
                else if (selectedWhip == ItemID.CoolWhip) 
                {
                    npc.AddBuff(324, duration);
                    SpawnTagDust(npc, DustID.IceTorch, 8);
                }
                else if (selectedWhip == ItemID.SwordWhip) 
                {
                    npc.AddBuff(308, duration);
                    player.AddBuff(308, duration);
                    SpawnTagDust(npc, 57, 6); 
                }
                else if (selectedWhip == ItemID.ScytheWhip) 
                {
                    npc.AddBuff(311, duration);
                    player.AddBuff(311, duration);
                    SpawnTagDust(npc, DustID.Shadowflame, 12);
                }
                else if (selectedWhip == ItemID.MaceWhip) 
                {
                    npc.AddBuff(319, duration);
                    SpawnTagDust(npc, DustID.Gold, 6); 
                }
                else if (selectedWhip == ItemID.RainbowWhip) 
                {
                    npc.AddBuff(316, duration);
                    SpawnTagDust(npc, 267, 10); 
                    
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