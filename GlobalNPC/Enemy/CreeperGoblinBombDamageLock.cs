using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Enemy
{
    // Hook khusus buat instance DD2GoblinBomb yang dimuncratkan CreeperEnemy pas meledak (bukan
    // yang beneran dari event Old One's Army). DD2GoblinBomb punya sistem damage/tier internal
    // sendiri yang kadang nimpa angka damage yang kita kasih pas NewProjectile() — makanya kita
    // paksa ulang di sini biar PERSIS sesuai yang kita mau. Sekalian nambahin ledakan tile radius
    // kecil (3 block) pas proyektilnya meledak, ngikutin cap pickaxe power progression yang sama
    // kayak ledakan utama Creeper-nya.
    public class CreeperGoblinBombDamageLock : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        private const float TileDestroyRadiusTiles = 3f;

        public bool isFromCreeperExplosion;
        public int lockedDamage;
        public int maxPickPower;

        public override void ModifyHitPlayer(Projectile projectile, Player target, ref Player.HurtModifiers modifiers)
        {
            if (!isFromCreeperExplosion)
                return;

            // PENTING: formula StatModifier.ApplyTo() itu (base * Additive + Flat) * Multiplicative.
            // Flat ada DI DALAM kurung yang dikali Multiplicative -> kalau Multiplicative di-nol-in
            // (versi lama: "*= 0f"), Flat ikut ke-kali 0 juga pas dievaluasi. Makanya damage
            // sebelumnya SELALU jadi 0.
            //
            // Fix: nol-in ADDITIVE (bukan Multiplicative) buat ngilangin kontribusi base damage awal.
            // Additive & Multiplicative itu get-only property (readonly struct), jadi ga bisa
            // di-assign langsung -> harus lewat operator: '+'/'-' nyasar ke Additive, '*'/'/' nyasar
            // ke Multiplicative. Additive defaultnya 1f (=100%), jadi dikurang 1f -> jadi 0f.
            // Multiplicative dibiarin default (1x, ga disentuh) biar Flat lolos utuh.
            modifiers.SourceDamage -= 1f;

            // Flat itu public field biasa (bukan property), bisa di-assign langsung.
            modifiers.SourceDamage.Flat = lockedDamage;
        }

        public override void OnKill(Projectile projectile, int timeLeft)
        {
            if (!isFromCreeperExplosion)
                return;

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return; // Hancurin tile cuma di server/singleplayer biar ga desync

            CreeperExplosionUtils.DestroyTilesInRadius(projectile.Center, TileDestroyRadiusTiles * 16f, maxPickPower);
        }
    }
}
