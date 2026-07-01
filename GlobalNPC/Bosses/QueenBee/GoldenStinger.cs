using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;

namespace TheSanity.NPCs 
{
    public class GoldenStinger : ModNPC
    {
        // --- STATE MACHINE AI ---
        private const int STATE_APPROACH = 0;        // Terbang mendekat
        private const int STATE_TELEGRAPH = 1;       // Aba-aba Dash biasa
        private const int STATE_DASH = 2;            // Melesat Dash biasa
        private const int STATE_RAM_TELEGRAPH = 3;   // Aba-aba Ram Dash (Serudukan jauh)
        private const int STATE_RAM_DASH = 4;        // Melesat Ram Dash Super

        public override string Texture => "Terraria/Images/NPC_" + NPCID.Hornet;

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.Hornet];

            NPCID.Sets.TrailCacheLength[Type] = 10; 
            NPCID.Sets.TrailingMode[Type] = 3;     

            // =========================================================================
            // [GUIDE BALANCING: KEKEBALAN DEBUFF (IMMUNITY)]
            // - true  : NPC kebal total terhadap debuff tersebut
            // - false : NPC bisa terkena debuff tersebut (Bawaan normal)
            // =========================================================================
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Poisoned] = true; // Kebal Poison
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Venom] = true;    // Kebal Acid Venom
            // =========================================================================
        }

        public override void SetDefaults()
        {
            NPC.width = 34;
            NPC.height = 34;

            // =========================================================================
            // [LOC] [VAL] BALANCE STATUS DASAR NPC (NORMAL MODE)
            // =========================================================================
            NPC.damage = 35;                    // Damage dasar normal mode
            NPC.defense = 10;                   // Pertahanan / Armor NPC
            NPC.lifeMax = 1000;                 // Maksimal Darah NPC
            // =========================================================================

            NPC.knockBackResist = 0.0f;         // 100% Kebal Knockback (Imunitas Total)
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.value = 500f;
            NPC.noGravity = true;              
            NPC.noTileCollide = true;           // Menembus block / dinding         

            AnimationType = NPCID.Hornet; 
        }

        public override void AI()
        {
            NPC.TargetClosest(true);
            Player player = Main.player[NPC.target];

            // LOCK ANTI-DESPAWN SYSTEM
            if (!player.active || player.dead)
            {
                NPC.velocity.Y += 0.3f; 
                NPC.EncourageDespawn(10); 
                return;
            }
            else
            {
                NPC.timeLeft = 3600; 
            }

            // --- INITIALIZATION FIRST FRAME PAS SPAWN ---
            if (NPC.localAI[0] == 0f)
            {
                NPC.localAI[0] = 1f;
                // Mengacak cooldown awal stinger saat pertama keluar dari perisai lebah agar tidak menyerang barengan
                NPC.ai[2] = Main.rand.Next(30, 120); 
            }

            NPC.spriteDirection = NPC.velocity.X < 0 ? -1 : 1;
            NPC.rotation = NPC.velocity.X * 0.05f;

            float distanceToPlayer = Vector2.Distance(NPC.Center, player.Center);
            int state = (int)NPC.ai[0];

            // =========================================================================
            // [LOC] [VAL] DYNAMIC DAMAGE BALANCING BERDASARKAN TINGKAT KESULITAN WORLD
            // =========================================================================
            int currentBaseDamage = 35;  // Default Normal Mode
            int currentRamDamage = 75;   // Default Normal Mode

            if (Main.masterMode)
            {
                currentBaseDamage = 100; // Pas 100 Damage di Master Mode!
                currentRamDamage = 220;  // Ram Dash ditingkatkan secara fatal di Master Mode
            }
            else if (Main.expertMode)
            {
                currentBaseDamage = 70;  // Skala Expert Mode biasa
                currentRamDamage = 150; 
            }
            // =========================================================================

            if (state == STATE_APPROACH)
            {
                if (NPC.ai[2] > 0) NPC.ai[2]--; 

                Vector2 targetPos = player.Center;
                Vector2 moveDirection = targetPos - NPC.Center;
                moveDirection.Normalize();

                NPC.damage = currentBaseDamage; 

                // ---------------------------------------------------------------------
                // [GUIDE BALANCING: KECEPATAN TERBANG MENDEKAT]
                // - speed : Batas kecepatan mengejar player (Default: 6f)
                // - 0.04f : Sensitivitas belokan kemudi terbang (Lerp factor)
                // ---------------------------------------------------------------------
                float speed = 6f;                                                                                       
                NPC.velocity = Vector2.Lerp(NPC.velocity, moveDirection * speed, 0.04f); 

                // =====================================================================
                // [MECHANIC BARU: ANTI-CLUMPING SEPARATION PER ENTITY]
                // Memindai stinger lain di arena, jika terlalu dekat akan saling mendorong
                // =====================================================================
                Vector2 separationForce = Vector2.Zero;
                int overlappingCount = 0;

                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC other = Main.npc[i];
                    // Cek jika entity yang dipindai adalah sesama GoldenStinger aktif dan bukan dirinya sendiri
                    if (other.active && other.type == NPC.type && i != NPC.whoAmI)
                    {
                        float distanceToStinger = Vector2.Distance(NPC.Center, other.Center);
                        // -------------------------------------------------------------
                        // [GUIDE BALANCING: JARAK AMAN ANTI-DEMPET]
                        // - 56f : Jarak radius minimal dalam piksel (kira-kira 3.5 Block). 
                        // Jika jarak antar stinger di bawah nilai ini, gaya dorong aktif.
                        // -------------------------------------------------------------
                        float safeRadius = 56f; 

                        if (distanceToStinger < safeRadius)
                        {
                            Vector2 pushDirection = NPC.Center - other.Center;
                            if (pushDirection == Vector2.Zero) // Pengaman crash posisi bertumpuk mutlak
                            {
                                pushDirection = new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 1f));
                            }
                            pushDirection.Normalize();

                            // Efek dorongan semakin kuat jika posisi antar entity semakin rapat
                            separationForce += pushDirection * (1f - (distanceToStinger / safeRadius));
                            overlappingCount++;
                        }
                    }
                }

                if (overlappingCount > 0)
                {
                    separationForce /= overlappingCount;
                    // -------------------------------------------------------------
                    // [GUIDE BALANCING: KEKUATAN DORONGAN PEMISAH]
                    // - 0.25f : Nilai kekuatan geser posisi minion. Naikkan nilainya 
                    // jika stinger dirasa masih terlalu sering menempel erat saat bergerak.
                    // -------------------------------------------------------------
                    NPC.velocity += separationForce * 0.25f;
                }
                // =====================================================================

                if (NPC.ai[2] <= 0) 
                {
                    // BALANCE JARAK PEMICU RAM DASH (60 - 70 Block)
                    if (distanceToPlayer >= 960f && distanceToPlayer <= 1120f)
                    {
                        NPC.ai[0] = STATE_RAM_TELEGRAPH;
                        NPC.ai[1] = 0;
                        NPC.netUpdate = true;
                    }
                    // BALANCE JARAK PEMICU DASH BIASA (50 Block)
                    else if (distanceToPlayer < 800f)
                    {
                        NPC.ai[0] = STATE_TELEGRAPH;
                        NPC.ai[1] = 0; 
                        NPC.netUpdate = true;
                    }
                }
            }
            else if (state == STATE_TELEGRAPH)
            {
                NPC.ai[1]++; 
                Vector2 moveDirection = player.Center - NPC.Center;
                moveDirection.Normalize();
                
                Vector2 wobble = new Vector2(0f, (float)Math.Sin(NPC.ai[1] * 0.2f) * 2f).RotatedBy(moveDirection.ToRotation());
                float telegraphSpeed = 3.5f; 
                NPC.velocity = Vector2.Lerp(NPC.velocity, moveDirection * telegraphSpeed + wobble, 0.05f);

                if (NPC.ai[1] >= 40)
                {
                    Vector2 dashVel = player.Center - NPC.Center;
                    dashVel.Normalize();

                    NPC.velocity = dashVel * 16f; 

                    NPC.ai[0] = STATE_DASH;
                    NPC.ai[1] = 0; 
                    NPC.netUpdate = true;
                    SoundEngine.PlaySound(SoundID.Item42, NPC.Center);
                }
            }
            else if (state == STATE_DASH)
            {
                NPC.ai[1]++;
                if (NPC.ai[1] >= 20) NPC.velocity *= 0.90f; 

                if (NPC.ai[1] >= 30) 
                {
                    NPC.ai[0] = STATE_APPROACH;
                    NPC.ai[1] = 0;

                    // =====================================================================
                    // [MECHANIC BARU: DYNAMIC ASYNC TIMING COOLDOWN DASH BIASA]
                    // - 80                   : Jeda internal asli bawaan code lama.
                    // - 120                  : Tambahan flat 2 detik murni (1 Detik = 60 Frame).
                    // - Main.rand.Next(0,91) : Pengacak tambahan waktu 0 sampai 1.5 detik (per Entity).
                    // =====================================================================
                    NPC.ai[2] = 80 + 120 + Main.rand.Next(0, 91); 

                    NPC.netUpdate = true;
                }
            }
            else if (state == STATE_RAM_TELEGRAPH)
            {
                NPC.ai[1]++;
                Vector2 moveDirection = player.Center - NPC.Center;
                moveDirection.Normalize();

                Vector2 wobble = new Vector2(0f, (float)Math.Sin(NPC.ai[1] * 0.4f) * 4f).RotatedBy(moveDirection.ToRotation());
                float ramTelegraphSpeed = 1.5f;
                NPC.velocity = Vector2.Lerp(NPC.velocity, moveDirection * ramTelegraphSpeed + wobble, 0.06f);

                if (NPC.ai[1] >= 55)
                {
                    Vector2 ramVel = player.Center - NPC.Center;
                    ramVel.Normalize();

                    NPC.damage = currentRamDamage; 
                    NPC.velocity = ramVel * 26f; 

                    NPC.ai[0] = STATE_RAM_DASH;
                    NPC.ai[1] = 0;
                    NPC.netUpdate = true;

                    SoundEngine.PlaySound(SoundID.Roar, NPC.Center); 
                }
            }
            else if (state == STATE_RAM_DASH)
            {
                NPC.ai[1]++;

                if (NPC.ai[1] >= 50) 
                {
                    NPC.velocity *= 0.85f; 
                }

                if (NPC.ai[1] >= 65) 
                {
                    NPC.ai[0] = STATE_APPROACH;
                    NPC.ai[1] = 0;

                    // =====================================================================
                    // [MECHANIC BARU: DYNAMIC ASYNC TIMING COOLDOWN RAM DASH SUPER]
                    // - 120                   : Jeda internal ram dash asli bawaan code lama.
                    // - 120                   : Tambahan flat 2 detik murni (1 Detik = 60 Frame).
                    // - Main.rand.Next(0,121) : Pengacak tambahan waktu 0 sampai 2 detik (per Entity).
                    // =====================================================================
                    NPC.ai[2] = 120 + 120 + Main.rand.Next(0, 121); 

                    NPC.netUpdate = true;
                }
            }
        }

        public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers)
        {
            if (target.statDefense >= 100)
            {
                float persenPenetrasi = 0.30f; // 0.30f = 30% Penetrasi Armor
                float armorPenetrationValue = target.statDefense * persenPenetrasi;
                
                modifiers.ArmorPenetration += armorPenetrationValue;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D texture = TextureAssets.Npc[NPCID.Hornet].Value;
            Rectangle drawFrame = NPC.frame;
            Vector2 origin = drawFrame.Size() / 2f;

            SpriteEffects effects = NPC.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 drawPos = NPC.Center - screenPos + new Vector2(0f, NPC.gfxOffY);

            Color sharedGlowColor = new Color(255, 220, 0, 255);

            // 1. SHADOW TRAIL
            for (int i = 1; i < NPC.oldPos.Length; i++)
            {
                if (NPC.oldPos[i] == Vector2.Zero) 
                    continue;

                Vector2 trailDrawPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos + new Vector2(0f, NPC.gfxOffY);
                float trailOpacity = (NPC.oldPos.Length - i) / (float)NPC.oldPos.Length;

                Color shadowColor = sharedGlowColor * trailOpacity * 0.85f;
                spriteBatch.Draw(texture, trailDrawPos, drawFrame, shadowColor, NPC.oldRot[i], origin, NPC.scale, effects, 0);
            }

            // 2. OUTLINE GLOW KUNING TEBAL
            float ketebalanGlowPiksel = 3.5f;
            for (int k = 0; k < 8; k++)
            {
                Vector2 offset = new Vector2(ketebalanGlowPiksel, 0f).RotatedBy(k * MathHelper.TwoPi / 8f);
                spriteBatch.Draw(texture, drawPos + offset, drawFrame, sharedGlowColor, NPC.rotation, origin, NPC.scale, effects, 0);
            }

            // 3. NPC UTAMA
            spriteBatch.Draw(texture, drawPos, drawFrame, NPC.GetAlpha(drawColor), NPC.rotation, origin, NPC.scale, effects, 0);

            return false; 
        }
    }
}