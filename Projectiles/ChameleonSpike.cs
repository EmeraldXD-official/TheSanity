using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System; 
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class ChameleonSpike : ModProjectile
    {
        public Color projectileColor = Color.White;
        public int parentAlpha = 178;

        public override string Texture => "TheSanity/Projectiles/RainbowSpike";

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.aiStyle = -1; 
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.timeLeft = 360;
            Projectile.scale = 1.0f;
        }

        public override void AI()
        {
            // -------------------------------------------------------------------------
            // [LOKASI BALANCING]: GRAVITASI DAN KECEPATAN JATUH DURI
            // -------------------------------------------------------------------------
            float spikeGravity = 0.22f;    
            float maxSpikeFall = 14f;      
            // -------------------------------------------------------------------------

            Projectile.velocity.Y += spikeGravity;
            if (Projectile.velocity.Y > maxSpikeFall)
                Projectile.velocity.Y = maxSpikeFall;

            if (Projectile.velocity != Vector2.Zero)
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Lighting.AddLight(Projectile.Center, projectileColor.ToVector3() * 0.35f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            int biome = GetBiomeFromCoords((int)Projectile.Center.X / 16, (int)Projectile.Center.Y / 16);
            int debuff = GetBiomeDebuff(biome);
            
            int duration = Main.raining ? 600 : 300;
            target.AddBuff(debuff, duration);

            if (Main.raining)
            {
                target.AddBuff(BuffID.Electrified, 300);
            }
        }

        private int GetBiomeFromCoords(int x, int y)
        {
            bool hasSnow = false, hasSand = false, hasJungle = false, hasCrimson = false, hasCorruption = false, hasHallow = false;
            
            int startX = Math.Clamp(x - 4, 0, Main.maxTilesX - 1);
            int endX = Math.Clamp(x + 4, 0, Main.maxTilesX - 1);
            int startY = Math.Clamp(y - 4, 0, Main.maxTilesY - 1);
            int endY = Math.Clamp(y + 4, 0, Main.maxTilesY - 1);

            for (int i = startX; i <= endX; i++)
            {
                for (int j = startY; j <= endY; j++)
                {
                    Tile t = Main.tile[i, j];
                    if (t != null && t.HasTile)
                    {
                        int type = t.TileType;
                        if (type == TileID.SnowBlock || type == TileID.IceBlock) hasSnow = true;
                        if (type == TileID.Sand || type == TileID.HardenedSand || type == TileID.Sandstone) hasSand = true;
                        if (type == TileID.JungleGrass || type == TileID.Mud) hasJungle = true;
                        if (type == TileID.CrimsonGrass || type == TileID.Crimstone || type == TileID.FleshIce) hasCrimson = true;
                        if (type == TileID.CorruptGrass || type == TileID.Ebonstone || type == TileID.CorruptIce) hasCorruption = true;
                        if (type == TileID.HallowedGrass || type == TileID.Pearlstone || type == TileID.HallowedIce) hasHallow = true; // FIX SYNC HALLOW
                    }
                }
            }

            if (hasCrimson) return 4;
            if (hasCorruption) return 5;
            if (hasHallow) return 9; // AREA HALLOW
            if (hasSnow) return 1;
            if (hasSand) return 2;
            if (hasJungle) return 3;
            if (y > Main.UnderworldLayer) return 7;
            if (y < Main.worldSurface * 0.4f) return 8;

            return 0; 
        }

        private int GetBiomeDebuff(int biome)
        {
            switch (biome)
            {
                case 9: return BuffID.NoBuilding; // MATCH DEBUFF BARU
                case 0: return BuffID.OgreSpit;
                case 1: return BuffID.Frostburn;
                case 2: return BuffID.OnFire;
                case 3: return BuffID.Poisoned;
                case 4: return BuffID.Ichor;
                case 5: return BuffID.CursedInferno;
                case 6: return BuffID.Cursed;
                case 7: return BuffID.Burning;
                case 8: return BuffID.VortexDebuff;
                default: return BuffID.OgreSpit;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

            for (int k = Projectile.oldPos.Length - 1; k > 0; k--)
            {
                if (Projectile.oldPos[k] == Vector2.Zero) continue;
                Vector2 trailPos = Projectile.oldPos[k] + (Projectile.Size * 0.5f) - Main.screenPosition;
                float opacity = 1f - ((float)k / Projectile.oldPos.Length);
                
                Color trailColor = projectileColor * opacity * 0.5f;
                trailColor.A = 0; 
                
                float trailScale = Projectile.scale * opacity * 0.85f;
                Main.EntitySpriteDraw(texture, trailPos, null, trailColor, Projectile.rotation, drawOrigin, trailScale, SpriteEffects.None, 0);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float alphaPct = (255f - parentAlpha) / 255f;
            Color mainDrawColor = projectileColor * alphaPct;

            Main.EntitySpriteDraw(texture, drawPos, null, mainDrawColor, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);

            return false;
        }
    }
}