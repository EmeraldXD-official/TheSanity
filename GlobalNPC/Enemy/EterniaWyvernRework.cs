using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;

namespace TheSanity
{
    // =========================================================================
    // [NPC GLOBAL REWORK]: ETERNIA WYVERN TIER 1, 2, & 3 REWORK
    // =========================================================================
    public class EterniaWyvernRework : global::Terraria.ModLoader.GlobalNPC
    {
        // Wajib diaktifkan agar setiap Wyvern punya timer cooldown ledakannya masing-masing
        public override bool InstancePerEntity => true;

        public int explosionCooldown = 0;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.DD2WyvernT1 || 
                   entity.type == NPCID.DD2WyvernT2 || 
                   entity.type == NPCID.DD2WyvernT3;
        }

        public override void SetDefaults(NPC npc)
        {
            // =========================================================================
            // [GUIDE & BALANCING LOKASI: KNOCKBACK RESISTANCE (KETAHANAN DI-PENTAL)]
            // =========================================================================
            if (npc.type == NPCID.DD2WyvernT3)
            {
                // TIER 3: Imun 90% Knockback (1.0f = 0%, 0.1f = 90% Imun)
                npc.knockBackResist = 0.1f; 
            }
        }

        public override void PostAI(NPC npc)
        {
            // Kurangi cooldown setiap frame
            if (explosionCooldown > 0) explosionCooldown--;

            // =========================================================================
            // [GUIDE & BALANCING LOKASI: DASH SPEED THRESHOLD]
            // =========================================================================
            // Angka kecepatan minimum untuk dianggap sedang "Dashing" (meluncur/menukik).
            // Normalnya saat Wyvern melayang diam, kecepatannya di bawah 2.0f.
            float dashSpeedThreshold = 6.0f;
            bool isDashing = npc.velocity.Length() > dashSpeedThreshold;
            // =========================================================================

            if (isDashing)
            {
                // 1. MEMUNCULKAN PARTIKEL SAAT DASHING
                // Munculkan partikel api/debu di sepanjang tubuhnya
                if (Main.rand.NextBool(3)) // Chance agar tidak terlalu lebat dan lag
                {
                    int dust = Dust.NewDust(npc.position, npc.width, npc.height, DustID.Torch, -npc.velocity.X * 0.5f, -npc.velocity.Y * 0.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].scale = 1.5f;
                }

                // 2. DETEKSI TABRAKAN (PROJECTILE FRIENDLY & TOWN NPC) UNTUK LEDAKAN
                if (explosionCooldown <= 0)
                {
                    bool triggerExplosion = false;

                    // Cek tabrakan dengan proyektil milik player (Peluru, Magic, dsb)
                    foreach (Projectile proj in Main.projectile)
                    {
                        if (proj.active && proj.friendly && proj.Hitbox.Intersects(npc.Hitbox))
                        {
                            triggerExplosion = true;
                            break; // Stop scanning jika sudah kena 1
                        }
                    }

                    // Cek tabrakan dengan NPC ramah (Town NPC)
                    if (!triggerExplosion)
                    {
                        foreach (NPC townNpc in Main.npc)
                        {
                            if (townNpc.active && townNpc.friendly && townNpc.Hitbox.Intersects(npc.Hitbox))
                            {
                                triggerExplosion = true;
                                break;
                            }
                        }
                    }

                    // Jika menyentuh salah satunya saat Dashing, picu ledakan!
                    if (triggerExplosion)
                    {
                        TriggerExplosion(npc);
                    }
                }
            }
        }

        // 3. DETEKSI TABRAKAN LANGSUNG DENGAN PLAYER
        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo)
        {
            float dashSpeedThreshold = 6.0f;
            if (npc.velocity.Length() > dashSpeedThreshold && explosionCooldown <= 0)
            {
                TriggerExplosion(npc);
            }
        }

        // =========================================================================
        // FUNGSI UTAMA: EKSEKUSI LEDAKAN AREA (AOE)
        // =========================================================================
        private void TriggerExplosion(NPC npc)
        {
            // =========================================================================
            // [GUIDE & BALANCING LOKASI: STATISTIK LEDAKAN PER TIER]
            // =========================================================================
            // Cooldown ledakan dalam bentuk Ticks (60 = 1 detik). 
            explosionCooldown = 60; 

            int explosionDamage = 100;    // Damage T1 (Master Mode standard)
            float explosionRadius = 120f; // Jarak jangkauan ledakan T1
            int dustAmount = 30;          // Kepadatan partikel efek visual

            if (npc.type == NPCID.DD2WyvernT2)
            {
                explosionDamage = 150;  // Damage T2
                explosionRadius = 180f; // Efek ledakan T2 lebih luas
                dustAmount = 50;
            }
            else if (npc.type == NPCID.DD2WyvernT3)
            {
                explosionDamage = 200;  // Damage T3
                explosionRadius = 180f; // Luas ledakan sama dengan T2
                dustAmount = 50;
            }
            // =========================================================================

            // A. VISUAL & AUDIO LEDAKAN
            SoundEngine.PlaySound(SoundID.Item14, npc.Center); // Suara bom meledak

            for (int i = 0; i < dustAmount; i++)
            {
                // Asap tebal
                Dust.NewDust(npc.position, npc.width, npc.height, DustID.Smoke, Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-5f, 5f), 100, default, 1.5f);
                // Api meledak ke segala arah
                Dust fireDust = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Torch, Main.rand.NextFloat(-8f, 8f), Main.rand.NextFloat(-8f, 8f), 100, default, 2f);
                fireDust.noGravity = true;
            }

            // B. MEMBERIKAN DAMAGE KE PLAYER (Sistem Anti-Lag/Bug Multiplayer)
            // Mengecek jarak (Radius Area) dan memastikan hanya melukai player lokal
            Player localPlayer = Main.LocalPlayer;
            if (localPlayer.active && !localPlayer.dead && localPlayer.Distance(npc.Center) <= explosionRadius)
            {
                localPlayer.Hurt(PlayerDeathReason.ByNPC(npc.whoAmI), explosionDamage, 0);
            }

            // C. MEMBERIKAN DAMAGE KE TOWN NPC DI SEKITAR
            // Hanya biarkan Server (atau Singleplayer) yang memproses darah NPC
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                foreach (NPC townNpc in Main.npc)
                {
                    if (townNpc.active && townNpc.friendly && townNpc.Distance(npc.Center) <= explosionRadius)
                    {
                        townNpc.SimpleStrikeNPC(explosionDamage, 0);
                    }
                }
            }
        }
    }
}