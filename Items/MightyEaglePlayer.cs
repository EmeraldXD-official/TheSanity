using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameInput;
using Microsoft.Xna.Framework;
using Terraria.Audio;
using TheSanity.Items;               
using TheSanity.Projectiles; 
using TheSanity.Systems;             

namespace TheSanity.Items
{
    public class MightyEaglePlayer : ModPlayer
    {
        public bool hasSardine;
        public int eagleCooldown;

        public override void ResetEffects()
        {
            hasSardine = false;
        }

        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (hasSardine && MightyEagleKeybindSystem.EagleStrikeKey.JustPressed && eagleCooldown <= 0)
            {
                NPC target = CariTarget(Player.Center, 2000f);
                
                if (target != null)
                {
                    Vector2 arahLempar = target.Center - Player.Center;
                    arahLempar.Normalize();
                    Vector2 kecepatanSarden = arahLempar * 18f; 

                    int damageSesuaiBuff = (int)Player.GetTotalDamage(DamageClass.Generic).ApplyTo(1010);

                    Projectile.NewProjectile(Player.GetSource_Accessory(new Item(ModContent.ItemType<MightyEagleSardine>())), 
                        Player.Center, kecepatanSarden, ModContent.ProjectileType<SardineProjectile>(), 
                        1, 0f, Player.whoAmI, target.whoAmI, damageSesuaiBuff); 

                    SoundEngine.PlaySound(SoundID.Item1, Player.Center);
                    CombatText.NewText(Player.getRect(), Color.Cyan, "Bait Thrown!", dramatic: true);
                    
                    eagleCooldown = 880; 
                }
            }
            else if (hasSardine && MightyEagleKeybindSystem.EagleStrikeKey.JustPressed && eagleCooldown > 0)
            {
                int sisaDetik = (eagleCooldown / 60) + 1;
                CombatText.NewText(Player.getRect(), Color.Red, $"Mighty Eagle Cooldown! ({sisaDetik}s)");
            }
        }

        public override void PostUpdate()
        {
            if (eagleCooldown > 0) 
            {
                eagleCooldown--;
                if (eagleCooldown == 0)
                {
                    CombatText.NewText(Player.getRect(), Color.LimeGreen, "Mighty Eagle Ready!", dramatic: true);
                }
            }
        }

        private NPC CariTarget(Vector2 position, float maxDetectDistance)
        {
            NPC closestBoss = null;
            NPC closestNormal = null;
            
            float maxBossDistSQ = maxDetectDistance * maxDetectDistance;
            float maxNormalDistSQ = maxDetectDistance * maxDetectDistance;

            foreach (NPC target in Main.ActiveNPCs)
            {
                if (target.CanBeChasedBy())
                {
                    NPC targetAsli = target;

                    // 1. PASANG FILTER ANTI-KLON LUNATIC CULTIST
                    if (target.type == NPCID.CultistBossClone)
                    {
                        for (int i = 0; i < Main.maxNPCs; i++)
                        {
                            if (Main.npc[i].active && Main.npc[i].type == NPCID.CultistBoss)
                            {
                                targetAsli = Main.npc[i]; // Paksa alihkan pandangan ke Cultist Asli
                                break;
                            }
                        }
                    }

                    // 2. Filter boss segmentasi biasa (Eater of Worlds, Destroyer, dll)
                    if (target.realLife >= 0 && target.realLife < Main.maxNPCs)
                    {
                        NPC induk = Main.npc[target.realLife];
                        if (induk.active) targetAsli = induk;
                    }

                    float distSQ = Vector2.DistanceSquared(targetAsli.Center, position);
                    
                    if (targetAsli.boss)
                    {
                        if (distSQ < maxBossDistSQ)
                        {
                            maxBossDistSQ = distSQ;
                            closestBoss = targetAsli;
                        }
                    }
                    else
                    {
                        if (distSQ < maxNormalDistSQ)
                        {
                            maxNormalDistSQ = distSQ;
                            closestNormal = targetAsli;
                        }
                    }
                }
            }

            if (closestBoss != null) return closestBoss;
            return closestNormal;
        }
    }
}