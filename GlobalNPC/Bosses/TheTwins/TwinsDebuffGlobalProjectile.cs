using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    // ==========================================
    // TwinsDebuffGlobalProjectile — nempelin debuff (Broken Armor + Weak + Bleeding) ke
    // proyektil VANILLA yang dipakai Twins (EyeFire, DeathLaser, CursedFlameHostile).
    //
    // Proyektil CUSTOM kita sendiri (RedPhantasmalBolt, TwinsCursedBeam) di-handle LANGSUNG
    // lewat OnHitPlayer di class masing-masing (gak lewat sini) — soalnya itu ModProjectile
    // milik kita sendiri, gampang di-override langsung.
    //
    // Kenapa perlu GlobalProjectile: EyeFire/DeathLaser/CursedFlameHostile itu proyektil ID
    // VANILLA yang JUGA dipakai musuh/boss LAIN (DeathLaser contoh - dipakai The Twins DAN
    // musuh lain; CursedFlameHostile dipakai Cultist dkk). Kita GAK BOLEH asal nempelin
    // debuff ke SEMUA instance tipe itu di seluruh game — WAJIB ditandain dulu SIAPA yang
    // nembak. Field IsFromTwins ini di-set MANUAL abis Projectile.NewProjectile() di
    // titik-titik spawn Twins (TwinDash.FireEyeFire/FireDeathLaserVolley,
    // TwinsLaserBarrage.FireLaserAtPlayer, TwinsCursedRain/TwinsSpinningCurse/
    // TwinsBorderShot.FireCursedFlame varian) - proyektil dari sumber LAIN (WoF clones,
    // Cultist, dll) TETAP GAK ke-tandain sama sekali, jadi behavior vanilla mereka di luar
    // Twins gak keganggu sama sekali.
    // ==========================================
    public class TwinsDebuffGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public bool IsFromTwins;

        public override bool AppliesToEntity(Projectile projectile, bool lateInstantiation)
        {
            return projectile.type == ProjectileID.EyeFire
                || projectile.type == ProjectileID.DeathLaser
                || projectile.type == ProjectileID.CursedFlameHostile;
        }

        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info)
        {
            if (IsFromTwins)
            {
                ApplyDebuffs(target);
            }
        }

        // ==========================================
        // Helper SATU TEMPAT buat nge-apply paket debuff Twins (Broken Armor + Weak +
        // Bleeding) - dipanggil dari sini (proyektil vanilla yang ditandai), DAN langsung
        // dari RedPhantasmalBolt.cs / TwinsCursedBeam.cs (proyektil custom kita sendiri),
        // DAN dari TwinsRetBeam.cs / TwinsLastStand.cs (beam "konseptual" yang ngedamage
        // lewat target.Hurt() manual, bukan lewat proyektil beneran), DAN dari
        // TwinsRework.cs (contact damage Spazmatism/Retinazer). Durasi-nya SATU KONSTANTA di
        // sini biar gampang di-tune sekali doang buat semua sumber damage Twins.
        // ==========================================
        public const int DebuffDurationTicks = 300; // 5 detik

        public static void ApplyDebuffs(Player target)
        {
            target.AddBuff(BuffID.BrokenArmor, DebuffDurationTicks);
            target.AddBuff(BuffID.Weak, DebuffDurationTicks);
            target.AddBuff(BuffID.Bleeding, DebuffDurationTicks);
        }
    }
}
