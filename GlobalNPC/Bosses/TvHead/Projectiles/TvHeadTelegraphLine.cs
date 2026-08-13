using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.TvHead.Projectiles
{
    public class TvHeadTelegraphLine : ModProjectile
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/TvHead/Assets/TelegraphLineTex";

        public ref float OwnerIndex => ref Projectile.ai[0];
        public ref float MaxLifetime => ref Projectile.ai[1];

        private float timer = 0f;

        public override void SetDefaults() {
            Projectile.width = 1;
            Projectile.height = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.timeLeft = 120;
        }

        public override void AI() {
            if (MaxLifetime <= 0f) MaxLifetime = 30f;

            int npcOwner = (int)OwnerIndex;
            if (npcOwner < 0 || npcOwner >= Main.maxNPCs || !Main.npc[npcOwner].active) {
                Projectile.Kill();
                return;
            }

            NPC owner = Main.npc[npcOwner];
            Projectile.Center = owner.Center;

            if (owner.target >= 0 && owner.target < Main.maxPlayers && Main.player[owner.target].active) {
                Player target = Main.player[owner.target];
                Vector2 aimDir = target.Center - Projectile.Center;
                if (aimDir != Vector2.Zero) {
                    Projectile.rotation = aimDir.ToRotation();
                }
            }

            timer++;
            if (timer >= MaxLifetime) {
                Projectile.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex;
            if (ModContent.HasAsset(Texture)) {
                tex = ModContent.Request<Texture2D>(Texture).Value;
            } else {
                tex = TextureAssets.Extra[33].Value;
            }

            float progress = MathHelper.Clamp(timer / MaxLifetime, 0f, 1f);
            float alpha = (float)Math.Sin(progress * MathHelper.Pi);
            float length = 2000f;

            Vector2 scale = new Vector2(length / tex.Width, MathHelper.Lerp(0.8f, 3f, progress));
            Color color = Color.Lerp(Color.Cyan, Color.Red, progress) * alpha * 0.8f;

            Main.EntitySpriteDraw(
                tex,
                Projectile.Center - Main.screenPosition,
                null,
                color,
                Projectile.rotation,
                new Vector2(0, tex.Height / 2f),
                scale,
                SpriteEffects.None,
                0
            );

            return false;
        }
    }
}