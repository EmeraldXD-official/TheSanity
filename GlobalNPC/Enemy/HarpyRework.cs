using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    // =========================================================================
    // [NPC REWORK SYSTEM]: HARPY 8-WAY BURST & BLOCK-PHASE MECHANIC (5 SECONDS)
    // =========================================================================
    public class HarpyRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public int harpyTimer = 0;
        
        // Timer khusus untuk menghitung durasi buff tembus block (dalam frame)
        public int phaseBuffTimer = 0; 

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.Harpy;
        }

        // =========================================================================
        // [ATTACK & PHASE CONTROL LOCATION]: TIMER 10 DETIK & TIMER BUFF 5 DETIK
        // =========================================================================
        public override void AI(NPC npc)
        {
            if (npc.type != NPCID.Harpy) return;

            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            // -------------------------------------------------------------------------
            // MEKANIK 1: PENGATURAN BUFF TEMBUS BLOCK (5 DETIK)
            // -------------------------------------------------------------------------
            if (phaseBuffTimer > 0)
            {
                phaseBuffTimer--;
                
                // Mengaktifkan fitur tembus block pada NPC
                npc.noTileCollide = true;

                // Visual effect: Munculkan partikel bulu putih mistis di sekeliling tubuhnya saat phase-shift aktif
                if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3))
                {
                    Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.WhiteTorch, 0f, 0f, 150, default, 1.1f);
                    d.velocity *= 0.2f;
                    d.noGravity = true;
                }

                // Jika durasi buff habis, kembalikan agar Harpy menabrak dinding lagi seperti biasa
                if (phaseBuffTimer <= 0)
                {
                    npc.noTileCollide = false;
                    npc.netUpdate = true;
                }
            }

            if (target.dead || !target.active)
            {
                harpyTimer = 0;
                return;
            }

            // -------------------------------------------------------------------------
            // MEKANIK 2: TEMBAKAN BULU 8 ARAH KONSISTEN (SETIAP 10 DETIK)
            // -------------------------------------------------------------------------
            harpyTimer++;

            // TRIGGER LOCATION: Tepat 600 Frame = 10 Detik Sekali!
            if (harpyTimer >= 600)
            {
                harpyTimer = 0; // Reset timer serangan

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // LOKASI BALANCING SPEED & DAMAGE BULU HARPY
                    float featherSpeed = 5.5f;
                    int featherDamage = 15;

                    // Menggunakan perhitungan sudut radians. Lingkaran penuh = 2 * PI. Dibagi 8 arah = PI / 4 per sudut.
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = i * (MathHelper.TwoPi / 8f);
                        Vector2 velocity = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * featherSpeed;

                        // Spawn proyektil bulu vanilla dengan parameter ai[0] diisi ID Harpy ini 
                        // agar proyektil tahu siapa induk pemiliknya jika berhasil hit player
                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            npc.Center,
                            velocity,
                            ProjectileID.HarpyFeather, // ID 38 (Bulu Harpy)
                            featherDamage,
                            1f,
                            Main.myPlayer,
                            npc.whoAmI // Kirim ID Harpy sebagai jangkar pengenal
                        );
                    }
                }

                // Efek suara kepakan sayap/bulu tajam meluncur
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item11, npc.Center);
                npc.netUpdate = true;
            }
        }

        // -------------------------------------------------------------------------
        // MEKANIK 3: TRIGGER BUFF JIKA PLAYER TERKENA HIT CONTACT (TABRAKAN BADAN)
        // -------------------------------------------------------------------------
        public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers)
        {
            if (npc.type == NPCID.Harpy)
            {
                // LOKASI DURASI BUFF CONTACT: 300 Frame = Tepat 5 Detik tembus dinding!
                phaseBuffTimer = 300; 
                npc.netUpdate = true;

                // Suara pekikan Harpy saat berhasil mencakar player
                Terraria.Audio.SoundEngine.PlaySound(SoundID.NPCDeath14, npc.Center);
            }
        }
    }

    // =========================================================================
    // [PROJECTILE ALTERATION SYSTEM]: DETEKSI HIT BULU UNTUK TRANSFER BUFF
    // =========================================================================
    public class HarpyFeatherHitModifier : GlobalProjectile
    {
        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation)
        {
            return entity.type == ProjectileID.HarpyFeather;
        }

        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info)
        {
            // Ambil data indeks NPC induk yang menembakkan bulu ini dari variabel ai[0]
            int ownerNPCIndex = (int)projectile.ai[0];

            // Validasi: Pastikan Harpy induknya masih hidup dan aktif di map
            if (ownerNPCIndex >= 0 && ownerNPCIndex < Main.maxNPCs)
            {
                NPC npc = Main.npc[ownerNPCIndex];
                if (npc.active && npc.type == NPCID.Harpy)
                {
                    // Ambil akses ke class global HarpyRework milik Harpy tersebut
                    if (npc.TryGetGlobalNPC<HarpyRework>(out var harpyGlobal))
                    {
                        // LOKASI DURASI BUFF PROJECTILE: Berikan 5 detik (300 frame) tembus block ke Harpy induknya!
                        harpyGlobal.phaseBuffTimer = 300;
                        npc.netUpdate = true;
                    }
                }
            }
        }
    }
}