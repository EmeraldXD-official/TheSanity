using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO; // Wajib untuk sinkronisasi BitWriter/BitReader
using Microsoft.Xna.Framework;
using Terraria.Audio;
using System.IO;

namespace TheSanity
{
    public class FaceMonsterRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Gunakan custom variable, JANGAN npc.ai[] karena AI Fighter vanilla menimpanya
        private int attackTimer = 0;
        private int heldBladeIndex = -1;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.FaceMonster;
        }

        // =========================================================================
        // Sinkronisasi Custom Variable ke Jaringan (Multiplayer Safe)
        // =========================================================================
        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter writer)
        {
            writer.Write(attackTimer);
            writer.Write(heldBladeIndex);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader reader)
        {
            attackTimer = reader.ReadInt32();
            heldBladeIndex = reader.ReadInt32();
        }

        public override void PostAI(NPC npc)
        {
            if (npc.target < 0 || npc.target >= 255) return;
            Player target = Main.player[npc.target];

            if (target == null || !target.active || target.dead)
            {
                attackTimer = 0;
                heldBladeIndex = -1;
                return;
            }

            attackTimer++; // Timer

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                // [SPAWN TIMER BALANCING LOCATION]
                if (attackTimer == 300)
                {
                    Vector2 spawnPos = npc.Top - new Vector2(0, 200f);
                    Vector2 fallVelocity = new Vector2(0f, 10f);

                    // [BLADE SPAWN DAMAGE BALANCING LOCATION]
                    int p = Projectile.NewProjectile(
                        npc.GetSource_FromAI(), 
                        spawnPos, 
                        fallVelocity, 
                        ModContent.ProjectileType<CrimsonBlade>(), 
                        30, // Damage pedang
                        1f, 
                        Main.myPlayer
                    );
                    
                    if (p != Main.maxProjectiles)
                    {
                        heldBladeIndex = p;
                        Main.projectile[p].ai[0] = npc.whoAmI;
                        NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, p);
                    }

                    SoundEngine.PlaySound(SoundID.Item9, npc.Center);
                    npc.netUpdate = true;
                }

                // [THROW TIMER BALANCING LOCATION]
                if (attackTimer >= 420)
                {
                    if (heldBladeIndex != -1)
                    {
                        Projectile blade = Main.projectile[heldBladeIndex];
                        if (blade.active && blade.type == ModContent.ProjectileType<CrimsonBlade>())
                        {
                            Vector2 throwDirection = target.Center - blade.Center;
                            throwDirection.Normalize();
                            
                            // [BLADE THROW SPEED BALANCING LOCATION]
                            blade.velocity = throwDirection * 14f; 
                            blade.ai[1] = 1f;
                            NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, heldBladeIndex);
                        }
                    }

                    attackTimer = 0;
                    heldBladeIndex = -1;
                    npc.netUpdate = true;
                    SoundEngine.PlaySound(SoundID.Item71, npc.Center);
                }
            }

            if (attackTimer > 300 && attackTimer < 420)
            {
                Projectile blade = null;

                if (heldBladeIndex != -1 && Main.projectile[heldBladeIndex].active && Main.projectile[heldBladeIndex].type == ModContent.ProjectileType<CrimsonBlade>())
                {
                    blade = Main.projectile[heldBladeIndex];
                }
                else
                {
                    for (int i = 0; i < Main.maxProjectiles; i++)
                    {
                        if (Main.projectile[i].active && Main.projectile[i].type == ModContent.ProjectileType<CrimsonBlade>() && Main.projectile[i].ai[0] == npc.whoAmI)
                        {
                            blade = Main.projectile[i];
                            heldBladeIndex = i;
                            break;
                        }
                    }
                }

                if (blade != null)
                {
                    blade.velocity = Vector2.Zero;
                    blade.Center = Vector2.Lerp(blade.Center, npc.Center, 0.2f);
                    blade.spriteDirection = npc.direction;

                    if (Main.rand.NextBool(5))
                        Dust.NewDust(blade.position, blade.width, blade.height, DustID.CrimsonTorch);
                }
            }
        }
    }
}