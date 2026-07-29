using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Buff;
using Terraria.Audio;

namespace TheSanity.GlobalNPC.Bosses.GemGuardian
{
    public class DiamondBossProjectile : ModProjectile
    {
        // Memakai sprite dasar Diamond dari game bawaan secara terprogram
        public override string Texture => "Terraria/Images/Item_" + ItemID.Diamond;

        public bool IsOrbitSpiral => Projectile.ai[0] == 1f;
        public bool IsIceShatter => Projectile.ai[0] == 2f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 0;
            ProjectileID.Sets.TrailCacheLength[Type] = 12; 
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;   
            Projectile.friendly = false; 
            Projectile.penetrate = -1;
            Projectile.tileCollide = false; 
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300; 
        }

        public override void AI() {
            // Rotasi berputar kristal es konstan
            Projectile.rotation += 0.15f;

            if (IsOrbitSpiral) {
                // AI Serangan 1: Mengatur sudut putaran dan radius yang makin melebar seiring waktu
                Projectile.ai[1] += 0.04f; // Kecepatan sudut orbit
                float radius = 20f + (300f - Projectile.timeLeft) * 2.2f; // Radius bertambah lebar
                
                // Cari index pusat Diamond Boss
                int bossType = ModContent.NPCType<DiamondBoss>();
                int bossIndex = NPC.FindFirstNPC(bossType);
                if (bossIndex != -1 && Main.npc[bossIndex].active) {
                    Vector2 center = Main.npc[bossIndex].Center;
                    Vector2 newOffset = Projectile.ai[1].ToRotationVector2() * radius;
                    Projectile.Center = center + newOffset;
                }
            }
            else if (IsIceShatter) {
                // AI Serangan 8: Mendeteksi jarak dekat player untuk memicu ledakan cincin salib
                Player target = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
                if (Vector2.Distance(Projectile.Center, target.Center) < 130f || Projectile.timeLeft == 220) {
                    SoundEngine.PlaySound(SoundID.Item27, Projectile.Center);
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        int shards = 12;
                        for (int i = 0; i < shards; i++) {
                            Vector2 shardVel = (MathHelper.TwoPi / shards * i).ToRotationVector2() * 6.5f;
                            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, shardVel, ModContent.ProjectileType<DiamondBossProjectile>(), Projectile.damage, 1f, Main.myPlayer);
                        }
                    }
                    Projectile.Kill(); // Hancur setelah meledak
                }
            }

            // Efek kilau partikel Diamond putih bercahaya di ekor proyektil
            if (Main.rand.NextBool(5)) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GemDiamond, 0f, 0f, 150, Color.White, 0.8f);
                d.noGravity = true;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            // Memberikan debuff Emerald Spell secara konstan berantai jika terkena serangannya
            int spellBuff = ModContent.BuffType<EmeraldSpell>();
            int existingIndex = target.FindBuffIndex(spellBuff);

            if (existingIndex != -1) {
                target.buffTime[existingIndex] += 60; // Akumulasi durasi
            } else {
                target.AddBuff(spellBuff, 240); // 4 Detik awal mulanya
            }
        }

        // Kustomisasi Efek Render: Menghasilkan bayangan transparan (Shadow Outline) putih bersih
        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() / 2f;
            Color whiteIceGlow = new Color(235, 245, 255, 180); 

            // Menggambar rentetan ekor bayangan (Glow Trails)
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                Vector2 trailDrawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = whiteIceGlow * ((Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length) * 0.4f;
                Main.spriteBatch.Draw(texture, trailDrawPos, null, trailColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);
            }

            // Gambar Shadow Outline Keliling (Warna putih mengkilat tipis)
            float outlineThickness = 3f;
            for (int i = 0; i < 4; i++) {
                Vector2 offset = new Vector2(outlineThickness, 0f).RotatedBy(MathHelper.PiOver2 * i);
                Main.spriteBatch.Draw(texture, Projectile.Center - Main.screenPosition + offset, null, whiteIceGlow * 0.6f, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);
            }

            // Gambar Inti Peluru Utama
            Vector2 mainDrawPos = Projectile.Center - Main.screenPosition;
            Main.spriteBatch.Draw(texture, mainDrawPos, null, Color.White, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

            return false; 
        }
    }
}