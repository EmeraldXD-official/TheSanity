using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class Code2Doom : ModProjectile
    {
        // Menggunakan sprite sheet resmi AncientDoomProjectile
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.AncientDoomProjectile;

        public override void SetStaticDefaults()
        {
            // Mengambil jumlah frame animasi sprite sheet dari AncientDoomProjectile
            Main.projFrames[Projectile.type] = Main.projFrames[ProjectileID.AncientDoomProjectile];

            // Cache untuk efek After Image (Jejak Bayangan Ungu)
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;

            // Karakteristik Khusus
            Projectile.penetrate = -1; // Tembus musuh tanpa batas
            Projectile.tileCollide = false; // Menembus dinding/blok
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
        }

        public override void AI()
        {
            int parentIndex = (int)Projectile.ai[0];
            if (parentIndex < 0 || parentIndex >= Main.maxProjectiles)
            {
                Projectile.Kill();
                return;
            }

            Projectile parent = Main.projectile[parentIndex];

            // Jika Yoyo Code 2 hancur/ditarik kembali, seluruh proyektil ikut hancur (menghentikan loop)
            if (!parent.active || parent.type != ProjectileID.Code2 || parent.owner != Projectile.owner)
            {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2; // Menjaga waktu hidup dikontrol oleh timer internal

            float waveIndex = Projectile.ai[1]; // Index proyektil dalam 1 wave (0, 1, 2, 3)

            // Counter Timer Gerakan
            Projectile.localAI[1]++;
            float timer = Projectile.localAI[1];

            float expandTime = 50f; // Waktu mekar/menjauh (0.83 detik)
            float returnTime = 50f; // Waktu kuncup/mendekat kembali (0.83 detik)
            float totalLifetime = expandTime + returnTime;

            // Jika timer gerakan selesai (sampai di pusat Yoyo), hancurkan proyektil ini
            if (timer > totalLifetime)
            {
                Projectile.Kill();
                return;
            }

            // 1. HITUNG RADIUS SPIRAL (Jarak dari Yoyo)
            float maxRadius = 180f; // Jarak maksimal mekar (11.25 block)
            float currentRadius;

            bool isReturning = timer > expandTime;

            if (!isReturning)
            {
                // Menjauh dari Yoyo (0 -> 180px)
                currentRadius = MathHelper.Lerp(0f, maxRadius, timer / expandTime);
            }
            else
            {
                // Mendekat kembali ke Yoyo (180px -> 0px)
                currentRadius = MathHelper.Lerp(maxRadius, 0f, (timer - expandTime) / returnTime);
            }

            // 2. PERFECT LOOP TRIGGER
            // Saat gelombang ini tepat memasuki fase mendekat (Tick 51), pemicu gelombang 4 proyektil berikutnya!
            if ((int)timer == (int)expandTime + 1 && (int)waveIndex == 0 && Projectile.owner == Main.myPlayer)
            {
                int flameDamage = (int)(parent.damage * 0.50f);

                for (int i = 0; i < 4; i++)
                {
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromAI(),
                        parent.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<Code2Doom>(),
                        flameDamage,
                        parent.knockBack * 0.3f,
                        Projectile.owner,
                        ai0: parent.whoAmI,
                        ai1: i
                    );
                }
            }

            // 3. MOVING IN ORBIT (Berputar memutari Yoyo)
            float rotSpeed = 0.09f; // Kecepatan putar mengelilingi Yoyo
            float baseAngle = waveIndex * MathHelper.PiOver2; // Terpisah 90 derajat antar 4 proyektil
            float currentAngle = baseAngle + (timer * rotSpeed);

            // Set posisi mengorbit pusat Yoyo Code 2
            Projectile.Center = parent.Center + currentAngle.ToRotationVector2() * currentRadius;
            Projectile.rotation = currentAngle;

            // 4. ANIMASI SPRITE FRAME & LIGHTING UNGU
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 5)
            {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Projectile.type];
            }

            Lighting.AddLight(Projectile.Center, 0.7f, 0.1f, 0.9f); // Cahaya Ungu Pekat
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Inflict debuff ShadowFlame selama 3 detik (180 ticks)
            target.AddBuff(BuffID.ShadowFlame, 180);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            int frameHeight = texture.Height / Main.projFrames[Projectile.type];
            Rectangle frameRect = new Rectangle(0, Projectile.frame * frameHeight, texture.Width, frameHeight);
            Vector2 origin = new Vector2(texture.Width * 0.5f, frameHeight * 0.5f);

            // 1. DRAW AFTER IMAGE UNGU (Jejak Bayangan)
            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                float alpha = (1f - (i / (float)Projectile.oldPos.Length)) * 0.5f;

                Main.EntitySpriteDraw(
                    texture,
                    drawPos,
                    frameRect,
                    Color.MediumPurple * alpha,
                    Projectile.oldRot[i],
                    origin,
                    Projectile.scale,
                    SpriteEffects.None,
                    0
                );
            }

            // 2. DRAW MAIN SPRITE (Menyala Terang)
            Vector2 mainPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(
                texture,
                mainPos,
                frameRect,
                Color.White * 0.95f,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            return false;
        }
    }
}