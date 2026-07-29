using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class EolRainbowBolt : ModProjectile
    {
        public override string Texture => "TheSanity/Mecanic/WhiteOrb";

        public override void SetStaticDefaults()
        {
            // --- PANDUAN VISUAL: PANJANG EKOR UNICORN ---
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 45;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 32;  
            Projectile.height = 32;
            Projectile.aiStyle = -1; 
            Projectile.hostile = true;     
            Projectile.friendly = false;
            Projectile.tileCollide = false; 
            Projectile.timeLeft = 300;     
            Projectile.scale = 1.0f; 
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
        {
            behindNPCs.Add(index);
        }

        public override void AI()
        {
            int ownerNPCIndex = (int)Projectile.ai[0];

            if (ownerNPCIndex < 0 || ownerNPCIndex >= Main.maxNPCs || !Main.npc[ownerNPCIndex].active)
            {
                Projectile.Kill();
                return;
            }

            NPC owner = Main.npc[ownerNPCIndex];

            // --- REKUES FIX: KEEP ALIVE SAAT DIAM ---
            // Kita hapus kondisi 'if (owner.velocity.Length() > 1f)'.
            // Sekarang, selama Unicorn aktif (meski diam), proyektil ini waktunya dikunci terus di angka 10 agar TIDAK hancur.
            Projectile.timeLeft = 10; 

            // Atur posisi nempel di area punggung/tengah belakang Unicorn
            float offsetBehind = -25f * owner.spriteDirection; 
            Vector2 positionOffset = new Vector2(offsetBehind, -12f); 

            Projectile.Center = owner.Center + positionOffset;

            // Jika sedang diam (kecepatan 0), rotasi dikunci horizontal mengikuti arah hadap sprite Unicorn
            if (owner.velocity.Length() > 0.1f)
            {
                Projectile.rotation = owner.velocity.ToRotation();
            }
            else
            {
                Projectile.rotation = (owner.spriteDirection == 1) ? 0f : MathHelper.Pi;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                Rectangle trailHitbox = new Rectangle((int)Projectile.oldPos[i].X, (int)Projectile.oldPos[i].Y, Projectile.width, Projectile.height);
                if (trailHitbox.Intersects(targetHitbox))
                {
                    return true; 
                }
            }
            return base.Colliding(projHitbox, targetHitbox);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            target.AddBuff(BuffID.WitheredArmor, 120); 
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

            for (int k = Projectile.oldPos.Length - 1; k >= 0; k--)
            {
                if (Projectile.oldPos[k] == Vector2.Zero) continue;

                Vector2 drawPos = Projectile.oldPos[k] + (Projectile.Size * 0.5f) - Main.screenPosition;

                float opacity = 1f - ((float)k / Projectile.oldPos.Length);

                float colorOffset = (Main.GlobalTimeWrappedHourly * 2.5f) - (k * 0.03f);
                Color rainbowColor = Main.hslToRgb(colorOffset % 1f, 1f, 0.6f);
                
                Color finalTrailColor = rainbowColor * opacity * 0.8f; 
                finalTrailColor.A = 0; 

                float scale = Projectile.scale * opacity * 1.0f;

                Main.EntitySpriteDraw(
                    texture,
                    drawPos,
                    null,
                    finalTrailColor,
                    Projectile.rotation, 
                    drawOrigin,
                    scale,
                    SpriteEffects.None,
                    0
                );
            }

            return false; 
        }
    }
}