using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity.Systems
{
    /// <summary>
    /// Sistem border arena versi solo mod (full vanilla, tanpa dependency CalamityMod),
    /// SEKARANG berbentuk LINGKARAN dan digambar pakai tekstur "AuraOut.png"
    /// (TheSanity/Projectiles/AuraOut, 500x500, putih polos) yang di-tint warna
    /// merah -> hijau tergantung HealthPercent (1 = merah/sehat penuh, 0 = hijau/mau mati).
    ///
    /// Border ini gak lagi statis kayak versi kotak dulu - tiap Border sekarang
    /// punya delegate Tick yang dipanggil tiap frame (diisi dari luar, misal
    /// TwinsArenaGlobalNPC), jadi bisa dipakai buat bikin border ngikutin boss,
    /// narik player yang keluar, dsb, tanpa nge-hardcode logic Twins di sini.
    /// </summary>
    public class ArenaBorderSystem : ModSystem
    {
        public class Border
        {
            public Vector2 Center;

            // Radius SAAT INI (hasil animasi) - dipakai LANGSUNG buat 3 hal sekaligus:
            // visual (PostDrawTiles di bawah), hitbox collider (ArenaBorderColliderNPC,
            // di-update tiap tick lewat Tick delegate di TwinsArenaGlobalNPC), dan logic
            // "tarik player masuk" (PullPlayersInside). Jadi begitu Radius dianimasikan
            // (muncul/membesar/mengecil), KETIGANYA otomatis ikut berubah bareng - gak ada
            // lagi hitbox yang "ketinggalan" stuck di ukuran/posisi lama.
            public float Radius;

            // Radius TUJUAN animasi yang lagi berjalan (sama dengan Radius kalau lagi gak
            // ada animasi berjalan). Disediakan buat referensi luar kalau perlu tahu ke
            // mana border ini sedang menuju.
            public float TargetRadius;

            // True begitu border ini mulai proses "mengecil untuk dihapus" (lihat
            // PostUpdateEverything) - dipakai locking supaya AnimateRadiusTo(0, ...) itu
            // cuma dipicu SEKALI, dan supaya kode luar (mis. logic tarik player) bisa
            // berhenti narik player begitu border lagi dalam proses menghilang.
            public bool IsRemoving = false;

            // Dipanggil tiap tick SEBELUM RemovalCondition dicek. Dipakai buat
            // update posisi border (ngikutin boss), reposisi collider, narik
            // player yang keluar arena, dll. Boleh null kalau border statis.
            public Action<Border> Tick = null;

            // Dipanggil tiap tick. Kalau return true, border ini MULAI proses hapus
            // (animasi mengecil ke 0 dulu - lihat PostUpdateEverything), BUKAN langsung
            // hilang instan lagi.
            public Func<bool> RemovalCondition = () => true;

            // Dipanggil TEPAT SEBELUM border ini beneran dibuang dari ActiveBorders
            // (animasi mengecilnya udah kelar, Radius udah ~0). Dipakai buat beberes
            // referensi luar (mis. collider NPC-nya, field static di caller, dll).
            public Action OnFullyRemoved = null;

            // Tick game (Main.GameUpdateCount) waktu Border ini dibikin.
            // Dipakai sebagai titik nol buat animasi warna merah <-> hijau,
            // biar animasinya mulai dari awal tiap kali border baru muncul
            // (bukan ngikut jam global yang udah jalan dari awal dunia).
            public int SpawnTick = (int)Main.GameUpdateCount;

            // Opacity tekstur aura, biar gak nutupin gameplay kebanyakan.
            public float Opacity = 0.5f;

            // === Kustomisasi tekstur (dipakai TorchGod) ===
            // Kalau TexturePath == null, Border ini pakai tekstur bulat default
            // (AuraOut.png) + animasi warna merah<->hijau (UseColorPulse), sama
            // persis kayak perilaku lama (dipakai Twins).
            //
            // Kalau TexturePath diisi, Border ini pakai spritesheet sendiri:
            // FrameCount frame tersusun HORIZONTAL (kiri ke kanan) dalam SATU
            // baris, masing-masing frame dianggap PERSEGI (lebar = tinggi =
            // tinggi tekstur totalnya). Animasinya loop terus selama Border
            // masih ada, TicksPerFrame tick per frame, dan CrossfadeTicks tick
            // terakhir di tiap frame dipakai buat nge-blend/fade ke frame
            // berikutnya (bukan potong kasar).
            public string TexturePath = null;
            public int FrameCount = 1;
            public int TicksPerFrame = 10;
            public int CrossfadeTicks = 4;

            // False = tint putih polos (dipakai kalau tekstur sendiri sudah
            // "berwarna", misal TorchGod). True = tint merah<->hijau berbasis
            // waktu kayak sebelumnya (dipakai Twins/AuraOut).
            public bool UseColorPulse = true;

            // Asset tekstur custom punya Border ini sendiri, di-load lazy
            // begitu pertama kali digambar (lihat ArenaBorderSystem.PostDrawTiles).
            internal Asset<Texture2D> CustomTexture;

            private float radiusAnimFrom;
            private float radiusAnimTarget;
            private int radiusAnimStartTick;
            private int radiusAnimDuration; // <= 0 berarti gak ada animasi radius berjalan

            // Mulai animasi Radius menuju "target" selama "durationTicks" tick, pakai
            // ease-out quad (cepat di awal, halus mendekati ujung) - dipakai buat SEMUA
            // perubahan radius yang dulunya instan: muncul pertama kali ("timbul", dari 0),
            // membesar pas Phase 2 (dari radius saat ini ke radius baru), dan mengecil pas
            // mau dihapus (ke 0).
            public void AnimateRadiusTo(float target, int durationTicks)
            {
                radiusAnimFrom = Radius;
                radiusAnimTarget = target;
                radiusAnimStartTick = (int)Main.GameUpdateCount;
                radiusAnimDuration = Math.Max(1, durationTicks);
                TargetRadius = target;
            }

            // Dipanggil tiap tick dari ArenaBorderSystem.PostUpdateEverything SEBELUM
            // RemovalCondition dicek, biar Radius selalu up-to-date buat dipakai Tick
            // delegate (collider/pull-player) di tick yang sama.
            public void UpdateRadiusAnimation()
            {
                if (radiusAnimDuration <= 0)
                    return;

                int elapsed = (int)Main.GameUpdateCount - radiusAnimStartTick;
                float t = MathHelper.Clamp(elapsed / (float)radiusAnimDuration, 0f, 1f);
                float eased = 1f - (1f - t) * (1f - t); // ease-out quad

                Radius = MathHelper.Lerp(radiusAnimFrom, radiusAnimTarget, eased);

                if (t >= 1f)
                {
                    Radius = radiusAnimTarget;
                    radiusAnimDuration = 0; // animasi kelar, berhenti nge-update tiap tick
                }
            }
        }

        // Berapa tick buat satu arah animasi (merah -> hijau ATAU hijau -> merah).
        // 60 tick = 1 detik (di 60 tps), jadi 180 = 3 detik satu arah,
        // berarti satu putaran penuh (merah -> hijau -> merah) = 6 detik.
        private const float AnimationHalfCycleTicks = 180f;

        // Hitung warna animasi merah<->hijau berbasis waktu sejak Border dibikin.
        // Gak lagi nyambung ke health boss sama sekali - murni animasi visual.
        private static Color GetAnimatedColor(Border b)
        {
            float elapsed = Main.GameUpdateCount - b.SpawnTick;
            float speed = MathHelper.Pi / AnimationHalfCycleTicks;

            // sin() bikin transisi-nya halus (ease in/out) di ujung-ujungnya,
            // bukan lompat linear kaku.
            float percent = (MathF.Sin(elapsed * speed - MathHelper.PiOver2) + 1f) / 2f;

            return Color.Lerp(Color.Red, Color.LimeGreen, percent);
        }

        public static List<Border> ActiveBorders = new();

        // Berapa tick animasi "mengecil" pas border mau dihapus (RemovalCondition true),
        // dan seberapa kecil Radius-nya baru dianggap "beneran abis" (baru boleh dibuang
        // dari ActiveBorders begitu di bawah ambang ini).
        private const int ShrinkDurationTicks = 30;   // ~0.5 detik
        private const float RemovalRadiusThreshold = 4f;

        // Tekstur "AuraOut.png" - lingkaran putih polos 500x500 yang jadi dasar
        // buat semua border, tinggal di-tint warnanya tiap Border sesuai HealthPercent.
        private static Asset<Texture2D> auraTexture;
        private const string AuraTexturePath = "TheSanity/Projectiles/AuraOut";

        // Ukuran asli tekstur AuraOut.png. Kalau file-nya diganti ukurannya,
        // update juga angka ini (atau baca dari auraTexture.Width/Height langsung).
        private const float AuraTextureSize = 500f;

        public override void OnModLoad()
        {
            if (Main.dedServ)
                return;

            auraTexture = ModContent.Request<Texture2D>(AuraTexturePath, AssetRequestMode.AsyncLoad);
        }

        public override void OnModUnload()
        {
            auraTexture = null;
        }

        public override void PostUpdateEverything()
        {
            for (int i = ActiveBorders.Count - 1; i >= 0; i--)
            {
                Border b = ActiveBorders[i];

                b.Tick?.Invoke(b);
                b.UpdateRadiusAnimation();

                // Begitu RemovalCondition ketrigger PERTAMA KALINYA, JANGAN langsung hapus -
                // mulai animasi MENGECIL dulu ("mengecil pas hilang", simetris sama "timbul"
                // pas muncul di TwinsArenaGlobalNPC.OnSpawn). Baru beneran dibuang dari list
                // begitu Radius-nya udah nyaris 0 (cek di bawah).
                if (!b.IsRemoving && b.RemovalCondition())
                {
                    b.IsRemoving = true;
                    b.AnimateRadiusTo(0f, ShrinkDurationTicks);
                }

                if (b.IsRemoving && b.Radius <= RemovalRadiusThreshold)
                {
                    b.OnFullyRemoved?.Invoke();
                    ActiveBorders.RemoveAt(i);
                }
            }
        }

        // Gambar semua aura lingkaran di world space, di atas tile tapi di bawah
        // player/NPC (PostDrawTiles). Kalau mau di atas semuanya, pindah ke hook
        // draw lain (misal lewat PlayerLayer atau detour DrawPlayers).
        public override void PostDrawTiles()
        {
            if (Main.dedServ || ActiveBorders.Count == 0)
                return;

            // BlendState.Additive dipakai sengaja: kalau tekstur bagian yang
            // "harusnya transparan" itu ternyata masih ada sisa hitam-hitam
            // (bukan alpha 0 murni), additive bikin bagian hitam itu otomatis
            // gak kegambar (hitam + background = background), sementara bagian
            // putih/terang di tekstur tetep nyala nge-glow.
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            foreach (Border b in ActiveBorders)
                DrawBorder(b);

            Main.spriteBatch.End();
        }

        private void DrawBorder(Border b)
        {
            Vector2 drawPos = b.Center - Main.screenPosition;
            Color baseTint = (b.UseColorPulse ? GetAnimatedColor(b) : Color.White) * b.Opacity;

            // === Border default (bulat, AuraOut.png statis) - perilaku lama ===
            if (b.TexturePath == null)
            {
                if (auraTexture == null || !auraTexture.IsLoaded)
                    return;

                Texture2D tex = auraTexture.Value;
                Vector2 circleOrigin = new Vector2(tex.Width / 2f, tex.Height / 2f);
                float circleScale = (b.Radius * 2f) / AuraTextureSize;

                Main.spriteBatch.Draw(tex, drawPos, null, baseTint, 0f, circleOrigin, circleScale, SpriteEffects.None, 0f);
                return;
            }

            // === Border custom (spritesheet ber-frame + crossfade) ===
            b.CustomTexture ??= ModContent.Request<Texture2D>(b.TexturePath, AssetRequestMode.AsyncLoad);

            if (!b.CustomTexture.IsLoaded)
                return;

            Texture2D sheet = b.CustomTexture.Value;
            int frameCount = Math.Max(1, b.FrameCount);
            int frameWidth = sheet.Width / frameCount;
            int frameHeight = sheet.Height;

            int ticksPerFrame = Math.Max(1, b.TicksPerFrame);
            int cycleLength = frameCount * ticksPerFrame;
            int tickInCycle = (int)(Main.GameUpdateCount - b.SpawnTick) % cycleLength;
            if (tickInCycle < 0)
                tickInCycle += cycleLength;

            int frameIndex = tickInCycle / ticksPerFrame;
            int nextFrameIndex = (frameIndex + 1) % frameCount;
            int tickInFrame = tickInCycle % ticksPerFrame;

            // CrossfadeTicks tick terakhir sebelum ganti frame dipakai buat
            // nge-fade frame sekarang keluar sambil frame berikutnya fade masuk.
            int crossfadeTicks = Math.Clamp(b.CrossfadeTicks, 0, ticksPerFrame);
            int fadeStartTick = ticksPerFrame - crossfadeTicks;

            float nextAlpha = 0f;
            if (crossfadeTicks > 0 && tickInFrame >= fadeStartTick)
                nextAlpha = (tickInFrame - fadeStartTick) / (float)crossfadeTicks;

            float currentAlpha = 1f - nextAlpha;

            Rectangle currentSrc = new Rectangle(frameIndex * frameWidth, 0, frameWidth, frameHeight);
            Rectangle nextSrc = new Rectangle(nextFrameIndex * frameWidth, 0, frameWidth, frameHeight);
            Vector2 frameOrigin = new Vector2(frameWidth / 2f, frameHeight / 2f);

            // Asumsi frame persegi (lebar == tinggi), jadi tinggi frame dipakai
            // sebagai referensi skala baik buat Circle maupun Square.
            float scale = (b.Radius * 2f) / frameHeight;

            if (currentAlpha > 0f)
                Main.spriteBatch.Draw(sheet, drawPos, currentSrc, baseTint * currentAlpha, 0f, frameOrigin, scale, SpriteEffects.None, 0f);

            if (nextAlpha > 0f)
                Main.spriteBatch.Draw(sheet, drawPos, nextSrc, baseTint * nextAlpha, 0f, frameOrigin, scale, SpriteEffects.None, 0f);
        }

        public override void OnWorldUnload()
        {
            ActiveBorders.Clear();
        }
    }
}
