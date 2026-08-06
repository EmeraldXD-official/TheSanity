using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class ValkyrieDebris : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.StoneBlock;

        // DAFTAR 10 BLOCK BATU BESAR (Boulder diganti Hellstone agar ukuran proporsional)
        public static readonly int[] BigRocks = new int[]
        {
            ItemID.Hellstone,
            ItemID.StoneBlock,
            ItemID.EbonstoneBlock,
            ItemID.CrimstoneBlock,
            ItemID.PearlstoneBlock,
            ItemID.Granite,
            ItemID.Marble,
            ItemID.Sandstone,
            ItemID.HardenedSand,
            ItemID.Obsidian
        };

        // DAFTAR 10 BLOCK KERIKIL / BATU SEDANG
        public static readonly int[] SmallPebbles = new int[]
        {
            ItemID.SiltBlock,
            ItemID.MudBlock,
            ItemID.ClayBlock,
            ItemID.AshBlock,
            ItemID.DirtBlock,
            ItemID.SandBlock,
            ItemID.SnowBlock,
            ItemID.CopperOre,
            ItemID.IronOre,
            ItemID.SilverOre
        };

        private bool isFalling = false;
        private float rotationSpeed = 0f;

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
        }

        public override void AI()
        {
            if (rotationSpeed == 0f)
            {
                rotationSpeed = Main.rand.NextFloat(-0.25f, 0.25f);
            }

            Projectile.rotation += rotationSpeed;

            // Cek Keberadaan Valkyrie Yoyo
            int parentIndex = (int)Projectile.ai[0];
            bool parentValid = parentIndex >= 0 && parentIndex < Main.maxProjectiles &&
                               Main.projectile[parentIndex].active &&
                               Main.projectile[parentIndex].type == ProjectileID.ValkyrieYoyo;

            if (!parentValid)
            {
                isFalling = true;
            }

            if (!isFalling)
            {
                // ==========================================
                // FASE 1: TERSEDOT KE CENTER YOYO
                // ==========================================
                Projectile parent = Main.projectile[parentIndex];
                Vector2 toCenter = parent.Center - Projectile.Center;
                float distance = toCenter.Length();

                if (distance <= 12f)
                {
                    Projectile.Kill();
                    return;
                }

                float suckSpeed = MathHelper.Clamp(distance * 0.1f, 6f, 18f);
                Vector2 targetVel = toCenter.SafeNormalize(Vector2.Zero) * suckSpeed;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetVel, 0.25f);

                if (Main.rand.NextBool(2))
                {
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Stone, Projectile.velocity * -0.2f, 100, default, 1.0f);
                    d.noGravity = true;
                }
            }
            else
            {
                // ==========================================
                // FASE 2: YOYO HILANG -> JATUH & MEMUDAR
                // ==========================================
                Projectile.friendly = false;
                Projectile.velocity.Y += 0.45f;
                Projectile.velocity.X *= 0.95f;

                Projectile.alpha += 18;
                if (Projectile.alpha >= 255)
                {
                    Projectile.Kill();
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            int visualItemID = (int)Projectile.ai[1];
            if (visualItemID <= 0 || visualItemID >= ItemLoader.ItemCount)
            {
                visualItemID = ItemID.StoneBlock;
            }

            // Memaksa tModLoader memuat texture ke memori agar tidak transparan
            Main.instance.LoadItem(visualItemID);

            Texture2D texture = TextureAssets.Item[visualItemID].Value;

            Rectangle sourceRect = texture.Frame();
            if (Main.itemAnimations[visualItemID] != null)
            {
                sourceRect = Main.itemAnimations[visualItemID].GetFrame(texture);
            }

            Vector2 origin = sourceRect.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // Skala Ukuran
            bool isBig = Array.IndexOf(BigRocks, visualItemID) >= 0;
            float drawScale = isBig ? 1.15f : 0.8f;

            // Pencahayaan Minimum (Terang walau di tempat gelap)
            Color drawColor = Color.Lerp(lightColor, Color.White, 0.45f) * ((255 - Projectile.alpha) / 255f);

            Main.EntitySpriteDraw(
                texture, drawPos, sourceRect, drawColor,
                Projectile.rotation, origin, drawScale,
                SpriteEffects.None, 0
            );

            return false;
        }
    }
}