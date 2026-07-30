using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics; // Import Engine Grafis Luminance
using TheSanity.GlobalNPC.Bosses.Pluto.PlutoProjectile;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoPart
{
    public partial class PlutoHead
    {
        private void ExecuteTeleportDashPattern(Player player) {
            int stage = (int)NPC.ai[1];
            int timer = (int)NPC.ai[2];

            if (stage == 0) {
                NPC.velocity = Vector2.Zero;

                if (timer == 0) {
                    NPC.alpha = 255; // Menghilang total

                    // Pindah posisi acak berjarak 950px dari player
                    float randAngle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                    Vector2 teleportOffset = randAngle.ToRotationVector2() * 950f;
                    NPC.Center = player.Center + teleportOffset;

                    // Mengunci segmen tubuh secara instan agar tidak glitch memanjang
                    for (int i = 0; i < Main.maxNPCs; i++) {
                        NPC segment = Main.npc[i];
                        if (segment.active && segment.ai[3] == NPC.whoAmI && 
                           (segment.type == ModContent.NPCType<PlutoBody>() || segment.type == ModContent.NPCType<PlutoTail>())) {
                            segment.Center = NPC.Center;
                            segment.netUpdate = true;
                        }
                    }

                    // INTEGRASI PORTAL VISUAL BARU
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        // 1. Cari portal lama milik boss ini, ubah statenya ke 'Shrink & Fade Out' (ai[1] = 1)
                        for (int i = 0; i < Main.maxProjectiles; i++) {
                            Projectile p = Main.projectile[i];
                            if (p.active && p.type == ModContent.ProjectileType<PlutoPortal>() && p.ai[0] == NPC.whoAmI) {
                                p.ai[1] = 1f; // Memicu fase mengecil dan fade out
                                p.netUpdate = true;
                            }
                        }

                        // 2. Spawn Portal Baru di lokasi keluar teleport
                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            NPC.Center,
                            Vector2.Zero,
                            ModContent.ProjectileType<PlutoPortal>(),
                            0, // Hanya visual, tanpa damage
                            0f,
                            Main.myPlayer,
                            NPC.whoAmI // Menyimpan ID Pluto Head di ai[0] portal
                        );
                    }

                    NPC.netUpdate = true;
                }

                // Mengunci target prediktif secara dinamis selama proses Aiming
                Vector2 predictedPos = player.Center + player.velocity * 9.0f;
                Vector2 aimDirection = (predictedPos - NPC.Center).SafeNormalize(Vector2.Zero);
                if (aimDirection != Vector2.Zero) {
                    NPC.rotation = aimDirection.ToRotation();
                }

                // NOTE: Dust lama sengaja dihapus total karena sekarang sudah digantikan
                // sepenuhnya oleh Laser Bidikan Merah-Hitam dinamis yang di-draw di PlutoHead.cs (PreDraw)!

                timer++;
                if (timer >= 120) { // Tepat 2 Detik (120 Frame pada 60 FPS)
                    NPC.ai[1] = 1f; // Pindah ke fase Super Dash
                    NPC.ai[2] = 0f;
                    NPC.alpha = 0; // Muncul kembali secara instan (seluruh tubuh langsung terlihat lagi)

                    float distanceToPredicted = Vector2.Distance(NPC.Center, predictedPos);
                    float totalDashDistance = distanceToPredicted + 3500f;
                    float dashSpeed = 44f * 5f; // Kecepatan 5x Lipat (220f)

                    NPC.velocity = aimDirection * dashSpeed;
                    NPC.rotation = aimDirection.ToRotation();
                    dashDuration = totalDashDistance / dashSpeed;

                    if (dashDuration > 150f) dashDuration = 150f;

                    // LUMINANCE INTEGRATION: Memicu guncangan layar masif saat boss mulai meluncur
                    ScreenShakeSystem.StartShake(22f, 35, aimDirection);

                    int soundNum = Main.rand.Next(1, 3);
                    SoundEngine.PlaySound(new SoundStyle($"TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/Dash{soundNum}"), NPC.Center);

                    NPC.ai[3]++;
                    NPC.netUpdate = true;
                }
                else {
                    NPC.ai[2] = timer;
                }
            }
            else if (stage == 1) {
                NPC.rotation = NPC.velocity.ToRotation();

                // Memuntahkan PlutoMine 8 arah konstan setiap 2 frame sekali saat meluncur cepat
                if (Main.netMode != NetmodeID.MultiplayerClient && timer % 2 == 0) {
                    int mineSpreads = 8;
                    for (int i = 0; i < mineSpreads; i++) {
                        Vector2 mineVel = Vector2.UnitX.RotatedBy(MathHelper.TwoPi / mineSpreads * i) * 5.5f;
                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            NPC.Center,
                            mineVel,
                            ModContent.ProjectileType<PlutoMine>(),
                            NPC.damage / 4,
                            0f,
                            Main.myPlayer
                        );
                    }
                }

                timer++;
                if (timer >= (int)dashDuration) {
                    NPC.ai[2] = 0f;
                    if ((int)NPC.ai[3] >= maxDashes) {
                        NPC.ai[0] = 0f; 
                        NPC.ai[1] = 0f;
                        NPC.ai[3] = 0f;
                    } else {
                        NPC.ai[1] = 0f; // Reset kembali ke fase persiapan teleport berikutnya
                    }
                    NPC.netUpdate = true;
                }
                else {
                    NPC.ai[2] = timer;
                }
            }
        }
    }
}