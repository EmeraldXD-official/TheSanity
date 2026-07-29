using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Terraria.Audio;

namespace TheSanity.Projectiles
{
    public class SardineProjectile : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.penetrate = -1; 
            Projectile.tileCollide = false;
            Projectile.timeLeft = 350; 
        }

        public override void AI()
        {
            int targetIndex = (int)Projectile.ai[0];
            if (targetIndex < 0 || targetIndex >= Main.maxNPCs)
            {
                Projectile.Kill();
                return;
            }

            NPC target = Main.npc[targetIndex];

            // =========================================================================
            // CODES ANTI-GOCOK LUNATIC CULTIST (SARDINE SIDE)
            // =========================================================================
            // Jika target terdeteksi sebagai klon, paksa sarden mencari tubuh utama Cultist yang asli
            if (target.type == NPCID.CultistBossClone)
            {
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    if (Main.npc[i].active && Main.npc[i].type == NPCID.CultistBoss)
                    {
                        Projectile.ai[0] = i; // Alihkan indeks memori proyektil
                        target = Main.npc[i]; // Ganti referensi target ke yang asli
                        break;
                    }
                }
            }

            // Tetap jaga segmentasi boss tipe cacing
            if (target.realLife >= 0 && target.realLife < Main.maxNPCs)
            {
                NPC induk = Main.npc[target.realLife];
                if (induk.active) target = induk;
            }
            // =========================================================================

            // STATUS A: Sarden sudah menempel
            if (Projectile.localAI[0] == 1f)
            {
                Projectile.friendly = false; 
                
                if (target.active)
                {
                    // Berteleportasi bersama boss! Karena posisi di-update setiap frame, 
                    // sejauh apa pun Cultist berteleportasi, sarden ikut terseret instan ke koordinat barunya.
                    Projectile.Center = target.Center;
                    Projectile.velocity = Vector2.Zero;
                }
                else
                {
                    PanggilMightyEagle(Projectile.Center, targetIndex);
                    Projectile.Kill();
                    return;
                }

                Projectile.localAI[1]++;
                if (Projectile.localAI[1] >= 120f)
                {
                    PanggilMightyEagle(target.Center, target.whoAmI);
                    Projectile.Kill(); 
                }
            }
            // STATUS B: Sarden sedang terbang memburu
            else
            {
                Projectile.rotation += 0.3f * Projectile.direction;

                if (target.active)
                {
                    Vector2 arahTujuan = target.Center - Projectile.Center;
                    float jarakKeTarget = arahTujuan.Length();
                    arahTujuan.Normalize();
                    
                    // Kecepatan pelacakan super agresif (35f) untuk menangkap pergerakan lincah boss
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, arahTujuan * 35f, 0.22f);

                    if (jarakKeTarget < 35f)
                    {
                        MenempelKeTarget(target);
                    }
                }

                if (Main.rand.NextBool(3))
                {
                    Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Water, Projectile.velocity.X * 0.2f, Projectile.velocity.Y * 0.2f);
                }
            }
        }

        private void MenempelKeTarget(NPC target)
        {
            if (Projectile.localAI[0] == 0f)
            {
                Projectile.localAI[0] = 1f; 
                target.AddBuff(ModContent.BuffType<Buff.SardineMarkBuff>(), 300);
            }
        }

        private void PanggilMightyEagle(Vector2 posisiMuncul, int targetIdx)
        {
            Player player = Main.player[Projectile.owner];
            int damageAsliElang = (int)Projectile.ai[1];

            Vector2 spawnPosition = posisiMuncul + new Vector2(1000f, -800f);
            Vector2 launchDirection = posisiMuncul - spawnPosition;
            launchDirection.Normalize();
            Vector2 initialVelocity = launchDirection * 15f;

            Projectile.NewProjectile(Projectile.GetSource_FromThis(), 
                spawnPosition, initialVelocity, ModContent.ProjectileType<MightyEagleProjectile>(), 
                damageAsliElang, 0f, Projectile.owner, targetIdx);

            SoundEngine.PlaySound(new SoundStyle("TheSanity/Sounds/MightyEagle"), player.Center);
            CombatText.NewText(player.getRect(), Color.Orange, "Mighty Eagle is coming!", dramatic: true);

            for (int i = 0; i < 15; i++)
            {
                Dust.NewDust(posisiMuncul - new Vector2(10, 10), 20, 20, DustID.Water, 0f, 0f, 0, default, 1.2f);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.FinalDamage *= 0;
            modifiers.DisableKnockback();
            modifiers.HideCombatText(); 
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            MenempelKeTarget(target);
        }
    }
}