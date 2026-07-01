using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Terraria.Audio;
using Terraria.DataStructures; // Diperlukan untuk IEntitySource, EntitySource_Parent, dan DrawData

namespace TheSanity.NPCs
{
    // =========================================================================
    // 1. KODE UTAMA ENTIAS SPACE RAIN CLOUD NPC (SMART TARGETING - 20 BLOCKS RADIUS)
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

        // [SISTEM TIMER BARU UNTUK FITUR HOOK & SLAM LANDING]
        private int hookedLifetimeTimer = 0; // Menyimpan durasi waktu saat awan di-hook player
        private int landingWindowTimer = 0;  // Jendela deteksi 0.5 detik setelah mendarat

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

        public override float SpawnChance(NPCSpawnInfo spawnInfo)
        {
            if (NPC.CountNPCS(Type) >= 150) 
            {
                return 0f;
            }

            // [LOC] [VAL] KONTROL SPAWN RATE UTAMA
            if (spawnInfo.Player.ZoneSkyHeight || spawnInfo.Player.ZoneOverworldHeight)
            {
                return 900.0f; 
            }
            
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
            {
                NPC.ai[3] = 1f; 
            }

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
                {
                    maxLifetime = Main.rand.Next(1200, 2101); 
                }

                lifetimeTimer++;
                if (lifetimeTimer >= maxLifetime)
                {
                    NPC.ai[3] = 1f; 
                }
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
                {
                    chosenCooldown = Main.rand.Next(600, 901); 
                }

                if (NPC.ai[3] != 1f && !isFreezingPostLightning)
                {
                    lightningCooldownTimer++;
                }

                if (lightningCooldownTimer >= (chosenCooldown - 60) && lightningCooldownTimer < chosenCooldown)
                {
                    isChargingLightning = true; 
                }

                if (lightningCooldownTimer >= chosenCooldown)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        // INITIALISASI SISTEM SMART-TARGETING UNIVERSAL
                        Vector2 finalTargetCenter = NPC.Center;
                        bool foundValidTarget = false;
                        float highestTargetScore = -1f;

                        // [LOC] [VAL] RADIUS TARGETING JANGKAUAN (1 Blok = 16 Piksel)
                        float scanRangeX = 20f * 16f; // Kunci 20 Blok ke kiri dan 20 Blok ke kanan (Total 320px)
                        float scanRangeY = 1000f;     // Jangkauan vertikal ke bawah (Tetap jauh agar bisa mendeteksi tanah)

                        // 1. PEMINDAIAN BLOK FACTION PLAYER
                        for (int i = 0; i < Main.maxPlayers; i++)
                        {
                            Player p = Main.player[i];
                            if (p.active && !p.dead && !p.ghost)
                            {
                                // IMMUNITY CHECK: Harus berada di bawah koordinat Y awan
                                if (p.Center.Y > NPC.Center.Y)
                                {
                                    float xDist = Math.Abs(p.Center.X - NPC.Center.X);
                                    float yDist = p.Center.Y - NPC.Center.Y;

                                    if (xDist <= scanRangeX && yDist <= scanRangeY)
                                    {
                                        float currentScore = 2000f - yDist;

                                        if (p.HasBuff(BuffID.Wet))
                                        {
                                            currentScore += 6000f; 
                                        }

                                        if (currentScore > highestTargetScore)
                                        {
                                            highestTargetScore = currentScore;
                                            finalTargetCenter = p.Center;
                                            foundValidTarget = true;
                                        }
                                    }
                                }
                            }
                        }

                        // 2. PEMINDAIAN BLOK FACTION NPC (MENCAKUP: ENEMY, CRITTER, DAN TOWN NPC)
                        for (int i = 0; i < Main.maxNPCs; i++)
                        {
                            NPC n = Main.npc[i];
                            if (n.active && n.whoAmI != NPC.whoAmI && n.type != NPC.type)
                            {
                                // IMMUNITY CHECK: Abaikan NPC yang sejajar atau lebih tinggi dari awan
                                if (n.Center.Y > NPC.Center.Y)
                                {
                                    float xDist = Math.Abs(n.Center.X - NPC.Center.X);
                                    float yDist = n.Center.Y - NPC.Center.Y;

                                    if (xDist <= scanRangeX && yDist <= scanRangeY)
                                    {
                                        float currentScore = 2000f - yDist;

                                        if (n.HasBuff(BuffID.Wet))
                                        {
                                            currentScore += 6000f; 
                                        }

                                        if (currentScore > highestTargetScore)
                                        {
                                            highestTargetScore = currentScore;
                                            finalTargetCenter = n.Center;
                                            foundValidTarget = true;
                                        }
                                    }
                                }
                            }
                        }

                        // [LOC] [VAL] KONTROL ARAH + CEK FALLBACK JANGKAUAN PETIR
                        Vector2 lightningVelocity = new Vector2(0f, 14f); // Kecepatan Petir (Default: 14f lurus ke bawah jika tidak ada target)
                        
                        if (foundValidTarget)
                        {
                            Vector2 shootDirection = finalTargetCenter - NPC.Center;
                            shootDirection.Normalize();
                            lightningVelocity = shootDirection * 14f; // Menembak serong mengarah ke target yang terkunci
                        }
                        
                        int pProj = Projectile.NewProjectile(
                            NPC.GetSource_FromAI(), 
                            NPC.Center, 
                            lightningVelocity, 
                            ProjectileID.VortexLightning, 
                            45, // [LOC] [VAL] Base Damage Petir Awan                   
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

            // --- PLATFORM LOGIC BARU (SOLID DARI ATAS, GHOST DARI BAWAH, FIX HIGH-SPEED BYPASS & WORK KE MOUNT) ---
            bool playerOnTop = false;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                if (player.active && !player.dead)
                {
                    // Cek Jarak Horizontal: Memastikan player berada di area lebar awan
                    if (player.position.X + player.width > NPC.position.X && player.position.X < NPC.position.X + NPC.width)
                    {
                        float playerBottom = player.position.Y + player.height;
                        float playerOldBottom = player.oldPosition.Y + player.height;

                        // [PANDUAN BALANCING: DETEKSI TOLERANSI TINGGI JATUH]
                        // Menggunakan Math.Max agar player yang jatuh super cepat (atau pakai mount) tidak menembus awan dalam 1 frame
                        float batasDeteksiDinamis = Math.Max(16f, player.velocity.Y + 2f);

                        // Syarat menapak: Player jatuh dari atas awan (velocity.Y >= 0) dan kaki melewati batas atas koordinat awan
                        if (playerBottom >= NPC.position.Y && playerOldBottom <= NPC.position.Y + batasDeteksiDinamis && player.velocity.Y >= 0)
                        {
                            // Ambil kecepatan jatuh/hantaman murni sebelum di-reset ke 0
                            float kecepatanHantaman = player.velocity.Y;

                            playerOnTop = true;
                            player.position.Y = NPC.position.Y - player.height;
                            player.velocity.Y = 0;
                            player.fallStart = (int)(player.position.Y / 16f); // Meng-negate fall damage total (Pemain aman)

                            // FIX BUG 2 LOCATION: Mengaktifkan status menapak kustom pada ModPlayer agar visual animasi falling dipadamkan
                            player.GetModPlayer<CloudyPlayerBlackout>().standingOnCloud = true;

                            // Jika menggunakan mount, reset juga flight/jump time internal mount agar tidak glitching
                            if (player.mount.Active)
                            {
                                player.mount.ResetFlightTime(player.velocity.X); // Tweak Perbaikan Error CS7036
                            }

                            // Jendela deteksi 0.5 detik (30 Ticks) langsung diaktifkan saat landing
                            if (landingWindowTimer == 0)
                            {
                                landingWindowTimer = 30;
                            }

                            // [PANDUAN BALANCING: BATAS KECEPATAN HANTAMAN UNTUK MENGHANCURKAN AWAN]
                            // Jika hantaman jatuh biasa atau menggunakan Mount melebihi batas kecepatan ini, awan langsung lenyap.
                            float batasKecepatanHancur = player.mount.Active ? 7.0f : 11.0f;

                            if (kecepatanHantaman >= batasKecepatanHancur && landingWindowTimer > 0)
                            {
                                NPC.ai[3] = 1f; // Langsung picu hilangnya awan secara estetik
                            }
                        }
                    }
                }
            }

            // Kurangi timer jendela landing setiap frame
            if (landingWindowTimer > 0)
            {
                landingWindowTimer--;
            }


            // --- MEKANIK GRAPPLING HOOK CUSTOM (BISA DIHOOK DARI SEGALA ARAH & DI-ROTASI KE BAWAH AWAN) ---
            bool adaHookMenempel = false;
            for (int j = 0; j < Main.maxProjectiles; j++)
            {
                Projectile proj = Main.projectile[j];
                // Pastikan projectile adalah Grappling Hook milik player yang aktif
                if (proj.active && Main.projHook[proj.type])
                {
                    // Cek apakah ujung hook menyentuh area kotak tabrak awan
                    if (proj.Hitbox.Intersects(NPC.Hitbox))
                    {
                        // Paksa status AI hook menjadi '2' (Status internal Terraria yang berarti Hook telah berhasil mengunci/menempel di target)
                        proj.ai[0] = 2f; 
                        proj.Center = NPC.Center; // Kunci koordinat ujung tali hook tepat di tengah awan
                        
                        adaHookMenempel = true;

                        Player playerHook = Main.player[proj.owner];
                        if (playerHook.active && !playerHook.dead)
                        {
                            // FIX BUG 1 LOCATION & BALANCING PULLING MECHANIC
                            // Biarkan vanilla hook melakukan tarikan serong/lurus secara mulus terlebih dahulu.
                            // Kita hanya memaksa posisi terbalik di bawah awan saat player sudah benar-benar dekat dengan inti tubuh awan.
                            // [VAL] Jarak toleransi radius penarikan sebelum nempel total (Default: 54f piksel)
                            float jarakKeAwan = Vector2.Distance(playerHook.Center, NPC.Center);
                            if (jarakKeAwan < 54f)
                            {
                                playerHook.position.X = NPC.Center.X - playerHook.width / 2f; // Otomatis center horizontal di bawah awan
                                playerHook.position.Y = NPC.position.Y + NPC.height + 12f;  // Jarak gantung di bawah awan (12 piksel)
                                playerHook.velocity = Vector2.Zero;                                          // Amankan pergerakan
                            }
                        }
                    }
                }
            }

            // Jika ada hook terdeteksi menempel di awan ini
            if (adaHookMenempel)
            {
                hookedLifetimeTimer++;
                // [PANDUAN BALANCING: TIMER AWAN HILANG SAAT DIHOOK (300 Ticks = 5 Detik)]
                if (hookedLifetimeTimer >= 300)
                {
                    NPC.ai[3] = 1f; // Picu fungsi menghilang estetik bawaan code
                }
            }
            else
            {
                // Reset timer ke 0 jika player melepaskan hook sebelum 5 detik selesai
                hookedLifetimeTimer = 0;
            }


            // --- LOGIKA GERAKAN DINAMIS + ANGIN ---
            if (NPC.ai[1] == 0f)
            {
                NPC.ai[1] = Main.rand.NextBool() ? 1f : -1f; 
            }

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
            {
                NPC.velocity = Vector2.Zero; 
            }
            else if (playerOnTop)
            {
                NPC.velocity = Vector2.Zero; 
            }
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
            {
                cloudColor = cloudColor * 0.40f; 
            }

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
    // 2. KODE KONTROL GLOBAL PROJECTILE (SISTEM IDENTITAS INDUK & FACTION NETRAL)
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
                {
                    return false; 
                }
                return true; 
            }
            return base.CanHitNPC(projectile, target);
        }

        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo hurtInfo)
        {
            if (isFromCustomCloud || isCustomCloudSpark)
            {
                if (isFromCustomCloud)
                {
                    target.AddBuff(BuffID.Electrified, 180); 
                }
                
                // [LOC] [VAL] DURASI HITAM LEGAM PLAYER (180 Ticks = 3 Detik)
                target.GetModPlayer<CloudyPlayerBlackout>().blackoutTimer = 180;
            }
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (isFromCustomCloud || isCustomCloudSpark)
            {
                if (isFromCustomCloud)
                {
                    target.AddBuff(BuffID.Electrified, 180); 
                }

                // [LOC] [VAL] DURASI HITAM LEGAM NPC (180 Ticks = 3 Detik)
                if (target.TryGetGlobalNPC(out CloudyNPCBlackout npcBlackout))
                {
                    npcBlackout.blackoutTimer = 180;
                }
            }
        }

        public override bool OnTileCollide(Projectile projectile, Vector2 oldVelocity)
        {
            if (isFromCustomCloud)
            {
                TriggerSparkSpout(projectile);
            }
            return base.OnTileCollide(projectile, oldVelocity);
        }

        public override void PostAI(Projectile projectile)
        {
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
                    int chosenProjType = ProjectileID.Spark;
                    Vector2 launchVelocity = new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-6f, -3f));
                    int sparkDamage = projectile.damage / 2; 

                    int sparkProj = Projectile.NewProjectile(
                        projectile.GetSource_FromThis(), 
                        projectile.Center, 
                        launchVelocity, 
                        chosenProjType, 
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
        
        // FIX BUG 2 LOCATION: Menambahkan field kustom pelacak injakan awan
        public bool standingOnCloud = false; 

        public override void ResetEffects()
        {
            standingOnCloud = false; // Reset otomatis setiap awal frame baru
        }

        public override void PostUpdateMiscEffects()
        {
            if (blackoutTimer > 0)
            {
                blackoutTimer--;
            }
        }

        // PERBAIKAN ERROR CS0115: Menggunakan hook resmi PreUpdateMovement menggantikan PostUpdateVelocity
        // Menginterupsi perhitungan gravitasi tModLoader sesaat sebelum frame render dipilih
        public override void PreUpdateMovement()
        {
            if (standingOnCloud)
            {
                Player.velocity.Y = 0f; // Sembuhkan jitter visual dan paksa mode Idle / Running biasa
                Player.fallStart = (int)(Player.position.Y / 16f);
            }
        }

        public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo)
        {
            if (blackoutTimer > 0)
            {
                float intensity = 1f;
                
                // [LOC] [VAL] AMBANG FADE OUT PLAYER (Mulai memudar di 30 tick / 0.5 detik terakhir)
                if (blackoutTimer < 30)
                {
                    intensity = blackoutTimer / 30f;
                }

                // Mengubah seluruh Cache data gambar player (Armor, Kulit, Rambut, Aksesoris) menjadi siluet hitam murni
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
            {
                blackoutTimer--;
            }
        }

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (blackoutTimer > 0)
            {
                float intensity = 1f;

                // [LOC] [VAL] AMBANG FADE OUT NPC (Mulai memudar di 30 tick / 0.5 detik terakhir)
                if (blackoutTimer < 30)
                {
                    intensity = blackoutTimer / 30f;
                }

                // Ambil data tekstur dan hitung titik tengah frame NPC
                Texture2D texture = TextureAssets.Npc[npc.type].Value;
                Vector2 drawOrigin = npc.frame.Size() / 2f;

                // Tentukan arah hadap sprite NPC (Flipping)
                SpriteEffects effects = SpriteEffects.None;
                if (npc.spriteDirection == 1)
                {
                    effects = SpriteEffects.FlipHorizontally;
                }

                // Hitung posisi rendering di layar (Termasuk kompensasi gfxOffY agar mulus saat menaiki block)
                Vector2 drawPos = npc.Center - screenPos;
                drawPos.Y += npc.gfxOffY;

                // Campur warna lingkungan saat ini dengan warna hitam pekat berdasarkan intensitas timer
                Color finalColor = Color.Lerp(drawColor, Color.Black, intensity);

                // Gambar ulang NPC dengan warna siluet hitam legam
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

                return false; // Mengembalikan false agar sprite bawaan yang berwarna tidak ikut digambar ulang
            }
            return true; // Kembali normal jika timer habis
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
                // [LOC] [VAL] PANDUAN BALANCING: LOCK FREKUENSI SPAWN ABSOLUT
                // Dengan memotong langsung variabel tanpa kondisi perbandingan 'if', kita memaksa game mengabaikan
                // efek Battle/Calming Potion, Water/Peace Candle, Town NPC, mod lain, dan slider Journey Mode.
                spawnRate = 140;  // Semakin kecil angkanya, semakin sering siklus spawn diperiksa
                maxSpawns = 6;    // Batas maksimal slot musuh yang aktif di layar pada biome ini
            }
        }
    }
}