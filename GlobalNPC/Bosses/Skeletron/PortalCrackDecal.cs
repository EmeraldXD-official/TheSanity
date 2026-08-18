using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.Skeletron
{
    // === PATTERN BARU: Portal Slam (4x menghentak) ===
    // Decal retakan tanah yang muncul PAS di titik hentakan tiap kali Head
    // menghentak turun (lihat TriggerSlamImpact di
    // SkeletronReworkGlobalNPC) — numpang asset BonePortalGroundCrack.png
    // yang sama kayak dipakai BoneWall buat crack di dasar kolomnya,
    // cuma di sini dibikin ModProjectile TERPISAH biar bisa dipanggil dari
    // mana aja (gak numpang jadi bagian gambar entitas lain) dan otomatis
    // ilang sendiri sesudah beberapa tick.
    //
    // Murni VISUAL: gak ada damage, gak ada collision, gak nge-block apa
    // pun — cuma nge-flash muncul (scale-in cepat) lalu fade-out pelan.
    public class PortalCrackDecal : ModProjectile
    {
        const int FlashInTime = 4;   // muncul instan-ish, kesan "retak seketika" pas kena hentakan
        const int HoldTime = 10;     // nangkring penuh sebentar
        const int FadeOutTime = 20;  // fade keluar pelan
        const int TotalDuration = FlashInTime + HoldTime + FadeOutTime;

        float baseScale = 1f;

        public override string Texture => "TheSanity/GlobalNPC/Bosses/Skeletron/BonePortalGroundCrack";

        public override void SetDefaults()
        {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalDuration + 5;
            Projectile.damage = 0;
        }

        // Dipanggil sekali sesudah NewProjectile() dari SpawnAt() di bawah.
        public void Setup(float scale)
        {
            baseScale = scale;
            Projectile.netUpdate = true;
        }

        public override void AI()
        {
            Projectile.ai[0]++;

            // FIX BARU (request user): crack decal harus kelihatan DI
            // BELAKANG Skeletron, bukan numpuk di depan sprite-nya. Vanilla/
            // tModLoader nyediain "cache list" khusus buat ini —
            // masukin whoAmI ke DrawCacheProjsBehindNPCs TIAP TICK (bukan
            // sekali doang) bikin proyektil ini digambar di layer KHUSUS
            // sebelum NPC, dan otomatis di-skip dari pass gambar normal
            // (jadi gak dobel gambar). Masih di ATAS tile/ground seperti
            // biasa, cuma di bawah NPC.
            if (Main.netMode != NetmodeID.Server)
                Main.instance.DrawCacheProjsBehindNPCs.Add(Projectile.whoAmI);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);

            float timer = Projectile.ai[0];
            float alpha;
            float growPunch;

            if (timer <= FlashInTime)
            {
                float t = timer / FlashInTime;
                alpha = t;
                growPunch = MathHelper.Lerp(0.4f, 1.15f, t); // sedikit "overshoot" pas retak baru muncul, kesan kaget/hentakan
            }
            else if (timer <= FlashInTime + HoldTime)
            {
                alpha = 1f;
                growPunch = MathHelper.Lerp(1.15f, 1f, (timer - FlashInTime) / HoldTime);
            }
            else
            {
                alpha = 1f - MathHelper.Clamp((timer - FlashInTime - HoldTime) / FadeOutTime, 0f, 1f);
                growPunch = 1f;
            }

            Color drawColor = Color.White * alpha * 0.9f;

            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, drawColor,
                0f, origin, baseScale * growPunch, SpriteEffects.None, 0f);

            return false;
        }

        // === Dipanggil dari TriggerSlamImpact (SkeletronReworkGlobalNPC),
        // tiap kali Head menghentak turun & nyampe titik hentakan ===
        // FIX BARU: default scale diperkecil (dulu 3.5f, kegedean/nutupin
        // layar) jadi 1.8f, sesuai request user.
        public static void SpawnAt(IEntitySource source, Vector2 position, float scale = 1.8f)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int index = Projectile.NewProjectile(source, position, Vector2.Zero,
                ModContent.ProjectileType<PortalCrackDecal>(), 0, 0f, Main.myPlayer);

            if (index >= 0 && index < Main.maxProjectiles
                && Main.projectile[index].active
                && Main.projectile[index].ModProjectile is PortalCrackDecal decal)
            {
                decal.Setup(scale);
            }
        }
    }
}