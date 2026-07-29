using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria.GameContent;

namespace TheSanity.Projectiles.TwinkleWeapon
{
    public class TwinkleTwinkleMinion : ModProjectile
    {
        public override string Texture => "TheSanity/Projectiles/TwinkleWeapon/TwinkleTwinkleMinion";

        private int Level => Math.Clamp((int)Projectile.minionSlots, 1, 20);

        private int skillCooldown = 0;
        private int dashCount = 0;

        // Variabel untuk tracking pola bintang
        private int starStep = 0;
        private int idleStarTimer = 0;
        private bool doingIdleStar = false;
        private readonly int[] starOrder = { 0, 2, 4, 1, 3 }; 

        // Struktur data kustom untuk menyimpan jejak garis dan bintik terang via Code saat Idle
        private struct StarLine {
            public Vector2 Start;
            public Vector2 End;
            public int TimeLeft;
        }

        private struct StarCorner {
            public Vector2 Position;
            public int TimeLeft;
        }

        private List<StarLine> idleLines = new List<StarLine>();
        private List<StarCorner> idleCorners = new List<StarCorner>();

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = true;
            
            // Mengaktifkan fitur cache posisi untuk efek bayangan visual (Shadow Trail)
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 15; 
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 22;   // Ukuran Sesuai Sprite 22x24
            Projectile.height = 24;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.minion = true;
            Projectile.minionSlots = 1f;
            Projectile.penetrate = -1;
            Projectile.netImportant = true;
        }

        // Mengizinkan minion memberikan damage kontak hanya saat menyerang (Dash / Pentagram)
        public override bool MinionContactDamage() {
            return Projectile.ai[0] == 1 || Projectile.ai[0] == 2;
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];

            if (player.dead || !player.active) player.ClearBuff(ModContent.BuffType<TwinkleTwinkleBuff>());
            if (player.HasBuff(ModContent.BuffType<TwinkleTwinkleBuff>())) Projectile.timeLeft = 2;

            // Update timer rasi bintang kosmik hiasan saat Idle
            for (int i = idleLines.Count - 1; i >= 0; i--) {
                var line = idleLines[i];
                line.TimeLeft--;
                if (line.TimeLeft <= 0) idleLines.RemoveAt(i);
                else idleLines[i] = line;
            }
            for (int i = idleCorners.Count - 1; i >= 0; i--) {
                var corner = idleCorners[i];
                corner.TimeLeft--;
                if (corner.TimeLeft <= 0) idleCorners.RemoveAt(i);
                else idleCorners[i] = corner;
            }

            // Proteksi kapasitas slot minion
            float otherSlots = 0f;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile other = Main.projectile[i];
                if (other.active && other.owner == Projectile.owner && other.whoAmI != Projectile.whoAmI && other.minion) {
                    otherSlots += other.minionSlots;
                }
            }
            float maxAllowed = player.maxMinions - otherSlots;
            if (maxAllowed < 1f) maxAllowed = 1f;
            if (Projectile.minionSlots > maxAllowed) {
                Projectile.minionSlots = maxAllowed;
            }

            // STATS SCALING
            float statDamageMult = 1f + (Level - 1) * 0.75f;
            float statSpeedMult = 1f + (Level - 1) * 0.015f;
            Projectile.damage = (int)(Projectile.originalDamage * statDamageMult);
            Projectile.scale = 1f; 

            // TARGETING SYSTEM
            NPC target = null;
            float targetRange = 900f;

            NPC ownerMinionAttackTargetNPC = Projectile.OwnerMinionAttackTargetNPC;
            if (ownerMinionAttackTargetNPC != null && ownerMinionAttackTargetNPC.CanBeChasedBy(Projectile)) {
                if (Vector2.Distance(Projectile.Center, ownerMinionAttackTargetNPC.Center) < targetRange) {
                    target = ownerMinionAttackTargetNPC;
                }
            }

            if (target == null) {
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (npc.CanBeChasedBy(Projectile)) {
                        float dist = Vector2.Distance(Projectile.Center, npc.Center);
                        if (dist < targetRange) {
                            target = npc;
                            targetRange = dist;
                        }
                    }
                }
            }

            // LOGIKA PERGERAKAN & SERANGAN
            if (target != null) {
                doingIdleStar = false;
                idleStarTimer = 0;
                skillCooldown++;

                if (Projectile.ai[0] == 0) { 
                    // PURE HOVER (Bersiap di belakang player)
                    Vector2 chargeReadyPos = player.Center + new Vector2(player.direction * -35f, -60f);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, chargeReadyPos - Projectile.Center, 0.12f);
                    Projectile.rotation += 0.2f; 

                    if (skillCooldown >= 65) {
                        Projectile.ai[0] = Main.rand.NextBool() ? 1 : 2; 
                        skillCooldown = 0;
                        dashCount = 0;
                        starStep = 0;
                        Projectile.netUpdate = true;
                    }
                }
                else if (Projectile.ai[0] == 1) { // SKILL 1: PURE DASH ATTACK
                    if (dashCount == 0) dashCount = Main.rand.Next(3, 6);

                    Projectile.ai[1]++;
                    if (Projectile.ai[1] < 20) { // Wind-up (Ancang-ancang)
                        Vector2 lockPos = target.Center + (Projectile.Center - target.Center).SafeNormalize(Vector2.UnitY) * 140f;
                        Projectile.velocity = Vector2.Lerp(Projectile.velocity, (lockPos - Projectile.Center) * 0.25f, 0.15f);
                        Projectile.rotation = (target.Center - Projectile.Center).ToRotation() + MathHelper.PiOver2;
                    }
                    else if (Projectile.ai[1] == 20) { // Eksekusi Terkam
                        Projectile.velocity = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * (22f * statSpeedMult);
                    }
                    else if (Projectile.ai[1] > 20 && Projectile.ai[1] < 42) {
                        Projectile.rotation += 0.45f;
                        // Keterangan: Trail Homing Star mekanik lama di sini sudah dihapus total!
                    }
                    else if (Projectile.ai[1] >= 42) {
                        dashCount--;
                        Projectile.ai[1] = 0;
                        if (dashCount <= 0) Projectile.ai[0] = 0;
                    }
                }
                else if (Projectile.ai[0] == 2) { // SKILL 2: POLA BINTANG RASI DI TARGET
                    float radius = 180f;
                    float angle = (starOrder[starStep] * (MathHelper.TwoPi / 5f)) - MathHelper.PiOver2;
                    Vector2 targetNode = target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;

                    float moveSpeed = (15f + Level * 0.5f) * statSpeedMult;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetNode - Projectile.Center, 0.22f);
                    if (Projectile.velocity.Length() > moveSpeed) {
                        Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * moveSpeed;
                    }
                    Projectile.rotation += 0.3f;

                    if (Main.rand.NextBool(2)) {
                        Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame, Vector2.Zero, 0, Color.White, 1f);
                        d.noGravity = true; 
                    }

                    if (Vector2.Distance(Projectile.Center, targetNode) < 24f) {
                        for (int i = 0; i < 8; i++) {
                            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.YellowTorch, Main.rand.NextVector2Circular(3f, 3f), 0, Color.White, 1.3f);
                            d.noGravity = true;
                        }

                        // Tembakkan 5 Homing Star menyebar dari sudut (Bukan trail jalan, melainkan burst ledakan)
                        float baseAngle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                        for (int k = 0; k < 5; k++) {
                            float starAngle = baseAngle + (k * MathHelper.TwoPi / 5f);
                            Vector2 starVel = new Vector2(MathF.Cos(starAngle), MathF.Sin(starAngle)) * 9f;
                            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, starVel, 
                                ModContent.ProjectileType<TwinkleMinionStar>(), (int)(Projectile.damage * 0.85f), 1f, Projectile.owner);
                        }

                        starStep++;
                        if (starStep >= 5) {
                            Projectile.ai[0] = 0; 
                            starStep = 0;
                        }
                        Projectile.netUpdate = true;
                    }
                }
            } 
            else { // IDLE LOGIC
                Projectile.ai[0] = 0;

                if (!doingIdleStar) {
                    idleStarTimer++;
                    float idleAngle = (float)Main.GameUpdateCount * 0.05f;
                    Vector2 idlePos = player.Center + new Vector2(MathF.Cos(idleAngle) * 40f, -70f + MathF.Sin(idleAngle * 2f) * 10f);
                    
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, idlePos - Projectile.Center, 0.1f);
                    Projectile.rotation = Projectile.velocity.X * 0.05f;

                    if (idleStarTimer >= 360) {
                        doingIdleStar = true;
                        starStep = 0;
                        idleStarTimer = 0;
                        Projectile.netUpdate = true;
                    }
                } 
                else {
                    float radius = 70f;
                    Vector2 starCenter = player.Center + new Vector2(0, -110f);
                    float angle = (starOrder[starStep] * (MathHelper.TwoPi / 5f)) - MathHelper.PiOver2;
                    Vector2 targetNode = starCenter + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;

                    Vector2 prevCenter = Projectile.Center;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetNode - Projectile.Center, 0.15f);
                    Projectile.rotation += 0.3f;

                    if (Vector2.Distance(prevCenter, Projectile.Center) > 0.5f) {
                        idleLines.Add(new StarLine {
                            Start = prevCenter,
                            End = Projectile.Center,
                            TimeLeft = 180 
                        });
                    }

                    if (Vector2.Distance(Projectile.Center, targetNode) < 15f) {
                        idleCorners.Add(new StarCorner {
                            Position = Projectile.Center,
                            TimeLeft = 180
                        });

                        for (int i = 0; i < 6; i++) {
                            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.YellowTorch, Main.rand.NextVector2Circular(1.5f, 1.5f), 0, Color.White, 1.4f);
                            d.noGravity = true;
                        }

                        starStep++;
                        if (starStep >= 5) {
                            doingIdleStar = false; 
                            starStep = 0;
                        }
                    }
                }
            }

            Lighting.AddLight(Projectile.Center, 1.0f, 0.9f, 0.4f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            NPC ownerMinionAttackTargetNPC = Projectile.OwnerMinionAttackTargetNPC;
            if (ownerMinionAttackTargetNPC != null && target.whoAmI == ownerMinionAttackTargetNPC.whoAmI && Projectile.ai[0] == 1) {
                int starCount = Main.rand.Next(5, 10);
                for (int i = 0; i < starCount; i++) {
                    Vector2 sampleSpawn = target.Center + new Vector2(Main.rand.NextFloat(-80, 80), Main.rand.NextFloat(-550, -450));
                    Vector2 fallingSpeed = new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), 14f);

                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), sampleSpawn, fallingSpeed, 
                        ModContent.ProjectileType<TwinkleMinionStar>(), (int)(Projectile.damage * 0.80f), 0.5f, Projectile.owner);
                }
            }
        }

        public override bool PreDraw(ref Color drawColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = texture.Size() / 2f;
            Texture2D pixelTex = TextureAssets.MagicPixel.Value;

            // RENDERING GARIS BINTANG IDLE KUSTOM
            foreach (var line in idleLines) {
                float opacity = (float)line.TimeLeft / 180f;
                Color lineColor = Color.Gold * opacity * 0.7f;
                Vector2 direction = line.End - line.Start;
                float angle = (float)Math.Atan2(direction.Y, direction.X);
                
                Main.spriteBatch.Draw(pixelTex, line.Start - Main.screenPosition, new Rectangle(0, 0, 1, 1), 
                    lineColor, angle, Vector2.Zero, new Vector2(direction.Length() + 1.5f, 2f), SpriteEffects.None, 0f);
            }

            // RENDERING BINTIK LEBIH TERANG DI UJUNG SUDUT
            foreach (var corner in idleCorners) {
                float opacity = (float)corner.TimeLeft / 180f;
                Vector2 screenPos = corner.Position - Main.screenPosition;
                Main.spriteBatch.Draw(pixelTex, screenPos, new Rectangle(0, 0, 1, 1), Color.White * opacity, 0f, new Vector2(0.5f, 0.5f), 6f * opacity, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(pixelTex, screenPos, new Rectangle(0, 0, 1, 1), Color.Gold * opacity, 0f, new Vector2(0.5f, 0.5f), 3f * opacity, SpriteEffects.None, 0f);
            }

            // 1. SHADOW TRAIL (Diperkuat dengan opasitas dasar pekat 0.65f agar mantap secara visual)
            int maxTrailFrames = Math.Clamp(2 + (Level / 2), 2, 15); 
            for (int i = maxTrailFrames - 1; i >= 0; i--) {
                Vector2 oldDrawPos = Projectile.oldPos[i] + (Projectile.Size / 2f) - Main.screenPosition;
                Color shadowColor = Color.Yellow * (0.65f * (1f - (float)i / maxTrailFrames));
                Main.spriteBatch.Draw(texture, oldDrawPos, null, shadowColor, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0f);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 2. OUTLINE SHADER (Kuning menyala neon)
            int outlineThickness = 1 + (Level / 5); 
            for (int x = -outlineThickness; x <= outlineThickness; x += outlineThickness) {
                for (int y = -outlineThickness; y <= outlineThickness; y += outlineThickness) {
                    if (x != 0 || y != 0) {
                        Main.spriteBatch.Draw(texture, drawPos + new Vector2(x, y), null, Color.Gold * 0.5f, 
                            Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0f);
                    }
                }
            }

            // 3. GAMBAR UTAMA SPRITE
            Main.spriteBatch.Draw(texture, drawPos, null, drawColor, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(texture, drawPos, null, Color.White * 0.3f, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }
    }
}