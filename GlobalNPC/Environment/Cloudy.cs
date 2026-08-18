using System;
using CollisionLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Terraria.Audio;
using Terraria.DataStructures;

namespace TheSanity.NPCs
{
    // =========================================================================
    // 1. KODE UTAMA ENTIAS SPACE RAIN CLOUD NPC (DENGAN WSOLID PLATFORM)
    // =========================================================================
    public class SpaceRainCloudNPC : ModNPC
    {
        private int lifetimeTimer = 0;
        private int maxLifetime = 0;
        
        private int lightningCooldownTimer = 0;   
        private int chosenCooldown = 0;           
        private bool isChargingLightning = false; 
        
        private bool isFreezingPostLightning = false; 
        private int postLightningFreezeTimer = 0;     

        private int landingWindowTimer = 0;

        // ==========================================================================
        // FISIK SOLID PLATFORM SEKARANG PAKAI COLLISIONLIB (Impact Library), SAMA
        // KAYAK ArenaBorderColliderNPC - BUKAN LAGI Rectangle-check + set posisi/
        // velocity player manual kayak sebelumnya.
        //
        // platformSurface cuma 1 garis lurus (bukan poligon kayak arena lingkaran)
        // yang nempel di TEPI ATAS hitbox awan, dan di-REBUILD tiap tick (posisi awan
        // gerak-gerak terus karena angin) lewat RebuildPlatformCollider() di bawah -
        // pola sama persis kayak RebuildColliders() punya ArenaBorderColliderNPC.
        //
        // style array-nya [bawah, atas, kiri, kanan] (lihat komentar di
        // ArenaBorderColliderNPC.RebuildColliders): di sana border pakai
        // {1,1,1,1} (solid dari segala arah, gak ada yg bisa ditembus). Di sini
        // sengaja CUMA "atas" yang di-set 1 ({0,1,0,0}) supaya sifatnya ONE-WAY
        // PLATFORM kayak platform vanilla: bisa didaki/ditembus dari bawah waktu
        // lompat, tapi solid (bisa dipijak) kalau didatangi dari atas pas jatuh.
        // CATATAN: makna pasti tiap bit style ini ditentukan internal CollisionLib
        // (source-nya gak ada di project ini buat di-cek), jadi kalau ternyata
        // kebalik (awan malah nembus dari atas / nge-block dari bawah), tinggal
        // tukar posisi 0 dan 1 di array platformStyle di bawah.
        //
        // grappleable di-set TRUE - sistem grapple manual yang lama (tracker
        // hookedNpcIndex, narik player ke bawah awan pakai set position/velocity
        // manual) UDAH DIHAPUS, soalnya gak support multiplayer dengan benar
        // (cuma client pemilik hook yang bener, client lain desync) dan visual
        // hook-nya nyeleneh (snap ke bawah awan, bukan nempel wajar). Sekarang
        // full pakai grapple bawaan CollisionLib, sama kayak border arena.
        // ==========================================================================
        private static readonly int[] platformStyle = { 0, 1, 0, 0 };
        private CollisionSurface platformSurface;

        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.RainCloudRaining;

        public override void SetStaticDefaults()
        {
            NPCID.Sets.ImmuneToAllBuffs[Type] = true;
        }

        public override void SetDefaults()
        {
            NPC.width = 54;            
            NPC.height = 24;           
            NPC.damage = 0;            
            NPC.defense = 0;
            NPC.lifeMax = 100;
            NPC.dontTakeDamage = true; 
            NPC.noGravity = true;      
            NPC.noTileCollide = false; 
            NPC.aiStyle = -1;          
            NPC.value = 0f;            
        }

        public override bool CheckActive()
        {
            return false;
        }

        // Rebuild garis collider (tepi atas hitbox) ke posisi awan TERKINI. Dipanggil
        // tiap tick dari AI() - persis pola RebuildColliders() di ArenaBorderColliderNPC,
        // cuma di sini cukup 1 garis aja (bukan poligon banyak sisi) karena bentuknya
        // platform datar, bukan lingkaran/kotak penuh.
        private void RebuildPlatformCollider()
        {
            Vector2 topLeft = NPC.position;
            Vector2 topRight = NPC.position + new Vector2(NPC.width, 0f);

            platformSurface = new CollisionSurface(topLeft, topRight, platformStyle, true);
        }

        public override float SpawnChance(NPCSpawnInfo spawnInfo)
        {
            // Catatan: Main.raining SENGAJA ga dicek di sini — hujan cuma syarat buat
            // aktifin skill petir Cloudy (lihat AI()), bukan syarat spawn-nya.
            int cap = Main.raining ? 60 : 30; // cap dikali 2 pas hujan
            if (NPC.CountNPCS(Type) >= cap)
                return 0f;

            if (spawnInfo.Player.ZoneSkyHeight || spawnInfo.Player.ZoneOverworldHeight)
                return 0.3f;

            return 0f;
        }

        public override void AI()
        {
            NPC.TargetClosest(true);
            Player targetPlayer = Main.player[NPC.target];

            // --- DETEKSI DAN PENYESUAIAN TINGGI TERBANG SAAT SPAWN DI SURFACE ---
            if (NPC.localAI[0] == 0f)
            {
                NPC.localAI[0] = 1f; 
                if (targetPlayer.ZoneOverworldHeight && !targetPlayer.ZoneSkyHeight)
                {
                    NPC.position.Y -= Main.rand.Next(600, 901); 
                    NPC.netUpdate = true; 
                }
            }

            if (!targetPlayer.active || targetPlayer.dead)
                NPC.ai[3] = 1f;

            if (NPC.ai[3] == 1f)
            {
                NPC.alpha += 4; 
                if (NPC.alpha >= 255)
                {
                    NPC.active = false; 
                    return;
                }
            }

            if (NPC.ai[3] != 1f)
            {
                if (maxLifetime == 0)
                    maxLifetime = Main.rand.Next(1200, 2101);

                lifetimeTimer++;
                if (lifetimeTimer >= maxLifetime)
                    NPC.ai[3] = 1f;
            }

            // --- MEKANIK KONTROL CUACA HUJAN (ANTI-NYAMBER SAAT CERAH) ---
            if (!Main.raining)
            {
                lightningCooldownTimer = 0;
                isChargingLightning = false;
                isFreezingPostLightning = false;
                postLightningFreezeTimer = 0;
            }
            else
            {
                if (chosenCooldown == 0)
                    chosenCooldown = Main.rand.Next(600, 901);

                if (NPC.ai[3] != 1f && !isFreezingPostLightning)
                    lightningCooldownTimer++;

                if (lightningCooldownTimer >= (chosenCooldown - 60) && lightningCooldownTimer < chosenCooldown)
                    isChargingLightning = true;

                if (lightningCooldownTimer >= chosenCooldown)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Vector2 finalTargetCenter = NPC.Center;
                        bool foundValidTarget = false;
                        float highestTargetScore = -1f;

                        float scanRangeX = 20f * 16f; 
                        float scanRangeY = 1000f;     

                        // Pindai Player
                        for (int i = 0; i < Main.maxPlayers; i++)
                        {
                            Player p = Main.player[i];
                            if (p.active && !p.dead && !p.ghost && p.Center.Y > NPC.Center.Y)
                            {
                                float xDist = Math.Abs(p.Center.X - NPC.Center.X);
                                float yDist = p.Center.Y - NPC.Center.Y;
                                if (xDist <= scanRangeX && yDist <= scanRangeY)
                                {
                                    float currentScore = 2000f - yDist;
                                    if (p.HasBuff(BuffID.Wet))
                                        currentScore += 6000f;
                                    if (currentScore > highestTargetScore)
                                    {
                                        highestTargetScore = currentScore;
                                        finalTargetCenter = p.Center;
                                        foundValidTarget = true;
                                    }
                                }
                            }
                        }

                        // Pindai NPC lain
                        for (int i = 0; i < Main.maxNPCs; i++)
                        {
                            NPC n = Main.npc[i];
                            if (n.active && n.whoAmI != NPC.whoAmI && n.type != NPC.type && n.Center.Y > NPC.Center.Y)
                            {
                                float xDist = Math.Abs(n.Center.X - NPC.Center.X);
                                float yDist = n.Center.Y - NPC.Center.Y;
                                if (xDist <= scanRangeX && yDist <= scanRangeY)
                                {
                                    float currentScore = 2000f - yDist;
                                    if (n.HasBuff(BuffID.Wet))
                                        currentScore += 6000f;
                                    if (currentScore > highestTargetScore)
                                    {
                                        highestTargetScore = currentScore;
                                        finalTargetCenter = n.Center;
                                        foundValidTarget = true;
                                    }
                                }
                            }
                        }

                        Vector2 lightningVelocity = new Vector2(0f, 14f);
                        if (foundValidTarget)
                        {
                            Vector2 shootDirection = finalTargetCenter - NPC.Center;
                            shootDirection.Normalize();
                            lightningVelocity = shootDirection * 14f;
                        }
                        
                        int pProj = Projectile.NewProjectile(
                            NPC.GetSource_FromAI(), 
                            NPC.Center, 
                            lightningVelocity, 
                            ProjectileID.VortexLightning, 
                            45,                   
                            0f, 
                            Main.myPlayer, 
                            lightningVelocity.ToRotation() 
                        );
                        
                        if (pProj < Main.maxProjectiles)
                        {
                            Main.projectile[pProj].alpha = 255;      
                            Main.projectile[pProj].hostile = true;   
                            Main.projectile[pProj].friendly = false; 
                            Main.projectile[pProj].netUpdate = true;
                        }
                    }

                    SoundEngine.PlaySound(SoundID.Thunder, NPC.Center);

                    isChargingLightning = false;
                    isFreezingPostLightning = true;
                    postLightningFreezeTimer = 54; 
                    lightningCooldownTimer = 0;
                }

                if (isFreezingPostLightning)
                {
                    postLightningFreezeTimer--;
                    if (postLightningFreezeTimer <= 0)
                    {
                        isFreezingPostLightning = false;
                        chosenCooldown = Main.rand.Next(600, 901);
                    }
                }
            }

            // --- GAYA FISIK BADAN UTAMA (SOLID PLATFORM VIA CollisionLib) ---
            // Rebuild garis collider ke posisi awan tick ini, lalu Update() - fisik
            // "nempel/gak bisa tembus dari atas"-nya sekarang beneran ditangani
            // CollisionLib (sama kayak border arena), BUKAN lagi kode manual di sini.
            RebuildPlatformCollider();
            platformSurface.Update();

            // Loop di bawah ini SEKARANG cuma buat efek gameplay (blackout, kunci
            // animasi, fallStart, "awan pecah kalau kejeblos kekencengan", dst) -
            // TIDAK LAGI maksa posisi/velocity player (itu udah kerjaan
            // platformSurface di atas). Deteksinya baca kondisi player yang
            // (diasumsikan) udah diresolve CollisionLib: berdiri tepat di tepi atas
            // awan dan velocity.Y-nya sudah 0.
            bool playerOnTop = false;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                if (player.active && !player.dead)
                {
                    if (player.GoingDownWithGrapple || player.controlDown)
                        continue;

                    // Deteksi "lagi berdiri di atas awan": horizontal overlap sama
                    // hitbox awan, kaki player nempel pas di tepi atasnya, dan
                    // velocity.Y udah 0 (tanda CollisionLib udah nyetop jatuhnya
                    // tick ini). Toleransi 6px buat jaga-jaga float rounding.
                    bool horizontalOverlap = player.position.X + player.width > NPC.position.X
                        && player.position.X < NPC.position.X + NPC.width;
                    bool restingOnTop = Math.Abs((player.position.Y + player.height) - NPC.position.Y) <= 6f;

                    if (horizontalOverlap && restingOnTop && player.velocity.Y == 0f)
                    {
                        float kecepatanHantaman = player.oldVelocity.Y;
                        playerOnTop = true;

                        // Mempertahankan logika internal Cloudy awal
                        player.fallStart = (int)(player.position.Y / 16f);
                        player.GetModPlayer<CloudyPlayerBlackout>().standingOnCloud = true;

                        if (player.mount.Active)
                            player.mount.ResetFlightTime(player.velocity.X);

                        if (landingWindowTimer == 0)
                            landingWindowTimer = 30;

                        float batasKecepatanHancur = player.mount.Active ? 7.0f : 11.0f;
                        if (kecepatanHantaman >= batasKecepatanHancur && landingWindowTimer > 0)
                            NPC.ai[3] = 1f;

                        // Mengunci gerakan animasi player agar tidak glitching di udara saat berdiri
                        if (Math.Abs(player.velocity.X) < 0.01f)
                        {
                            player.legFrame.Y = 0;
                            player.legFrameCounter = 0;
                        }
                        player.wingFrame = 0;
                        player.wingFrameCounter = 0;
                        player.bodyFrame.Y = 0;
                        player.bodyFrameCounter = 0;
                    }
                }
            }

            if (landingWindowTimer > 0)
                landingWindowTimer--;

            // Mekanik grapple hook manual (tracker/hookedNpcIndex, narik player ke
            // bawah awan, dst) UDAH DIHAPUS - grapple sekarang sepenuhnya ditangani
            // CollisionLib lewat platformSurface (grappleable = true di
            // RebuildPlatformCollider()), yang notabene emang lebih aman buat
            // multiplayer (gak ada lagi manual set position/velocity player yang
            // gampang desync) dan visualnya ngikutin standar CollisionLib, bukan
            // patokan jarak 54f + snap posisi manual kayak sebelumnya.

            // --- LOGIKA GERAKAN DINAMIS + ANGIN ---
            if (NPC.ai[1] == 0f)
                NPC.ai[1] = Main.rand.NextBool() ? 1f : -1f;

            if (NPC.collideX)
            {
                NPC.ai[1] *= -1f;
                NPC.ai[3] = 1f;
            }

            float baseSpeed = 1.8f;
            float windPushEffect = Main.windSpeedCurrent * 3.0f;
            float calculatedSpeedX = (NPC.ai[1] * baseSpeed) + windPushEffect;

            if (NPC.ai[1] > 0f && calculatedSpeedX < 0.25f) calculatedSpeedX = 0.25f;
            if (NPC.ai[1] < 0f && calculatedSpeedX > -0.25f) calculatedSpeedX = -0.25f;

            NPC.velocity.X = calculatedSpeedX;

            if (isChargingLightning || isFreezingPostLightning)
                NPC.velocity = Vector2.Zero;
            else if (playerOnTop)
                NPC.velocity = Vector2.Zero;
        }

        // Bagian kedua dari siklus CollisionLib tiap tick - sama kayak
        // ArenaBorderColliderNPC.PostAI(), harus dipanggil biar collider-nya
        // "kelar" diproses library sebelum tick berikutnya.
        public override void PostAI()
        {
            platformSurface?.PostUpdate();
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D texture = TextureAssets.Projectile[ProjectileID.RainCloudRaining].Value;
            int totalFrames = Main.projFrames[ProjectileID.RainCloudRaining];
            if (totalFrames <= 0) totalFrames = 4;
            
            int frameHeight = texture.Height / totalFrames;
            int animatedFrame = (int)(Main.GameUpdateCount / 7) % totalFrames;
            Rectangle sourceRectangle = new Rectangle(0, animatedFrame * frameHeight, texture.Width, frameHeight);
            Vector2 textureOrigin = sourceRectangle.Size() * 0.5f;

            Color cloudColor = drawColor * (1f - (NPC.alpha / 255f));
            if (Main.raining && Main.cloudAlpha >= 0.5f)
                cloudColor = cloudColor * 0.40f;

            if (isChargingLightning && NPC.ai[3] != 1f)
            {
                Color glowColor = Color.Cyan * 1f * (1f - (NPC.alpha / 255f));
                Vector2[] outlineOffsets = new Vector2[]
                {
                    new Vector2(-3, 0), new Vector2(3, 0), new Vector2(0, -3), new Vector2(0, 3),
                    new Vector2(-2, -2), new Vector2(2, -2), new Vector2(-2, 2), new Vector2(2, 2)
                };
                foreach (Vector2 offset in outlineOffsets)
                {
                    spriteBatch.Draw(texture, NPC.Center + offset - screenPos, sourceRectangle, glowColor, NPC.rotation, textureOrigin, NPC.scale, SpriteEffects.None, 0f);
                }
            }

            spriteBatch.Draw(texture, NPC.Center - screenPos, sourceRectangle, cloudColor, NPC.rotation, textureOrigin, NPC.scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    // =========================================================================
    // 2. KODE KONTROL GLOBAL PROJECTILE (SISTEM IDENTITAS INDUK & HOOK TRACKER)
    // =========================================================================
    public class CloudyProjectileTracker : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public bool isFromCustomCloud = false;
        public bool isCustomCloudSpark = false;
        private bool hasSpawnedSparks = false;

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (projectile.type == ProjectileID.VortexLightning && source is EntitySource_Parent parentSource && parentSource.Entity is NPC npc && npc.type == ModContent.NPCType<SpaceRainCloudNPC>())
            {
                isFromCustomCloud = true;
            }
            
            if (projectile.type == ProjectileID.Spark && source is EntitySource_Parent sparkParent && sparkParent.Entity is Projectile parentProj)
            {
                if (parentProj.TryGetGlobalProjectile(out CloudyProjectileTracker parentTracker) && parentTracker.isFromCustomCloud)
                {
                    isCustomCloudSpark = true;
                }
            }
        }

        public override bool? CanHitNPC(Projectile projectile, NPC target)
        {
            if ((isFromCustomCloud || isCustomCloudSpark))
            {
                if (target.type == ModContent.NPCType<SpaceRainCloudNPC>())
                    return false;
                return true;
            }
            return base.CanHitNPC(projectile, target);
        }

        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo hurtInfo)
        {
            if (isFromCustomCloud || isCustomCloudSpark)
            {
                if (isFromCustomCloud)
                    target.AddBuff(BuffID.Electrified, 180);
                
                target.GetModPlayer<CloudyPlayerBlackout>().blackoutTimer = 180;
            }
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (isFromCustomCloud || isCustomCloudSpark)
            {
                if (isFromCustomCloud)
                    target.AddBuff(BuffID.Electrified, 180);

                if (target.TryGetGlobalNPC(out CloudyNPCBlackout npcBlackout))
                    npcBlackout.blackoutTimer = 180;
            }
        }

        public override bool OnTileCollide(Projectile projectile, Vector2 oldVelocity)
        {
            if (isFromCustomCloud)
                TriggerSparkSpout(projectile);
            return base.OnTileCollide(projectile, oldVelocity);
        }

        public override void PostAI(Projectile projectile)
        {
            // Update posisi hook manual (hookedNpcIndex) UDAH DIHAPUS - grapple ke
            // awan sekarang murni ditangani CollisionLib (platformSurface di
            // SpaceRainCloudNPC, grappleable = true), gak perlu campur tangan di sini.

            // --- TRIGGER SPARK SAAT PETIR MENYENTUH TANAH ---
            if (isFromCustomCloud && !hasSpawnedSparks && (projectile.velocity.Y == 0f || Collision.SolidCollision(projectile.position, projectile.width, projectile.height)))
            {
                TriggerSparkSpout(projectile);
            }
        }

        private void TriggerSparkSpout(Projectile projectile)
        {
            if (hasSpawnedSparks) return;
            hasSpawnedSparks = true;

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                int spawnCount = Main.rand.Next(4, 7);
                for (int i = 0; i < spawnCount; i++)
                {
                    Vector2 launchVelocity = new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-6f, -3f));
                    int sparkDamage = projectile.damage / 2;

                    int sparkProj = Projectile.NewProjectile(
                        projectile.GetSource_FromThis(),
                        projectile.Center,
                        launchVelocity,
                        ProjectileID.Spark,
                        sparkDamage,
                        0f,
                        Main.myPlayer
                    );

                    if (sparkProj < Main.maxProjectiles)
                    {
                        Main.projectile[sparkProj].hostile = true;
                        Main.projectile[sparkProj].friendly = true;
                        Main.projectile[sparkProj].timeLeft = 120;
                        Main.projectile[sparkProj].netUpdate = true;
                    }
                }
            }
        }
    }

    // =========================================================================
    // 3. KODE MOD PLAYER (SISTEM CORAK HITAM GOSONG PLAYER - ALL LAYERS)
    // =========================================================================
    public class CloudyPlayerBlackout : ModPlayer
    {
        public int blackoutTimer = 0;
        public bool standingOnCloud = false;

        public override void ResetEffects()
        {
            standingOnCloud = false;
        }

        public override void PostUpdateMiscEffects()
        {
            if (blackoutTimer > 0)
                blackoutTimer--;
        }

        public override void PreUpdateMovement()
        {
            if (standingOnCloud)
            {
                Player.velocity.Y = 0f;
                Player.fallStart = (int)(Player.position.Y / 16f);
            }
        }

        public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo)
        {
            if (blackoutTimer > 0)
            {
                float intensity = 1f;
                if (blackoutTimer < 30)
                    intensity = blackoutTimer / 30f;

                for (int i = 0; i < drawInfo.DrawDataCache.Count; i++)
                {
                    DrawData data = drawInfo.DrawDataCache[i];
                    data.color = Color.Lerp(data.color, Color.Black, intensity);
                    drawInfo.DrawDataCache[i] = data;
                }
            }
        }
    }

    // =========================================================================
    // 4. KODE GLOBAL NPC (SISTEM CORAK HITAM GOSONG ALL NPC VIA PRE-DRAW MANUAL TINT)
    // =========================================================================
    public class CloudyNPCBlackout : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;
        public int blackoutTimer = 0;

        public override void PostAI(NPC npc)
        {
            if (blackoutTimer > 0)
                blackoutTimer--;
        }

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (blackoutTimer > 0)
            {
                float intensity = 1f;
                if (blackoutTimer < 30)
                    intensity = blackoutTimer / 30f;

                Texture2D texture = TextureAssets.Npc[npc.type].Value;
                Vector2 drawOrigin = npc.frame.Size() / 2f;

                SpriteEffects effects = SpriteEffects.None;
                if (npc.spriteDirection == 1)
                    effects = SpriteEffects.FlipHorizontally;

                Vector2 drawPos = npc.Center - screenPos;
                drawPos.Y += npc.gfxOffY;

                Color finalColor = Color.Lerp(drawColor, Color.Black, intensity);

                spriteBatch.Draw(
                    texture,
                    drawPos,
                    npc.frame,
                    npc.GetAlpha(finalColor),
                    npc.rotation,
                    drawOrigin,
                    npc.scale,
                    effects,
                    0f
                );

                return false;
            }
            return true;
        }
    }

    // =========================================================================
    // 5. KODE GLOBAL NPC (LOCK ABSOLUT SPAWN FREQUENCY - ANTI MODIFIER BIOME/BUFF)
    // =========================================================================
    public class SpaceRainCloudSpawnBypass : global::Terraria.ModLoader.GlobalNPC
    {
        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
        {
            if (Main.raining && (player.ZoneOverworldHeight || player.ZoneSkyHeight))
            {
                // "dikali 2": spawnRate makin KECIL = spawn makin sering, jadi dibagi 2
                // (bukan dikali) supaya frekuensi spawn-nya beneran 2x lipat.
                // maxSpawns (slot enemy) dikali 2 seperti biasa.
                spawnRate /= 2;
                maxSpawns *= 2;
            }
        }
    }
}