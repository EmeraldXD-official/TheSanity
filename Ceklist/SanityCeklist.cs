using System.Collections.Generic;
using Terraria.ModLoader;
using Terraria.ID;
using Terraria.Localization;

namespace TheSanity
{
    // ==========================================================
    // INTEGRASI BOSS CHECKLIST
    // File ini khusus mendaftarkan semua boss custom mod
    // "TheSanity" ke mod Boss Checklist (kalau ter-install).
    // Kalau mau nambah boss lain nanti, tinggal tambah satu
    // blok Mod.Call baru di bawah Twinkle.
    // ==========================================================
    public class SanityCeklist : ModSystem
    {
        public override void PostSetupContent()
        {
            // Kalau player tidak install Boss Checklist, skip aja —
            // jangan sampai bikin mod ini error/crash.
            if (!ModLoader.TryGetMod("BossChecklist", out Mod bossChecklist))
            {
                Mod.Logger.Info("[SanityCeklist] BossChecklist tidak terdeteksi/tidak di-enable — skip registrasi.");
                return;
            }

            Mod.Logger.Info("[SanityCeklist] BossChecklist terdeteksi, mencoba registrasi boss...");
            RegisterTwinkle(bossChecklist);
            // RegisterBossLainnya(bossChecklist); // <- contoh kalau nanti nambah boss baru
        }

        private void RegisterTwinkle(Mod bossChecklist)
        {
            try
            {
                bossChecklist.Call(
                    "LogBoss",                                                     // 0. Tipe entry
                    Mod,                                                           // 1. Instance mod TheSanity
                    "Twinkle",                                                     // 2. Internal Name (string, alfanumerik doang, no spasi)

                    // 3. Progression value.
                    // Kamu bilang Twinkle ditaruh SETELAH Eater of Worlds / Brain of Cthulhu
                    // dan SEBELUM Queen Bee. Referensi kasar urutan vanilla:
                    //   King Slime ~0  -> Eye of Cthulhu ~1 -> EoW/BoC ~2 -> Queen Bee ~3 -> Skeletron ~4
                    // 2.3f menaruh Twinkle tepat di antara EoW/BoC (2) dan Queen Bee (3).
                    // CEK ULANG angka pastinya di halaman wiki "Boss Progression Values"
                    // biar posisinya presisi dibanding boss-boss mod lain yang kamu pakai juga.
                    2.3f,

                    // 4. Downed boolean — WAJIB nyambung ke flag yang sama dipakai progression lock
                    (System.Func<bool>)(() => TwinkleDownedSystem.downedTwinkle),

                    // 5. Boss ID / List of IDs — NPC ID boss (bisa int tunggal atau List<int> kalau multi-part)
                    ModContent.NPCType<global::TheSanity.GlobalNPC.Bosses.Twinkle.Twinkle>(),

                    // 6. Additional Entry Data (opsional) — availability, collectibles, spawnItems
                    //    SEKARANG semua masuk sini lewat Dictionary, BUKAN argumen posisi terpisah lagi.
                    new Dictionary<string, object>() {
                        ["availability"] = (System.Func<bool>)(() => true),
                        ["collectibles"] = new List<int> { ModContent.ItemType<global::TheSanity.Items.Placeable.TwinkleRelic>() },
                        ["spawnItems"] = ModContent.ItemType<global::TheSanity.GlobalNPC.Bosses.Twinkle.SatterdStars>(),

                        // "spawnInfo" ini yang bikin teks "Condition Unknown" di Boss Log berubah
                        // jadi info spawn beneran. [i:TheSanity/SatterdStars] otomatis render
                        // jadi ikon item Sattered Stars-nya langsung di dalam teks.
                        ["spawnInfo"] = Language.GetOrRegister(
                            "Mods.TheSanity.NPCs.Twinkle.BossChecklistSpawnInfo",
                            () => "Use [i:TheSanity/SatterdStars] at night to summon Twinkle."
                        ),
                    }
                );

                Mod.Logger.Info("[SanityCeklist] Twinkle berhasil didaftarkan ke Boss Checklist.");
            }
            catch (System.Exception ex)
            {
                // Kalau ada argumen yang nggak cocok tipe/urutan, exception-nya bakal
                // muncul jelas di sini, bukan gagal diam-diam.
                Mod.Logger.Error("[SanityCeklist] GAGAL mendaftarkan Twinkle ke Boss Checklist!", ex);
            }
        }
    }
}
