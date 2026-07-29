using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    public class ReworkedEyezor : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Timer internal untuk mengatur cooldown tembakan EyeFire khusus Eyezor
        private int eyeFireTimer = 0;

        public override void SetStaticDefaults()
        {
            // --- SISTEM IMUNITAS DEBUFF EYEZOR ---
            // Menambahkan imunitas penuh terhadap Ichor dan Cursed Inferno (Curse Flame)
            NPCID.Sets.SpecificDebuffImmunity[NPCID.Eyezor][BuffID.Ichor] = true;
            NPCID.Sets.SpecificDebuffImmunity[NPCID.Eyezor][BuffID.CursedInferno] = true;
        }

        public override void PostAI(NPC npc)
        {
            // Pastikan hanya mengeksekusi kode ini pada Eyezor vanilla yang sedang aktif
            if (npc.type == NPCID.Eyezor && npc.active)
            {
                npc.TargetClosest(true);
                Player target = Main.player[npc.target];

                if (target != null && target.active && !target.dead)
                {
                    float distance = Vector2.Distance(npc.Center, target.Center);

                    // [LOC] [VAL] JANGKAUAN RADIUS DETEKSI (43.75 Tile * 16 Piksel = 700f)
                    // Catatan: Jika maksudmu adalah "Hanya menembak jika JAUHNYA minimal 43.75 tile", ubah '<=' menjadi '>='
                    if (distance <= 700f) 
                    {
                        eyeFireTimer++;

                        // [LOC] [VAL] COOLDOWN TEMBAKAN EYEFIRE (60 Ticks = 1 Detik)
                        if (eyeFireTimer >= 60)
                        {
                            eyeFireTimer = 0;

                            if (Main.netMode != NetmodeID.MultiplayerClient)
                            {
                                // Mengatur koordinat keluar peluru agar pas di area kepala Eyezor
                                Vector2 headPosition = npc.Top + new Vector2(npc.spriteDirection * 4f, 8f);
                                
                                // Sistem Aiming langsung: Mengunci dan menghitung arah lurus ke pusat tubuh Player
                                Vector2 shootDirection = target.Center - headPosition;
                                shootDirection.Normalize();

                                // [LOC] [VAL] KECEPATAN SEMBURAN PROYEKTIL EYEFIRE
                                float projectileSpeed = 9.5f; 
                                Vector2 velocity = shootDirection * projectileSpeed;

                                // [LOC] [VAL] BASE DAMAGE TEMBAKAN EYEFIRE EYEZOR
                                int damage = 40; 

                                int proj = Projectile.NewProjectile(
                                    npc.GetSource_FromAI(),
                                    headPosition,
                                    velocity,
                                    ProjectileID.EyeFire, // Menggunakan semburan api milik Spazmatism
                                    damage,
                                    1.5f,
                                    Main.myPlayer
                                );

                                if (proj < Main.maxProjectiles)
                                {
                                    Main.projectile[proj].hostile = true;
                                    Main.projectile[proj].friendly = false;
                                    Main.projectile[proj].netUpdate = true;
                                }
                            }
                        }
                    }
                    else
                    {
                        // Reset timer jika player keluar dari radius jangkauan
                        eyeFireTimer = 0; 
                    }
                }
            }
        }

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
        {
            if (npc.type == NPCID.Eyezor)
            {
                // --- SISTEM KETAHANAN BESAR (RESISTANCE) TERHADAP PROYEKTIL TERTENTU ---
                if (projectile.type == ProjectileID.GreenLaser ||
                    projectile.type == ProjectileID.MeteorShot ||
                    projectile.type == ProjectileID.PurpleLaser ||
                    projectile.type == ProjectileID.ChlorophyteBullet ||
                    projectile.type == ProjectileID.BulletHighVelocity)
                {
                    // [LOC] [VAL] MULTIPLIER KETAHANAN DAMAGE
                    // Nilai 0.15f berarti Eyezor hanya menerima 15% damage asli (Mengurangi/menahan 85% damage masuk)
                    modifiers.FinalDamage *= 0.15f; 
                }
            }
        }
    }
}