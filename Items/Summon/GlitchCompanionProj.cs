using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.Summon
{
    public class GlitchCompanionProj : ModProjectile
    {
        public override void SetStaticDefaults() {
            Main.projFrames[Type] = 14; // Membaca 14 Frame
            Main.projPet[Type] = true;
            ProjectileID.Sets.MinionSacrificable[Type] = false;
            ProjectileID.Sets.MinionTargettingFeature[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.minion = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.minionSlots = 1f;
            Projectile.penetrate = -1;
            Projectile.netImportant = true;
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];

            // 1. Cek ketersediaan Player & Buff
            if (player.dead || !player.active) {
                player.ClearBuff(ModContent.BuffType<GlitchCompanionBuff>());
            }
            if (player.HasBuff(ModContent.BuffType<GlitchCompanionBuff>())) {
                Projectile.timeLeft = 2;
            }

            // 2. POSISI: Melayang di atas kepala player
            float floatSway = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * 3f;
            Vector2 targetHeadPos = player.Center + new Vector2(0, -48f + floatSway);

            Projectile.Center = Vector2.Lerp(Projectile.Center, targetHeadPos, 0.3f);
            Projectile.spriteDirection = player.direction;

            // 3. ANIMASI 14 FRAME
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 4) {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= 14) {
                    Projectile.frame = 0;
                }
            }

            // 4. LOGIKA MENEMBAK MUSUH
            NPC targetNPC = FindTarget(player);

            if (targetNPC != null) {
                Projectile.spriteDirection = targetNPC.Center.X > Projectile.Center.X ? 1 : -1;

                // Cooldown Tembak (Tiap 40 tick / ~0.66 detik)
                Projectile.ai[0]++;
                if (Projectile.ai[0] >= 40f) {
                    Projectile.ai[0] = 0f;

                    if (Main.myPlayer == Projectile.owner) {
                        Vector2 shootVel = Vector2.Normalize(targetNPC.Center - Projectile.Center) * 10f;
                        
                        // PROYEKTIL TEMBAKAN: DemonScythe (Sabit meluncur berputar)
                        int projIndex = Projectile.NewProjectile(
                            Projectile.GetSource_FromAI(),
                            Projectile.Center,
                            shootVel,
                            ProjectileID.ScytheWhipProj,
                            Projectile.damage,
                            Projectile.knockBack,
                            Projectile.owner
                        );

                        if (projIndex >= 0 && projIndex < Main.maxProjectiles) {
                            Projectile spawnedProj = Main.projectile[projIndex];
                            spawnedProj.friendly = true;
                            spawnedProj.hostile = false;
                            spawnedProj.DamageType = DamageClass.Summon;
                            spawnedProj.owner = Projectile.owner;
                            // Menghilangkan jeda lambat DemonScythe agar langsung meluncur cepat
                            spawnedProj.ai[0] = 30f; 
                            spawnedProj.netUpdate = true;
                        }
                    }

                    // SFX TEMBAKAN ELEKTRONIK/LASER (Bukan swing/tebasan)
                    SoundEngine.PlaySound(SoundID.Item12, Projectile.Center); // Suara Laser/Zap

                    for (int i = 0; i < 8; i++) {
                        Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Electric, 0, 0, 100, default, 1f);
                    }
                }
            } else {
                Projectile.ai[0] = 0f;
            }

            Lighting.AddLight(Projectile.Center, 0.2f, 0.3f, 0.5f);
        }

        private NPC FindTarget(Player player) {
            NPC target = null;
            float maxDistance = 650f;

            if (player.HasMinionAttackTargetNPC) {
                NPC npc = Main.npc[player.MinionAttackTargetNPC];
                if (npc.CanBeChasedBy() && Vector2.Distance(Projectile.Center, npc.Center) < maxDistance) {
                    return npc;
                }
            }

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.CanBeChasedBy()) {
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < maxDistance) {
                        maxDistance = dist;
                        target = npc;
                    }
                }
            }

            return target;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            
            int frameHeight = tex.Height / 14;
            Rectangle sourceRect = new Rectangle(0, Projectile.frame * frameHeight, tex.Width, frameHeight);
            Vector2 origin = new Vector2(tex.Width / 2f, frameHeight / 2f);
            SpriteEffects effects = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, sourceRect, Color.White, 0f, origin, 0.35f, effects, 0);

            return false;
        }
    }
}