using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Globals
{
    // Hook ini nge-cover semua serangan berbentuk Projectile:
    // panah/peluru (Ranged), sihir (Magic), dan minion/sentry (Summoner).
    // Melee TIDAK perlu disentuh, vanilla sudah otomatis nerapin efek
    // Flask ke serangan melee langsung.
    public class FlaskProjectileGlobal : GlobalProjectile
    {
        // "Database" global: Buff Flask (di Player) -> (Debuff yang kena ke NPC, Durasi tick)
        // Dictionary ini PUBLIC & STATIC supaya mod lain (atau kode kamu sendiri)
        // bisa nambahin entry baru tanpa harus edit file ini / reference project ini.
        public static readonly Dictionary<int, (int npcDebuff, int duration)> FlaskEffects = new();

        // Ini API pendaftarannya. Panggil ini dari mod manapun (termasuk mod ini sendiri)
        // buat nambahin flask baru ke sistem, tanpa perlu bikin DLC/addon terpisah.
        public static void AddFlaskEffect(int flaskBuffType, int npcDebuffType, int durationTicks)
        {
            FlaskEffects[flaskBuffType] = (npcDebuffType, durationTicks);
        }

        public override void Load()
        {
            // Daftarin semua flask VANILLA di sini secara default.
            // Nama internal buff vanilla itu "WeaponImbueX", bukan "FlaskOfX".
            AddFlaskEffect(BuffID.WeaponImbueFire,         BuffID.OnFire,        180); // 3 detik
            AddFlaskEffect(BuffID.WeaponImbuePoison,       BuffID.Poisoned,      300); // 5 detik
            AddFlaskEffect(BuffID.WeaponImbueVenom,        BuffID.Venom,         600); // 10 detik
            AddFlaskEffect(BuffID.WeaponImbueCursedFlames, BuffID.CursedInferno, 240); // 4 detik
            AddFlaskEffect(BuffID.WeaponImbueIchor,        BuffID.Ichor,         480); // 8 detik
            AddFlaskEffect(BuffID.WeaponImbueNanites,      BuffID.Confused,      90);  // disederhanakan
        }

        public override void Unload()
        {
            FlaskEffects.Clear();
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers)
                return;

            Player player = Main.player[projectile.owner];
            if (player == null || !player.active || player.dead)
                return;

            foreach (var kvp in FlaskEffects)
            {
                int flaskBuffId = kvp.Key;
                if (player.HasBuff(flaskBuffId))
                {
                    var (npcDebuff, duration) = kvp.Value;
                    target.AddBuff(npcDebuff, duration);
                }
            }
        }
    }
}
