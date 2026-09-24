using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.IceWand
{
    // Sprite yang sama dengan IceBlockIndicator bisa dipakai lagi di sini,
    // simpan sebagai Projectiles/IceBlockProjectile.png
    // ai[0] = sisa delay sebelum mulai jatuh (dipakai biar jatuhnya bergantian)
    // ai[1] = index NPC target awal (referensi saja, tidak homing)
    public class IceBlockProjectile : ModProjectile
    {
        private const int TrailLength = 10;
        private List<Vector2> trail = new List<Vector2>();

        public override void SetDefaults()
        {
            Projectile.width = 28;
            Projectile.height = 24;
            // Sama kayak MaxScale di IceBlockIndicator.cs (0.6f) biar ukurannya nyambung mulus
            // pas transisi dari indicator ke blok yang jatuh (default-nya 1f = ukuran asli texture).
            Projectile.scale = 0.6f;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.ignoreWater = true;
        }

        public override void AI()
        {
            trail.Insert(0, Projectile.Center);
            if (trail.Count > TrailLength) trail.RemoveAt(trail.Count - 1);

            if (Projectile.ai[0] > 0)
            {
                // masih menunggu giliran jatuh, sedikit goyang biar tidak kaku
                Projectile.ai[0]--;
                Projectile.velocity = Vector2.Zero;
                Projectile.position.Y += (float)Math.Sin(Main.GameUpdateCount * 0.1f) * 0.15f;
                return;
            }

            if (Projectile.velocity == Vector2.Zero)
            {
                // baru mulai jatuh -> mainkan sound es sekali
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.2f, Volume = 0.9f }, Projectile.Center);
            }

            Projectile.velocity.Y += 0.35f; // gravitasi
            if (Projectile.velocity.Y > 16f) Projectile.velocity.Y = 16f;
            Projectile.rotation += 0.05f * Projectile.direction;

            if (Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.IceTorch, 0f, 0f, 100, default, 1f);
                d.velocity *= 0.2f;
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.Frozen, 300); // 5 detik membeku
            SoundEngine.PlaySound(SoundID.Item27 with { Pitch = -0.2f }, target.Center);

            for (int i = 0; i < 12; i++)
            {
                Dust d = Dust.NewDustDirect(target.position, target.width, target.height, DustID.IceTorch, 0f, 0f, 100, default, 1.5f);
                d.velocity = Main.rand.NextVector2Circular(3f, 3f);
                d.noGravity = true;
            }
        }

        public override void Kill(int timeLeft)
        {
            SoundEngine.PlaySound(SoundID.Shatter, Projectile.position);
            for (int i = 0; i < 10; i++)
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.IceTorch, 0f, 0f, 100, default, 1.3f);
                d.velocity = Main.rand.NextVector2Circular(4f, 4f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Main.instance.LoadProjectile(Projectile.type);
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() / 2f;

            // afterimage biru es mengikuti jejak jatuhnya
            for (int i = trail.Count - 1; i >= 0; i--)
            {
                float t = 1f - (i / (float)TrailLength);
                Color trailColor = new Color(120, 200, 255) * (t * 0.5f);
                Vector2 drawPos = trail[i] - Main.screenPosition;
                Main.EntitySpriteDraw(texture, drawPos, null, trailColor, Projectile.rotation, origin, Projectile.scale * (0.7f + 0.3f * t), SpriteEffects.None, 0);
            }

            Vector2 mainDrawPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(texture, mainDrawPos, null, lightColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            return false;
        }
    }
}