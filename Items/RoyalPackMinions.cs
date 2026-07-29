using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;

namespace TheSanity.Systems
{
    // =========================================================================
    // GLOBAL PROJECTILE (UNTUK DEBUFF TORNADO & HOMING BATU BLIZZARD VANILLA)
    // =========================================================================
    public class RoyalBoneGlobalProj : GlobalProjectile
    {
        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone) {
            if (projectile.type == ProjectileID.DeerclopsRangedProjectile) {
                Player player = Main.player[projectile.owner];
                if (player.active && !player.dead && player.GetModPlayer<RoyalBonePackPlayer>().hasRoyalBonePack) {
                    target.AddBuff(BuffID.Frostburn, 180); 
                }
            }
        }

        // FITUR BARU: Membuat batu es pecahan Blizzard ikut homing ke musuh terdekat
        public override void PostAI(Projectile projectile) {
            if (projectile.type == ProjectileID.DeerclopsRangedProjectile && projectile.friendly) {
                Player player = Main.player[projectile.owner];
                
                // Pastikan dipicu oleh aksesoris Royal Bone Pack yang aktif
                if (player.active && !player.dead && player.GetModPlayer<RoyalBonePackPlayer>().hasRoyalBonePack) {
                    NPC target = projectile.Center.FindClosestNPC(600f);
                    if (target != null) {
                        Vector2 desiredVelocity = (target.Center - projectile.Center).SafeNormalize(Vector2.Zero) * 14f;
                        // Lerp halus agar gerakan belok pecahan es terlihat natural dan presisi
                        projectile.velocity = Vector2.Lerp(projectile.velocity, desiredVelocity, 0.09f);
                    }
                }
            }
        }
    }

    // ==========================================
    // 1. BLIZZARDNADO (MAGIC) 
    // ==========================================
    public class BlizzardnadoProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WeatherPainShot;
        
        private int shootTimer = 0;
        private int shotCounter = 0;    
        private int barrageLeft = 0;    
        private int barrageDelay = 0;   

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = Main.projFrames[ProjectileID.WeatherPainShot];
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];
            Item heldItem = player.HeldItem;
            bool spawnAll = !heldItem.IsAir && heldItem.damage > 0 && heldItem.DamageType == DamageClass.Default;
            bool spawnMagic = spawnAll || (!heldItem.IsAir && heldItem.DamageType.CountsAsClass(DamageClass.Magic));

            if (!player.active || player.dead || !player.GetModPlayer<RoyalBonePackPlayer>().hasRoyalBonePack || !spawnMagic) {
                Projectile.Kill();
                return;
            }

            // SEBELUMNYA: ApplyTo(25) -> SEKARANG: ApplyTo(5)
            Projectile.damage = (int)player.GetDamage(DamageClass.Magic).ApplyTo(5);

            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 4) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Projectile.type];
            }

            NPC target = Projectile.Center.FindClosestNPC(700f);
            Vector2 targetPos;

            if (target != null) {
                float sideChooser = Projectile.Center.X < target.Center.X ? -160f : 160f;
                targetPos = target.Center + new Vector2(sideChooser, -20f);
            } else {
                targetPos = player.Center + new Vector2(-60f, -50f);
            }

            targetPos.Y += (float)Math.Sin(Main.GameUpdateCount * 0.08f) * 12f;

            Vector2 moveDirection = targetPos - Projectile.Center;
            float distance = moveDirection.Length();
            
            if (distance > 8f) {
                moveDirection.Normalize();
                float speed = MathHelper.Clamp(distance * 0.12f, 5f, 18f); 
                Projectile.velocity = moveDirection * speed;
            } else {
                Projectile.velocity = Vector2.Zero;
            }

            if (target != null) {
                if (barrageLeft > 0) {
                    barrageDelay++;
                    if (barrageDelay >= 3) {
                        barrageDelay = 0;
                        barrageLeft--;

                        if (Main.myPlayer == Projectile.owner) {
                            Vector2 shootVel = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero).RotatedByRandom(MathHelper.ToRadians(12)) * 16f;
                            int p = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, shootVel, ProjectileID.DeerclopsRangedProjectile, Projectile.damage, Projectile.knockBack, Projectile.owner);
                            if (p != Main.maxProjectiles) {
                                Main.projectile[p].friendly = true;
                                Main.projectile[p].hostile = false;
                                Main.projectile[p].DamageType = DamageClass.Magic;
                                Main.projectile[p].usesLocalNPCImmunity = true;
                                Main.projectile[p].localNPCHitCooldown = 20; 
                            }
                        }
                    }
                } else {
                    shootTimer++;
                    if (shootTimer >= 40) {
                        shootTimer = 0;
                        shotCounter++;

                        if (shotCounter >= 5) {
                            shotCounter = 0;
                            barrageLeft = 15;
                            barrageDelay = 0;
                        } else {
                            if (Main.myPlayer == Projectile.owner) {
                                Vector2 shootVel = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 14f;
                                int p = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, shootVel, ProjectileID.DeerclopsRangedProjectile, Projectile.damage, Projectile.knockBack, Projectile.owner);
                                if (p != Main.maxProjectiles) {
                                    Main.projectile[p].friendly = true;
                                    Main.projectile[p].hostile = false;
                                    Main.projectile[p].DamageType = DamageClass.Magic;
                                    Main.projectile[p].usesLocalNPCImmunity = true;
                                    Main.projectile[p].localNPCHitCooldown = 20;
                                }
                            }
                        }
                    }
                }
            } else {
                shootTimer = 0;
                shotCounter = 0;
                barrageLeft = 0;
            }
        }
    }

    // ==========================================
    // 2. SKULLYTRON (SUMMONER)
    // ==========================================
    public class SkullytronProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BabySkeletronHead;
        private int attackTimer = 0;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];
            Item heldItem = player.HeldItem;
            bool spawnAll = !heldItem.IsAir && heldItem.damage > 0 && heldItem.DamageType == DamageClass.Default;
            bool spawnSummon = spawnAll || (!heldItem.IsAir && (heldItem.DamageType.CountsAsClass(DamageClass.Summon) || heldItem.DamageType.CountsAsClass(DamageClass.SummonMeleeSpeed)));

            if (!player.active || player.dead || !player.GetModPlayer<RoyalBonePackPlayer>().hasRoyalBonePack || !spawnSummon) {
                Projectile.Kill();
                return;
            }

            // SEBELUMNYA: ApplyTo(22) -> SEKARANG: ApplyTo(5)
            Projectile.damage = (int)player.GetDamage(DamageClass.Summon).ApplyTo(5);

            NPC target = Projectile.Center.FindClosestNPC(700f);
            if (target != null) {
                Projectile.rotation += 0.4f;
                Vector2 dir = target.Center - Projectile.Center;
                Projectile.velocity = dir.SafeNormalize(Vector2.Zero) * 14f;

                attackTimer++;
                if (attackTimer >= 90) {
                    attackTimer = 0;
                    if (Main.myPlayer == Projectile.owner) {
                        SoundEngine.PlaySound(SoundID.Item8, Projectile.Center); 
                        
                        for (int i = 0; i < 10; i++) {
                            float angle = MathHelper.TwoPi / 10f * i;
                            Vector2 launchVel = new Vector2(0, 5.4f).RotatedBy(angle); 
                            
                            Projectile.NewProjectile(
                                Projectile.GetSource_FromThis(), 
                                Projectile.Center, 
                                launchVel, 
                                ModContent.ProjectileType<SkullytronSkull>(), 
                                (int)(Projectile.damage * 0.9f), 
                                1f, 
                                Projectile.owner, 
                                0f,            
                                target.whoAmI  
                            );
                        }
                    }
                }
            } else {
                attackTimer = 0;
                Vector2 orbitOffset = new Vector2(50f, 0f).RotatedBy(Main.GameUpdateCount * 0.03f);
                orbitOffset.Y += (float)Math.Sin(Main.GameUpdateCount * 0.06f) * 10f;
                Vector2 idlePos = player.Center + orbitOffset;
                
                Projectile.velocity = (idlePos - Projectile.Center) * 0.08f;
                Projectile.rotation = (float)Math.Sin(Main.GameUpdateCount * 0.05f) * 0.35f; 
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.BrokenArmor, 300);
        }
    }

    // ==========================================
    // SKULLYTRON SKULL (BONE GLOVE - STABIL)
    // ==========================================
    public class SkullytronSkull : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BoneGloveProj;

        public override void SetDefaults() {
            Projectile.width = 18;  
            Projectile.height = 18;
            Projectile.friendly = false; 
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false; 
            Projectile.timeLeft = 240;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            Projectile.ai[0]++; 

            int targetIndex = (int)Projectile.ai[1];
            NPC target = null;
            if (targetIndex >= 0 && targetIndex < Main.maxNPCs && Main.npc[targetIndex].active && Main.npc[targetIndex].CanBeChasedBy()) {
                target = Main.npc[targetIndex];
            } else {
                target = Projectile.Center.FindClosestNPC(800f); 
            }

            if (Projectile.ai[0] < 30) {
                Projectile.friendly = false; 
                Projectile.rotation += 0.25f; 
            } 
            else {
                Projectile.friendly = true; 
                if (target != null) {
                    Vector2 targetDir = target.Center - Projectile.Center;
                    Vector2 desiredVelocity = targetDir.SafeNormalize(Vector2.Zero) * 18f; 
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, 0.18f);
                    Projectile.rotation += 0.35f; 
                } else {
                    Projectile.velocity *= 0.95f;
                    if (Projectile.timeLeft > 20) Projectile.timeLeft = 20;
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.BrokenArmor, 300);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY);

            Main.EntitySpriteDraw(texture, drawPos, null, lightColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false; 
        }
    }

    // ==========================================
    // 3. CROWNYSLIME (MELEE)
    // ==========================================
    public class CrownySlimeProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.KingSlimePet;
        
        private Vector2 realPlayerPos;
        private bool isSpoofing = false;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 12;
        }

        public override void SetDefaults() {
            Projectile.CloneDefaults(ProjectileID.KingSlimePet);
            AIType = ProjectileID.KingSlimePet; 
            Projectile.width = 28;
            Projectile.height = 24;
            Projectile.friendly = true;     
            Projectile.tileCollide = true;   
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override bool TileCollideStyle(ref int width, ref int height, ref bool fallThrough, ref Vector2 hitboxCenterInWorld) {
            Player player = Main.player[Projectile.owner];
            NPC target = Projectile.Center.FindClosestNPC(700f);
            
            if (target != null) {
                fallThrough = target.Center.Y > Projectile.Bottom.Y + 16f; 
            } else {
                fallThrough = player.Center.Y > Projectile.Bottom.Y + 16f;
            }
            return true;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) { return false; } 

        public override bool PreAI() {
            Player player = Main.player[Projectile.owner];
            Item heldItem = player.HeldItem;
            bool spawnAll = !heldItem.IsAir && heldItem.damage > 0 && heldItem.DamageType == DamageClass.Default;
            bool spawnMelee = spawnAll || (!heldItem.IsAir && heldItem.DamageType.CountsAsClass(DamageClass.Melee));

            if (!player.active || player.dead || !player.GetModPlayer<RoyalBonePackPlayer>().hasRoyalBonePack || !spawnMelee) {
                Projectile.Kill();
                return false;
            }

            // SEBELUMNYA: ApplyTo(28) -> SEKARANG: ApplyTo(5)
            Projectile.damage = (int)player.GetDamage(DamageClass.Melee).ApplyTo(5);
            Projectile.timeLeft = 2;

            float distToRealPlayer = Vector2.Distance(Projectile.Center, player.Center);
            if (distToRealPlayer > 1200f) {
                Projectile.Center = player.Center;
                Projectile.velocity = Vector2.Zero;
                Projectile.netUpdate = true;
                isSpoofing = false;
                return true; 
            }

            NPC target = Projectile.Center.FindClosestNPC(700f);
            if (target != null) {
                realPlayerPos = player.Center;
                player.Center = target.Center;
                isSpoofing = true;
            } else {
                isSpoofing = false;
            }

            return true; 
        }

        public override void PostAI() {
            Player player = Main.player[Projectile.owner];
            if (isSpoofing) {
                player.Center = realPlayerPos;
                isSpoofing = false;
            }

            NPC target = Projectile.Center.FindClosestNPC(700f);
            if (target != null) {
                if (Projectile.velocity.Y < -6.2f) Projectile.velocity.Y = -6.2f; 
                if (Projectile.velocity.Y != 0f) {
                    float xDist = target.Center.X - Projectile.Center.X;
                    Projectile.velocity.X = MathHelper.Clamp(xDist * 0.1f, -5.5f, 5.5f);
                }
                Projectile.spriteDirection = target.Center.X > Projectile.Center.X ? -1 : 1;
            } else {
                if (Math.Abs(Projectile.velocity.X) > 0.1f) {
                    Projectile.spriteDirection = Projectile.velocity.X > 0f ? -1 : 1;
                } else {
                    Projectile.spriteDirection = player.Center.X > Projectile.Center.X ? -1 : 1;
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire, 180);  
            target.AddBuff(BuffID.Slimed, 300);  

            Projectile.velocity.Y = -7.0f; 
            float xDist = target.Center.X - Projectile.Center.X;
            Projectile.velocity.X = Math.Sign(xDist) * 3.5f;

            if (Main.myPlayer == Projectile.owner) {
                int spikes = Main.rand.Next(5, 9); 
                for (int i = 0; i < spikes; i++) {
                    Vector2 spikeVel = new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-9f, -5f));
                    
                    int p = Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(), 
                        Projectile.Center, 
                        spikeVel, 
                        ModContent.ProjectileType<CrownySlimeSpike>(), 
                        Projectile.damage, 
                        1f, 
                        Projectile.owner,
                        0f,
                        target.whoAmI // Mengirimkan index target yang diinjak ke ai[1] proyektil Spike
                    );
                    
                    if (p != Main.maxProjectiles) {
                        Main.projectile[p].friendly = true;
                        Main.projectile[p].hostile = false;
                        Main.projectile[p].DamageType = DamageClass.Melee;
                        Main.projectile[p].usesLocalNPCImmunity = true;
                        Main.projectile[p].localNPCHitCooldown = 15;
                    }
                }
            }
            Projectile.netUpdate = true;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            int numFrames = 12; 
            int frameHeight = texture.Height / numFrames;
            int actualFrame = 0;
            
            if (Projectile.velocity.Y != 0f) {
                actualFrame = 4; 
            } else if (Math.Abs(Projectile.velocity.X) > 0.1f) {
                actualFrame = (int)(Main.GameUpdateCount / 5) % 6; 
            } else {
                actualFrame = 0; 
            }

            Rectangle sourceRectangle = new Rectangle(0, actualFrame * frameHeight, texture.Width, frameHeight);
            Vector2 origin = new Vector2(texture.Width / 2f, frameHeight / 2f);
            
            SpriteEffects effects = Projectile.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            drawPos.Y += Projectile.gfxOffY;

            Main.EntitySpriteDraw(texture, drawPos, sourceRectangle, lightColor, Projectile.rotation, origin, Projectile.scale, effects, 0);
            return false; 
        }
    }

    // =========================================================================
    // CROWNY SLIME SPIKE (FIXED: HOMING LANGSUNG KE TARGET YANG SEDANG DIINJAK)
    // =========================================================================
    public class CrownySlimeSpike : ModProjectile 
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SpikedSlimeSpike;

        public override void SetDefaults() {
            Projectile.width = 6;
            Projectile.height = 6;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false; // Set ke false agar tidak pecaah membentur tanah/platform saat dilempar ke bawah
            Projectile.timeLeft = 180;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            
            Projectile.ai[0]++; 

            // Jeda dikurangi dari 60 frame menjadi 10 frame agar burst ledakan duri terlihat sebentar sebelum berbelok tajam
            if (Projectile.ai[0] >= 60) {
                int slimeTargetIndex = (int)Projectile.ai[1]; 
                NPC target = null;

                // PRIORITAS 1: Mengunci musuh asli yang saat ini sedang diinjak oleh Crowny Slime
                if (slimeTargetIndex >= 0 && slimeTargetIndex < Main.maxNPCs && Main.npc[slimeTargetIndex].active && Main.npc[slimeTargetIndex].CanBeChasedBy()) {
                    target = Main.npc[slimeTargetIndex];
                } 
                // PRIORITAS 2: Jika musuh utama sudah mati tengah jalan, cari musuh terdekat lain di sekitar
                else {
                    target = Projectile.Center.FindClosestNPC(600f); 
                }
                
                if (target != null) {
                    Vector2 trackingVelocity = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 14f;
                    // Lerp dinaikkan ke 0.15f membuat belokan peluru duri sangat agresif dan responsif
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, trackingVelocity, 0.15f);
                } else {
                    Projectile.velocity.Y += 0.16f;
                    if (Projectile.velocity.Y > 16f) Projectile.velocity.Y = 16f;
                }
            } else {
                // Sedikit efek gravitasi ringan di awal peluncuran (frame 1-9)
                Projectile.velocity.Y += 0.05f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire, 180);  
            target.AddBuff(BuffID.Slimed, 300);  
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY);
            
            Main.EntitySpriteDraw(texture, drawPos, null, lightColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    // ==========================================
    // 4. BABYNET (RANGER)
    // ==========================================
    public class BabyNetProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BabyHornet;
        private int flipTimer = 0;
        private float customRotation = 0f;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = Main.projFrames[ProjectileID.BabyHornet];
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];
            Item heldItem = player.HeldItem;
            bool spawnAll = !heldItem.IsAir && heldItem.damage > 0 && heldItem.DamageType == DamageClass.Default;
            bool spawnRanger = spawnAll || (!heldItem.IsAir && heldItem.DamageType.CountsAsClass(DamageClass.Ranged));

            if (!player.active || player.dead || !player.GetModPlayer<RoyalBonePackPlayer>().hasRoyalBonePack || !spawnRanger) {
                Projectile.Kill();
                return;
            }

            // SEBELUMNYA: ApplyTo(24) -> SEKARANG: ApplyTo(5)
            Projectile.damage = (int)player.GetDamage(DamageClass.Ranged).ApplyTo(5);

            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 4) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Projectile.type];
            }

            NPC target = Projectile.Center.FindClosestNPC(700f);
            if (target != null) {
                Vector2 orbitPos = target.Center + new Vector2(160, 0).RotatedBy(Main.GameUpdateCount * 0.03f);
                Projectile.velocity = (orbitPos - Projectile.Center) * 0.1f;
                Projectile.spriteDirection = (target.Center.X > Projectile.Center.X) ? -1 : 1;

                flipTimer++;
                if (flipTimer >= 120) {
                    customRotation += 0.5f;
                    if (customRotation >= MathHelper.TwoPi) {
                        customRotation = 0f;
                        flipTimer = 0;

                        if (Main.myPlayer == Projectile.owner) {
                            for (int i = 0; i < 10; i++) {
                                float angle = MathHelper.TwoPi / 10f * i;
                                Vector2 launchVel = new Vector2(0, 5f).RotatedBy(angle);
                                int p = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, launchVel, ModContent.ProjectileType<BabyNetStinger>(), Projectile.damage, 1f, Projectile.owner);
                                if (p != Main.maxProjectiles) {
                                    Main.projectile[p].usesLocalNPCImmunity = true;
                                    Main.projectile[p].localNPCHitCooldown = 15;
                                }
                            }
                        }
                    }
                    Projectile.rotation = customRotation;
                } else {
                    Projectile.rotation = Projectile.velocity.X * 0.05f;
                }
            } else {
                Vector2 idlePos = player.Center + new Vector2(-40, -50);
                Projectile.velocity = (idlePos - Projectile.Center) * 0.08f;
                Projectile.rotation = Projectile.velocity.X * 0.05f;
                Projectile.spriteDirection = (player.direction == 1) ? -1 : 1;
                flipTimer = 0;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Poisoned, 240);
        }
    }

    public class BabyNetStinger : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Stinger;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.timeLeft = 240;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            NPC target = Projectile.Center.FindClosestNPC(450f);
            if (target != null) {
                Vector2 targetVel = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 7.5f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetVel, 0.09f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Poisoned, 240);
        }
    }

    // ==========================================
    // EXTENSION UTILITY 
    // ==========================================
    public static class ProjectileExtensions {
        public static NPC FindClosestNPC(this Vector2 center, float maxRange) {
            NPC closest = null;
            float maxDistance = maxRange;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.CanBeChasedBy()) {
                    float dist = Vector2.Distance(center, npc.Center);
                    if (dist < maxDistance) {
                        maxDistance = dist;
                        closest = npc;
                    }
                }
            }
            return closest;
        }

        public static NPC FindClosestNPCExcluding(this Vector2 center, float maxRange, int excludeIndex) {
            NPC closest = null;
            float maxDistance = maxRange;
            for (int i = 0; i < Main.maxNPCs; i++) {
                if (i == excludeIndex) continue; 
                
                NPC npc = Main.npc[i];
                if (npc.CanBeChasedBy()) {
                    float dist = Vector2.Distance(center, npc.Center);
                    if (dist < maxDistance) {
                        maxDistance = dist;
                        closest = npc;
                    }
                }
            }
            return closest;
        }
    }
}