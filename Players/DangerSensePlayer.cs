using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio; 
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Players
{
    public class DangerSensePlayer : ModPlayer
    {
        // === State Mekanik Utama Aksesoris ===
        public bool dangerSenseEquipped = false;
        public int charges = 3;
        public int cooldownTimer = 0;
        public bool isTimeSlowed = false; 

        // === State Timer ===
        public int alertAnimationTimer = 0;
        public int activeTimer = 0; 

        // Menyimpan posisi titik pelarian berbentuk lingkaran
        public List<Vector2> escapePoints = new List<Vector2>();
        private bool oldMouseLeft = false;

        public override void ResetEffects()
        {
            dangerSenseEquipped = false;
        }

        public override void PreUpdate()
        {
            if (isTimeSlowed)
            {
                // Mekanik timer mundur visual (5 detik)
                if (activeTimer > 0 && activeTimer % 60 == 0)
                {
                    int displaySeconds = activeTimer / 60;
                    CombatText.NewText(Player.getRect(), Color.Red, displaySeconds.ToString(), true);
                }

                activeTimer--;
                
                if (activeTimer <= 0)
                {
                    isTimeSlowed = false; 
                    charges--; 

                    CombatText.NewText(Player.getRect(), Color.Yellow, "Dodged! Charge Wasted.", false);
                    
                    if (charges <= 0)
                    {
                        cooldownTimer = 1800; 
                        CombatText.NewText(Player.getRect(), Color.Orange, "Cooldown!", true);
                    }
                    return;
                }

                // Kunci kontrol pergerakan fisik player
                Player.velocity = Vector2.Zero;
                Player.position = Player.oldPosition; 

                // Matikan input keyboard
                Player.controlLeft = false;
                Player.controlRight = false;
                Player.controlUp = false;
                Player.controlDown = false;
                Player.controlJump = false;
                Player.controlUseItem = false; 
                Player.controlUseTile = false;
                Player.controlThrow = false;
                Player.controlHook = false;    
                Player.controlMount = false;   

                Player.itemAnimation = 0;
                Player.itemTime = 0;
            }
        }

        public override void PostUpdateEquips()
        {
            if (!dangerSenseEquipped)
            {
                isTimeSlowed = false;
                alertAnimationTimer = 0;
                activeTimer = 0;
                return;
            }

            // --- Logika Cooldown ---
            if (cooldownTimer > 0)
            {
                cooldownTimer--;
                if (cooldownTimer == 0)
                {
                    charges = 3; 
                    CombatText.NewText(Player.getRect(), Color.LightGreen, "Danger Sense Ready!", true);
                }
            }

            // --- Deteksi Jika Ada Boss Mendekat ---
            if (!isTimeSlowed && charges > 0 && cooldownTimer == 0)
            {
                NPC closestBoss = FindNearbyBoss(160f);
                if (closestBoss != null)
                {
                    TriggerDangerSense(closestBoss);
                }
            }

            // --- Logika Jagat Raya Saat Waktu Berhenti ---
            if (isTimeSlowed)
            {
                ApplyTimeStop(); 
                HandlePointSelection(); 

                if (alertAnimationTimer < 30)
                {
                    alertAnimationTimer++;
                }
            }
            else
            {
                alertAnimationTimer = 0; 
            }

            oldMouseLeft = Main.mouseLeft;
        }

        private NPC FindNearbyBoss(float range)
        {
            foreach (NPC npc in Main.npc)
            {
                if (npc.active && npc.boss && Vector2.Distance(Player.Center, npc.Center) <= range)
                {
                    return npc;
                }
            }
            return null;
        }

        private void TriggerDangerSense(NPC boss)
        {
            isTimeSlowed = true;
            escapePoints.Clear();
            alertAnimationTimer = 1; 
            activeTimer = 300; 

            SoundEngine.PlaySound(new SoundStyle("TheSanity/Sounds/DangerActive") { Volume = 1.1f, Pitch = 0.0f }, Player.Center);
            
            CombatText.NewText(Player.getRect(), Color.Cyan, "TIME STOP!", true);

            int totalPoints = 16;   
            
            // =========================================================================
            // PERBAIKAN UTAMA: Mengecilkan lebar radius lingkaran (dari 960f ke 350f)
            // =========================================================================
            float distance = 350f;  

            for (int i = 0; i < totalPoints; i++)
            {
                float angle = MathHelper.TwoPi / totalPoints * i;
                Vector2 point = Player.Center + angle.ToRotationVector2() * distance;
                escapePoints.Add(point);
            }
        }

        private void ApplyTimeStop()
        {
            foreach (NPC npc in Main.npc)
            {
                if (npc.active) npc.position -= npc.velocity;
            }

            foreach (Projectile proj in Main.projectile)
            {
                if (proj.active) proj.position -= proj.velocity;
            }
        }

        private void HandlePointSelection()
        {
            if (Main.mouseLeft && !oldMouseLeft)
            {
                Vector2 mouseWorld = Main.MouseWorld;

                for (int i = 0; i < escapePoints.Count; i++)
                {
                    if (Vector2.Distance(mouseWorld, escapePoints[i]) < 35f)
                    {
                        Player.Teleport(escapePoints[i] - new Vector2(Player.width / 2, Player.height / 2), TeleportationStyleID.RodOfDiscord);
                        NetMessage.SendData(MessageID.TeleportEntity, -1, -1, null, 0, Player.whoAmI, escapePoints[i].X, escapePoints[i].Y, 1);
                        
                        charges--;
                        isTimeSlowed = false; 

                        if (charges <= 0)
                        {
                            cooldownTimer = 1800; 
                            CombatText.NewText(Player.getRect(), Color.Orange, "Cooldown!", true);
                        }
                        break;
                    }
                }
            }
        }

        public static bool IsTimeStopped()
        {
            foreach (Player p in Main.player)
            {
                if (p.active && p.GetModPlayer<DangerSensePlayer>().isTimeSlowed)
                {
                    return true;
                }
            }
            return false;
        }
    }

    // =========================================================================
    // GLOBAL CLASS UNTUK MEMATIKAN AI BOSS
    // =========================================================================
    public class DangerSenseGlobalNPC : Terraria.ModLoader.GlobalNPC
    {
        public override bool PreAI(NPC npc)
        {
            if (DangerSensePlayer.IsTimeStopped())
            {
                return false; 
            }
            return true; 
        }
    }

    public class DangerSenseGlobalProjectile : Terraria.ModLoader.GlobalProjectile
    {
        public override bool PreAI(Projectile projectile)
        {
            if (DangerSensePlayer.IsTimeStopped())
            {
                if (projectile.minion || projectile.sentry || Main.projPet[projectile.type] || ProjectileID.Sets.MinionShot[projectile.type])
                {
                    return true; 
                }
                return false; 
            }
            return true;
        }
    }
}