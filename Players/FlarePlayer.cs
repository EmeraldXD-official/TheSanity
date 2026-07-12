using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio; 
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic; // Dibutuhkan untuk List Target
using Terraria.DataStructures;   // Dibutuhkan untuk PlayerDrawSet
using TheSanity.NPCs;            // Dibutuhkan untuk mendeteksi GlobalNPC sunMarked

namespace TheSanity.Players
{
    public class FlarePlayer : ModPlayer
    {
        // ==================== FIX INDEKS DOUBLE TAP VANILLA ====================
        private const int DashLeft = 2;  
        private const int DashRight = 3; 

        // ==================== VARIABLE UTAMA MEKANIK ====================
        // Properti Dash Vanilla Bawaan
        public bool isDashing = false;
        public int dashDelay = 0;
        public int dashTimer = 0;
        public int dashDirection = 0;
        private const int DashCooldownDuration = 50; 
        private const int DashActiveDuration = 18;   
        private const float DashVelocitySpeed = 14.5f; 

        // Properti Omnislash (Fase Malam - Thunderflare)
        public bool isOmnislashing = false;
        private NPC omnislashTarget = null;
        private int omnislashTimer = 0;
        private int strikeCount = 0;
        private const int MaxStrikes = 6; 

        // Properti Sun Dash (Fase Siang - Solar Lock-on)
        public bool isSunDashing = false;
        private List<int> sunDashIndices = new List<int>();
        private int currentDashTargetIndex = 0;
        private int sunDashTimer = 0;
        private Vector2 dashStartPos;
        private Vector2 dashEndPos;
        private const int DASH_DURATION = 4; // Durasi per tebasan dash (makin kecil makin supersonik!)

        // ==================== RESET EFFECTS ====================
        public override void ResetEffects() {
            if (dashTimer >= DashActiveDuration) {
                isDashing = false;
            }
        }

        public override void UpdateDead() {
            isDashing = false;
            dashTimer = 0;
            dashDelay = 0;
            isOmnislashing = false;
            omnislashTarget = null;
            
            // Reset status skill siang saat mati
            isSunDashing = false;
            sunDashIndices.Clear();
        }

        // ==================== MEKANIK INPUT DOUBLE-TAP DASH ====================
        public override void PreUpdateMovement() {
            // Mengunci pergerakan normal jika player sedang melakukan tebasan berantai (Omnislash atau Sun Dash)
            if (isSunDashing || isOmnislashing) {
                Player.velocity = Vector2.Zero;
                return;
            }

            if (dashDelay > 0)
                dashDelay--;

            if (dashTimer > 0)
                dashTimer--;

            if (dashDelay == 0 && dashTimer == 0 && !isOmnislashing && !Player.frozen) {
                if (Player.controlLeft && Player.releaseLeft && Player.doubleTapCardinalTimer[DashLeft] < 15) {
                    InitiateDash(-1);
                }
                else if (Player.controlRight && Player.releaseRight && Player.doubleTapCardinalTimer[DashRight] < 15) {
                    InitiateDash(1);
                }
            }

            if (isDashing && dashTimer > 0) {
                Player.velocity.X = dashDirection * DashVelocitySpeed;
                
                if (Player.velocity.Y > 0f) {
                    Player.velocity.Y *= 0.5f;
                }
                
                Player.immune = true;
                Player.immuneTime = Math.Max(Player.immuneTime, 2);
            }
            else if (isDashing && dashTimer == 0) {
                isDashing = false;
                dashDelay = DashCooldownDuration;
            }
        }

        private void InitiateDash(int direction) {
            isDashing = true;
            dashDirection = direction;
            dashTimer = DashActiveDuration;
            
            Player.velocity.Y = 0f; 
            Player.velocity.X = dashDirection * DashVelocitySpeed;

            SoundEngine.PlaySound(SoundID.Item74, Player.Center);
        }

        // ==================== MEKANIK OMNISLASH (MALAM) ====================
        public void StartOmnislash(NPC target) {
            if (isOmnislashing || target == null || !target.active) return;

            isOmnislashing = true;
            omnislashTarget = target;
            omnislashTimer = 0;
            strikeCount = 0;
        }

        private void ExecuteOmnislashLogic() {
            if (omnislashTarget == null || !omnislashTarget.active || strikeCount >= MaxStrikes) {
                isOmnislashing = false;
                omnislashTarget = null;
                return;
            }

            if (omnislashTimer % 5 == 0) {
                Player.velocity = Vector2.Zero;

                float randomAngle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                Vector2 offset = randomAngle.ToRotationVector2() * 90f;
                
                Player.Center = omnislashTarget.Center + offset;
                Player.direction = (omnislashTarget.Center.X > Player.Center.X) ? 1 : -1;

                // FIX 1.4.4: Kalkulasi HitInfo & Strike NPC yang legal secara API tModLoader
                int baseDamage = Player.GetWeaponDamage(Player.HeldItem);
                NPC.HitInfo hit = omnislashTarget.CalculateHitInfo(baseDamage, Player.direction, false, 5f, DamageClass.Melee);
                omnislashTarget.StrikeNPC(hit);

                // Sinkronisasi data ke multiplayer menggunakan SendData terstandar
                if (Main.netMode != NetmodeID.SinglePlayer) {
                    NetMessage.SendData(MessageID.DamageNPC, -1, -1, null, omnislashTarget.whoAmI, baseDamage, 5f, Player.direction, 0);
                }

                for (int i = 0; i < 12; i++) {
                    Vector2 dustVel = (omnislashTarget.Center - Player.Center).SafeNormalize(Vector2.Zero).RotatedByRandom(0.4f) * Main.rand.NextFloat(4f, 9f);
                    Dust d = Dust.NewDustPerfect(omnislashTarget.Center, DustID.Electric, dustVel, 100, default, 1.3f);
                    d.noGravity = true;
                }

                SoundEngine.PlaySound(SoundID.Item60, Player.Center); 
                strikeCount++;
            }

            omnislashTimer++;
        }

        // ==================== MEKANIK SUN DASH (SIANG) ====================
        public void StartSunDash(List<NPC> targets) {
            if (isSunDashing || targets.Count == 0) return;

            sunDashIndices.Clear();

            // Mengacak daftar musuh yang terkena mark (Multi-target Random Order)
            List<NPC> randomizedList = new List<NPC>(targets);
            int n = randomizedList.Count;
            while (n > 1) {
                n--;
                int k = Main.rand.Next(n + 1);
                NPC value = randomizedList[k];
                randomizedList[k] = randomizedList[n];
                randomizedList[n] = value;
            }

            foreach (var npc in randomizedList) {
                sunDashIndices.Add(npc.whoAmI);
            }

            if (sunDashIndices.Count > 0) {
                isSunDashing = true;
                currentDashTargetIndex = 0;
                PrepareNextSunDash();
            }
        }

        private void PrepareNextSunDash() {
            if (currentDashTargetIndex >= sunDashIndices.Count) {
                EndSunDash();
                return;
            }

            NPC target = Main.npc[sunDashIndices[currentDashTargetIndex]];
            if (!target.active || target.dontTakeDamage) {
                currentDashTargetIndex++;
                PrepareNextSunDash();
                return;
            }

            dashStartPos = Player.Center;
            dashEndPos = target.Center;
            sunDashTimer = 0;

            // Suara sring tebasan sonic berkecepatan tinggi
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.6f, Volume = 0.85f }, Player.Center);
        }

        private void ExecuteSunDashLogic() {
            // Kebal total dari damage & debuff selama menebas supersonik siang hari
            Player.immune = true;
            Player.immuneTime = Math.Max(Player.immuneTime, 2);
            Player.velocity = Vector2.Zero;

            if (currentDashTargetIndex >= sunDashIndices.Count) {
                EndSunDash();
                return;
            }

            NPC target = Main.npc[sunDashIndices[currentDashTargetIndex]];
            if (!target.active) {
                currentDashTargetIndex++;
                PrepareNextSunDash();
                return;
            }

            dashEndPos = target.Center; // Sinkronisasi berkala jika target bergerak cepat
            sunDashTimer++;

            float progress = (float)sunDashTimer / DASH_DURATION;
            Player.Center = Vector2.Lerp(dashStartPos, dashEndPos, progress);

            // EFEK AFTERIMAGE JALUR DASH: Semburan kilatan api emas murni di sepanjang jalur
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(Player.Center + Main.rand.NextVector2Circular(12f, 12f), DustID.GoldFlame, null, 100, Color.Yellow, 1.4f);
                d.noGravity = true;
            }

            // KETIKA PROGRESS SELESAI (SAMPAI DI LOKASI MUSUH)
            if (sunDashTimer >= DASH_DURATION) {
                Player.Center = dashEndPos;
                Player.direction = (target.Center.X > dashStartPos.X) ? 1 : -1;

                // Eksekusi damage masif (4.5x Lipat Weapon Damage)
                int baseDamage = Player.GetWeaponDamage(Player.HeldItem);
                int executionDamage = (int)(baseDamage * 4.5f);

                // FIX 1.4.4: Kalkulasi HitInfo & Strike NPC murni yang aman dan ter-sync otomatis
                NPC.HitInfo hitInfo = target.CalculateHitInfo(executionDamage, Player.direction, false, 6f, DamageClass.Melee);
                target.StrikeNPC(hitInfo);

                if (Main.netMode != NetmodeID.SinglePlayer) {
                    NetMessage.SendData(MessageID.DamageNPC, -1, -1, null, target.whoAmI, executionDamage, 6f, Player.direction, 0);
                }

                // Hilangkan mark tanda kunci target setelah dibantai
                target.GetGlobalNPC<FlareGlobalNPC>().sunMarked = false;

                // Suara ledakan tebasan solar
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 1f, Pitch = 0.2f }, target.Center);
                
                // Efek percikan burst api solar melingkar di tubuh target
                for (int i = 0; i < 18; i++) {
                    Dust d = Dust.NewDustPerfect(target.Center, DustID.SolarFlare, Main.rand.NextVector2CircularEdge(6f, 6f), 0, Color.Orange, 1.3f);
                    d.noGravity = true;
                }

                currentDashTargetIndex++;
                PrepareNextSunDash();
            }
        }

        private void EndSunDash() {
            isSunDashing = false;
            sunDashIndices.Clear();
        }

        // ==================== VISUAL POST UPDATE ====================
        public override void PostUpdate() {
            // Handler Pengecekan Skill Aktif
            if (isOmnislashing) {
                Player.velocity = Vector2.Zero; 
                Player.fallStart = (int)(Player.position.Y / 16f); 
                ExecuteOmnislashLogic();
            }
            else if (isSunDashing) {
                Player.velocity = Vector2.Zero;
                Player.fallStart = (int)(Player.position.Y / 16f);
                ExecuteSunDashLogic();
            }

            // DUST AFTERIMAGE DASH HITAM PEKAT (Bawaan Asli)
            if (isDashing && Math.Abs(Player.velocity.X) > 2f) {
                for (int i = 0; i < 3; i++) {
                    Dust d = Dust.NewDustDirect(
                        Player.position, 
                        Player.width, 
                        Player.height, 
                        DustID.Shadowflame, 
                        -Player.velocity.X * 0.2f, 
                        -Player.velocity.Y * 0.2f, 
                        150, 
                        Color.Black, 
                        1.7f
                    );
                    d.noGravity = true;
                    d.velocity *= 0.3f; 
                }

                if (Main.rand.NextBool(2)) {
                    int smoke = Dust.NewDust(Player.position, Player.width, Player.height, DustID.Smoke, 0f, 0f, 200, Color.Black, 1.2f);
                    Main.dust[smoke].noGravity = true;
                }
            }
        }

        // ==================== SYSTEM SHADOW DAN GLOWING RENDERING ====================
        public override void FrameEffects() {
            // FIX: Menggunakan properti resmi vanilla Terraria untuk menggambar afterimage dash
            if (isSunDashing) {
                Player.armorEffectDrawShadow = true;
            }
        }

        public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo) {
            // MEMBUAT PLAYER BERWARNA GLOWING KUNING EMAS PENUH SAAT SUN DASH
            if (isSunDashing) {
                Color goldGlow = Color.Yellow;
                drawInfo.colorArmorHead = goldGlow;
                drawInfo.colorArmorBody = goldGlow;
                drawInfo.colorArmorLegs = goldGlow;
                drawInfo.colorHair = goldGlow;
                drawInfo.colorBodySkin = goldGlow;
                drawInfo.colorShirt = goldGlow;
                drawInfo.colorPants = goldGlow;
                drawInfo.colorShoes = goldGlow;
            }
        }
    }
}