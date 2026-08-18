using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.Skeletron
{
    // === PATTERN BARU: Slam Telegraph Beam (referensi Astrageldon Slime) ===
    // Garis vertikal peringatan yang muncul PAS SEBELUM Head slam turun di
    // pattern Portal Slam (dipanggil dari State.PortalSlamCast begitu fase
    // dash-nya mulai) — nunjukin PERSIS di titik mana BigBoneSpike bakal
    // erupsi, biar player punya waktu kabur dari situ SEBELUM kena. Niru
    // beam telegraph Astrageldon Slime: dia jump, muncul garis di bawahnya
    // nunjukin titik pendaratan, BARU dia slam turun.
    //
    // Murni VISUAL: gak ada damage, gak ada collision, gak nge-block apa
    // pun. Durasinya otomatis disamain sama lama fase dash Head (lihat
    // pemanggilnya di State.PortalSlamCast), jadi beam-nya ilang PERSIS pas
    // Head beneran nyampe/nyentuh titik hentakan.
    public class SlamTelegraphBeam : ModProjectile
    {
        const float BeamHeight = 900f; // panjang garis ke ATAS dari titik target
        const float BeamWidth = 10f;   // ketebalan garis

        const int FadeInTime = 5;
        const int FadeOutTime = 5;

        int totalDuration = 20; // di-set dari luar lewat Setup(), ini cuma fallback

        static readonly Color TelegraphColor = new Color(190, 90, 255); // ungu, senada VoidSparkDust/portal

        // CATATAN: class ini SENGAJA gak pernah gambar pakai
        // TextureAssets.Projectile[Projectile.type] (lihat PreDraw — yang
        // dipakai buat gambar beneran itu Terraria.GameContent.
        // TextureAssets.MagicPixel, tekstur 1x1 putih polos bawaan vanilla
        // yang di-stretch jadi garis, gak butuh sprite custom). Property
        // Texture di bawah cuma buat "nyenengin" requirement tModLoader
        // yang wajib nunjuk ke file asset yang BENERAN ADA — numpang path
        // asset lain yang udah pasti ke-load di mod ini (BonePortalGroundCrack),
        // isinya sendiri gak pernah kepake buat gambar apa pun di sini.
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
            Projectile.damage = 0;
        }

        // Dipanggil sekali dari SpawnAt() di bawah, sesudah NewProjectile().
        public void Setup(int duration)
        {
            totalDuration = duration;
            Projectile.timeLeft = duration + 5; // buffer kecil
            Projectile.netUpdate = true;
        }

        public override void AI()
        {
            Projectile.ai[0]++;

            if (Projectile.ai[0] >= totalDuration)
                Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            float timer = Projectile.ai[0];
            float alpha;

            if (timer <= FadeInTime)
                alpha = timer / FadeInTime;
            else if (timer >= totalDuration - FadeOutTime)
                alpha = MathHelper.Clamp((totalDuration - timer) / FadeOutTime, 0f, 1f);
            else
                alpha = 1f;

            // kesan "berkedip" kayak warning beam, bukan garis solid diem
            float pulse = 0.55f + 0.45f * (float)System.Math.Sin(timer * 0.9f);

            Color drawColor = TelegraphColor * alpha * pulse;

            Vector2 bottom = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0.5f, 1f); // pivot bawah-tengah pixel -> garis stretch ke ATAS dari titik target
            Vector2 scale = new Vector2(BeamWidth, BeamHeight);

            Main.spriteBatch.Draw(pixel, bottom, null, drawColor, 0f, origin, scale, SpriteEffects.None, 0f);

            return false;
        }

        // === Dipanggil dari State.PortalSlamCast (SkeletronReworkGlobalNPC)
        // begitu fase dash turun mulai ===
        // duration biasanya disamain persis sama lama fase dash itu
        // (slamTravelTime), jadi beam-nya ilang PAS Head nyampe.
        public static void SpawnAt(IEntitySource source, Vector2 position, int duration)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int index = Projectile.NewProjectile(source, position, Vector2.Zero,
                ModContent.ProjectileType<SlamTelegraphBeam>(), 0, 0f, Main.myPlayer);

            if (index >= 0 && index < Main.maxProjectiles
                && Main.projectile[index].active
                && Main.projectile[index].ModProjectile is SlamTelegraphBeam beam)
            {
                beam.Setup(duration);
            }
        }

        // === Multiplayer sync ===
        // totalDuration cuma di-set sekali server-side lewat Setup(), jadi
        // perlu dikirim manual ke client — pola sama kayak field extra di
        // projectile lain (BigBoneSpike, BoneWall, dst).
        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write((short)totalDuration);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            totalDuration = reader.ReadInt16();
        }
    }
}
