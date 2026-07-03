using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class SkullOfEdgeProj : ModProjectile
    {
        private int hitCount = 0;
        private const int TrailLength = 5; 
        private List<Vector2[]> oldWhipPointsCache = new List<Vector2[]>();

        public override void SetStaticDefaults() {
            ProjectileID.Sets.IsAWhip[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.DefaultToWhip();
            Projectile.WhipSettings.Segments = 19;
        }

        // --- METHOD AI BARU UNTUK DUST & GLOWING ---
        public override void AI() {
            // Ambil semua titik kontrol cambuk saat ini
            List<Vector2> list = new List<Vector2>();
            Projectile.FillWhipControlPoints(Projectile, list);

            for (int i = 0; i < list.Count; i++) {
                // 1. EFEK GLOWING: Memancarkan cahaya lampu ungu di sekitar cambuk
                // Parameter: Posisi, R (0.4), G (0.1), B (0.6)
                Lighting.AddLight(list[i], 0.4f, 0.1f, 0.6f);

                // 2. EFEK DUST UNGU: Memunculkan dust Shadowflame di sepanjang cambuk
                // Main.rand.NextBool(6) artinya peluang 1 dari 6 per frame agar dust tidak terlalu padat/lag
                if (Main.rand.NextBool(6)) {
                    Dust dust = Dust.NewDustDirect(list[i] - new Vector2(4, 4), 8, 8, DustID.Shadowflame, 0f, 0f, 100, default, 0.9f);
                    dust.velocity *= 0.2f; // Membuat dust melayang pelan di tempat
                    dust.noGravity = true; // Dust tidak jatuh ke bawah
                }
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            float damageMultiplier = 1f - (0.35f * hitCount);
            if (damageMultiplier < 0.1f) {
                damageMultiplier = 0.1f;
            }
            modifiers.SourceDamage *= damageMultiplier;
            hitCount++;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 240);
            target.AddBuff(ModContent.BuffType<Buff.SkullOfEdgeTag>(), 240);

            for (int i = 0; i < 8; i++) {
                Dust dust = Dust.NewDustDirect(target.position, target.width, target.height, DustID.Shadowflame, 0f, 0f, 100, default, 1.5f);
                dust.velocity *= 1.4f;
                dust.noGravity = true;
            }

            Main.player[Projectile.owner].MinionAttackTargetNPC = target.whoAmI;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;

            List<Vector2> currentWhipPoints = new List<Vector2>();
            Projectile.FillWhipControlPoints(Projectile, currentWhipPoints);

            Vector2[] pointsArray = currentWhipPoints.ToArray();
            oldWhipPointsCache.Add(pointsArray);
            if (oldWhipPointsCache.Count > TrailLength) {
                oldWhipPointsCache.RemoveAt(0);
            }

            // --- DRAW AFTERIMAGE SHADOW FLAME ---
            for (int j = 0; j < oldWhipPointsCache.Count; j++) {
                Vector2[] trailPoints = oldWhipPointsCache[j];
                float alphaMultiplier = (float)j / TrailLength;
                
                Color shadowFlameColor = new Color(150, 60, 240); // Warna ungu neon
                Color trailColor = shadowFlameColor * alphaMultiplier;
                trailColor.A = (byte)(255 * alphaMultiplier * 0.6f);

                for (int i = 0; i < trailPoints.Length - 1; i++) {
                    Vector2 pos = trailPoints[i];
                    Vector2 nextPos = trailPoints[i + 1];

                    Rectangle sourceRect = new Rectangle(20, 0, 20, 10);
                    if (i == 0) sourceRect.X = 0;
                    else if (i == trailPoints.Length - 2) sourceRect.X = 40;

                    Vector2 diff = nextPos - pos;
                    float rotation = diff.ToRotation();
                    Vector2 origin = new Vector2(0, 5);
                    Vector2 scale = new Vector2(diff.Length() / 20f, 1f);

                    Vector2 drawPos = pos - Main.screenPosition;
                    Main.EntitySpriteDraw(texture, drawPos, sourceRect, trailColor, rotation, origin, scale, SpriteEffects.None, 0);
                }
            }

            // --- DRAW CAMBUK UTAMA (DENGAN EFEK GLOW) ---
            for (int i = 0; i < currentWhipPoints.Count - 1; i++) {
                Vector2 pos = currentWhipPoints[i];
                Vector2 nextPos = currentWhipPoints[i + 1];

                Rectangle sourceRect = new Rectangle(20, 0, 20, 10);
                if (i == 0) sourceRect.X = 0;
                else if (i == currentWhipPoints.Count - 2) sourceRect.X = 40;

                Vector2 diff = nextPos - pos;
                float rotation = diff.ToRotation();
                
                // Mengambil warna asli lingkungan
                Color color = Lighting.GetColor((int)pos.X / 16, (int)pos.Y / 16);
                
                // Trik Glowing: Campurkan warna lingkungan dengan warna putih/terang (Color.White) sebesar 30%.
                // Ini membuat cambuknya tetap terlihat menyala terang meskipun di tempat yang gelap gulita.
                color = Color.Lerp(color, Color.White, 0.35f);

                Vector2 origin = new Vector2(0, 5);
                Vector2 scale = new Vector2(diff.Length() / 20f, 1f);

                Vector2 drawPos = pos - Main.screenPosition;
                Main.EntitySpriteDraw(texture, drawPos, sourceRect, color, rotation, origin, scale, SpriteEffects.None, 0);
            }

            return false; 
        }
    }
}