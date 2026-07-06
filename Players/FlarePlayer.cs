using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio; 
using Microsoft.Xna.Framework;
using System;

namespace TheSanity.Players
{
    public class FlarePlayer : ModPlayer
    {
        // ==================== FIX INDEKS DOUBLE TAP VANILLA ====================
        private const int DashLeft = 2;  
        private const int DashRight = 3; 

        // ==================== VARIABLE UTAMA MEKANIK ====================
        // Properti Dash
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
        }

        // ==================== MEKANIK INPUT DOUBLE-TAP DASH ====================
        public override void PreUpdateMovement() {
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

        // ==================== MEKANIK OMNISLASH ====================
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

                int baseDamage = Player.GetWeaponDamage(Player.HeldItem);
                NPC.HitInfo hit = omnislashTarget.CalculateHitInfo(baseDamage, Player.direction, false, 5f);
                omnislashTarget.StrikeNPC(hit);

                // FIX: Mengubah NetID menjadi NetmodeID yang benar bawaan Terraria
                if (Main.netMode != NetmodeID.SinglePlayer) {
                    NetMessage.SendStrikeNPC(omnislashTarget, hit);
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

        // ==================== VISUAL POST UPDATE ====================
        public override void PostUpdate() {
            if (isOmnislashing) {
                Player.velocity = Vector2.Zero; 
                Player.fallStart = (int)(Player.position.Y / 16f); 
                ExecuteOmnislashLogic();
            }

            // DUST AFTERIMAGE DASH HITAM PEKAT
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
    }
}